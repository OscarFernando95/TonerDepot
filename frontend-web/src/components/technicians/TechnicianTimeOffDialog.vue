<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import type { TechnicianDto, TimeOffDto } from '../../api/technicians'

const emit = defineEmits<{ (e: 'changed'): void }>()

const dateTimeFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const formatDateTime = (iso: string) => dateTimeFormatter.format(new Date(iso))

const visible = ref(false)
const loading = ref(false)
const saving = ref(false)
const technician = ref<TechnicianDto | null>(null)
const items = ref<TimeOffDto[]>([])
const range = ref<[Date, Date] | null>(null)
const reason = ref('')

async function load() {
  if (!technician.value) return
  loading.value = true
  try {
    const { data } = await techniciansApi.listTechnicianTimeOff(technician.value.id)
    items.value = data
  } catch (err: any) {
    console.error('No se pudieron cargar los períodos fuera de la oficina:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el listado.')
  } finally {
    loading.value = false
  }
}

async function open(target: TechnicianDto) {
  technician.value = target
  range.value = null
  reason.value = ''
  visible.value = true
  await load()
}

async function add() {
  if (!technician.value || !range.value) return
  saving.value = true
  try {
    await techniciansApi.addTechnicianTimeOff(
      technician.value.id,
      range.value[0].toISOString(),
      range.value[1].toISOString(),
      reason.value.trim() || null
    )
    ElMessage.success('Técnico marcado fuera de la oficina.')
    range.value = null
    reason.value = ''
    await load()
    emit('changed')
  } catch (err: any) {
    console.error('No se pudo marcar fuera de la oficina:', err)
    const errors = err.response?.data?.errors
    ElMessage.error((errors && Object.values(errors).flat().join(' ')) || err.response?.data?.title || 'No se pudo guardar.')
  } finally {
    saving.value = false
  }
}

async function cancel(item: TimeOffDto) {
  if (!technician.value) return
  await ElMessageBox.confirm(
    item.isActive ? 'El técnico vuelve a estar disponible desde ahora. ¿Continuar?' : '¿Anular este período fuera de la oficina?',
    'Confirmar',
    { type: 'warning' }
  )
  try {
    await techniciansApi.cancelTechnicianTimeOff(technician.value.id, item.id)
    ElMessage.success('Listo.')
    await load()
    emit('changed')
  } catch (err: any) {
    console.error('No se pudo cancelar el período:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cancelar.')
  }
}

function statusOf(item: TimeOffDto): { label: string; type: 'warning' | 'info' | 'success' } {
  if (item.cancelledAt && new Date(item.cancelledAt) <= new Date(item.startsAt)) return { label: 'Anulado', type: 'info' }
  if (item.isActive) return { label: 'En curso', type: 'warning' }
  if (new Date(item.startsAt) > new Date()) return { label: 'Programado', type: 'success' }
  return { label: item.cancelledAt ? 'Terminado antes' : 'Terminado', type: 'info' }
}

function canCancel(item: TimeOffDto) {
  return !item.cancelledAt && new Date(item.effectiveEndsAt) > new Date()
}

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" :title="`Fuera de la oficina — ${technician?.fullName ?? ''}`" width="640px">
    <p class="hint">
      Permisos, vacaciones, incapacidades… No es un retiro. Mientras dure, el técnico no recibe asignaciones y su
      tiempo no cuenta para el SLA de clientes en horario de oficina.
    </p>
    <div class="form-row">
      <el-date-picker
        v-model="range"
        type="datetimerange"
        start-placeholder="Inicio"
        end-placeholder="Fin"
        format="DD/MM/YYYY HH:mm"
        style="flex: 1"
      />
    </div>
    <div class="form-row">
      <el-input v-model="reason" maxlength="300" placeholder="Motivo (opcional): vacaciones, permiso, cita médica…" />
      <el-button type="primary" :loading="saving" :disabled="!range" @click="add">Marcar fuera</el-button>
    </div>

    <el-table :data="items" v-loading="loading" empty-text="Sin períodos registrados." size="small">
      <el-table-column label="Desde" width="150">
        <template #default="{ row }">{{ formatDateTime(row.startsAt) }}</template>
      </el-table-column>
      <el-table-column label="Hasta" width="150">
        <template #default="{ row }">{{ formatDateTime(row.effectiveEndsAt) }}</template>
      </el-table-column>
      <el-table-column prop="reason" label="Motivo" />
      <el-table-column label="Estado" width="120">
        <template #default="{ row }">
          <el-tag :type="statusOf(row).type" size="small">{{ statusOf(row).label }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="90">
        <template #default="{ row }">
          <el-button v-if="canCancel(row)" link type="danger" @click="cancel(row)">
            {{ row.isActive ? 'Terminar' : 'Anular' }}
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    <template #footer>
      <el-button @click="visible = false">Cerrar</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.form-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}
</style>
