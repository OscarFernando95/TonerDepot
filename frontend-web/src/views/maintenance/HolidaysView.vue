<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as holidaysApi from '../../api/holidays'
import type { HolidayDto } from '../../api/holidays'
import { useAuthStore } from '../../stores/auth'
import { RoleNames } from '../../api/types'

const auth = useAuthStore()
const canEdit = auth.hasRole(RoleNames.Administrador)

// Las fechas son de calendario (yyyy-MM-dd): se formatean en UTC para no correrse un día por la zona del navegador.
const dateFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'full', timeZone: 'UTC' })
const formatDate = (value: string) => dateFormatter.format(new Date(`${value}T00:00:00Z`))

const currentYear = new Date().getFullYear()
const year = ref(currentYear)
const years = [currentYear - 1, currentYear, currentYear + 1, currentYear + 2]
const holidays = ref<HolidayDto[]>([])
const loading = ref(false)

const dialogVisible = ref(false)
const saving = ref(false)
const form = ref({ date: '', name: '' })

const legalCount = computed(() => holidays.value.filter((h) => h.source === 'legal' && !h.isWorkingDay).length)

async function load(silent = false) {
  if (!silent) loading.value = true
  try {
    const { data } = await holidaysApi.listHolidays(year.value)
    holidays.value = data
  } catch (err: any) {
    console.error('No se pudieron cargar los festivos:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los festivos.')
  } finally {
    loading.value = false
  }
}

function openDialog() {
  form.value = { date: '', name: '' }
  dialogVisible.value = true
}

async function saveClosure() {
  if (!form.value.date || !form.value.name.trim()) return
  saving.value = true
  try {
    await holidaysApi.setHolidayOverride(form.value.date, form.value.name.trim(), false)
    ElMessage.success('Día no laborable agregado.')
    dialogVisible.value = false
    await load()
  } catch (err: any) {
    console.error('No se pudo guardar el día no laborable:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar.')
  } finally {
    saving.value = false
  }
}

async function markWorking(row: HolidayDto) {
  await ElMessageBox.confirm(`¿Tratar el ${formatDate(row.date)} (${row.name}) como día laborable?`, 'Confirmar', { type: 'warning' })
  try {
    await holidaysApi.setHolidayOverride(row.date, row.name, true)
    ElMessage.success('Ajuste guardado.')
    await load()
  } catch (err: any) {
    console.error('No se pudo marcar como laborable:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar.')
  }
}

async function removeOverride(row: HolidayDto) {
  await ElMessageBox.confirm('¿Quitar este ajuste?', 'Confirmar', { type: 'warning' })
  try {
    await holidaysApi.removeHolidayOverride(row.date)
    ElMessage.success('Ajuste eliminado.')
    await load()
  } catch (err: any) {
    console.error('No se pudo quitar el ajuste:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo quitar el ajuste.')
  }
}

onMounted(load)
useRealtimeUpdates(['Holiday'], () => load(true))
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Festivos</h1>
      <div class="page-header-actions">
        <el-select v-model="year" style="width: 120px" @change="load">
          <el-option v-for="y in years" :key="y" :label="String(y)" :value="y" />
        </el-select>
        <el-button v-if="canEdit" type="primary" @click="openDialog">Agregar día no laborable</el-button>
      </div>
    </div>
    <p class="hint">
      Festivos nacionales de Colombia (calculados: incluyen los trasladados al lunes y los de Semana Santa) — {{ legalCount }}
      en {{ year }}. En estos días, y en fines de semana fuera del horario del técnico, no cuentan horas de SLA para
      clientes en horario de oficina ni se asignan técnicos. Los clientes 24/7 no se ven afectados.
    </p>

    <el-table :data="holidays" v-loading="loading" stripe empty-text="Sin festivos.">
      <el-table-column label="Fecha" width="280">
        <template #default="{ row }">{{ formatDate(row.date) }}</template>
      </el-table-column>
      <el-table-column prop="name" label="Nombre" />
      <el-table-column label="Origen" width="200">
        <template #default="{ row }">
          <el-tag v-if="row.source === 'custom'" type="warning" size="small">Cierre de la empresa</el-tag>
          <el-tag v-else-if="row.isWorkingDay" type="success" size="small">Legal — trabajado</el-tag>
          <el-tag v-else type="info" size="small">Legal</el-tag>
        </template>
      </el-table-column>
      <el-table-column v-if="canEdit" label="" width="200">
        <template #default="{ row }">
          <el-button v-if="row.source === 'custom' || row.isWorkingDay" link type="danger" @click="removeOverride(row)">
            Quitar ajuste
          </el-button>
          <el-button v-else link @click="markWorking(row)">Marcar laborable</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Agregar día no laborable" width="420px">
      <el-form label-position="top">
        <el-form-item label="Fecha">
          <el-date-picker v-model="form.date" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="Nombre">
          <el-input v-model="form.name" maxlength="120" placeholder="Ej. Cierre de fin de año" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" :disabled="!form.date || !form.name.trim()" @click="saveClosure">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.page-header-actions {
  display: flex;
  gap: 0.5rem;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}
</style>
