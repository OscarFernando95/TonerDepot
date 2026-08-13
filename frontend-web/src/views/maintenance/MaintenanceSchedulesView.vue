<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as schedulesApi from '../../api/maintenanceSchedules'
import * as assetsApi from '../../api/assets'
import { useAuthStore } from '../../stores/auth'
import { MaintenanceFrequencyTypes, RoleNames, type AssetDto, type MaintenanceScheduleDto } from '../../api/types'

const auth = useAuthStore()
const canEvaluateNow = auth.hasRole(RoleNames.Administrador)

const schedules = ref<MaintenanceScheduleDto[]>([])
const assets = ref<AssetDto[]>([])
const loading = ref(false)
const evaluating = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const frequencyOptions = Object.values(MaintenanceFrequencyTypes)

const form = reactive({
  assetId: '',
  frequencyType: MaintenanceFrequencyTypes.PorContador as string,
  printThreshold: undefined as number | undefined,
  timeIntervalDays: undefined as number | undefined
})

const isPorContador = computed(() => form.frequencyType === MaintenanceFrequencyTypes.PorContador)

async function loadData() {
  loading.value = true
  try {
    const [schedulesRes, assetsRes] = await Promise.all([
      schedulesApi.listMaintenanceSchedules(),
      assetsApi.listAssets()
    ])
    schedules.value = schedulesRes.data
    assets.value = assetsRes.data
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.assetId = ''
  form.frequencyType = MaintenanceFrequencyTypes.PorContador
  form.printThreshold = undefined
  form.timeIntervalDays = undefined
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await schedulesApi.createMaintenanceSchedule({
      assetId: form.assetId,
      frequencyType: form.frequencyType as any,
      printThreshold: isPorContador.value ? form.printThreshold ?? null : null,
      timeIntervalDays: !isPorContador.value ? form.timeIntervalDays ?? null : null
    })
    ElMessage.success('Cronograma creado.')
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el cronograma.')
  } finally {
    saving.value = false
  }
}

async function toggleStatus(schedule: MaintenanceScheduleDto) {
  const nextStatus = !schedule.isActive
  await ElMessageBox.confirm(
    `¿${nextStatus ? 'Activar' : 'Pausar'} el cronograma de ${schedule.assetBrandName} ${schedule.assetModel}?`,
    'Confirmar',
    { type: 'warning' }
  )
  await schedulesApi.setMaintenanceScheduleStatus(schedule.id, nextStatus)
  ElMessage.success('Cronograma actualizado.')
  await loadData()
}

async function evaluateNow() {
  evaluating.value = true
  try {
    const { data } = await schedulesApi.evaluateSchedulesNow()
    ElMessage.success(
      data.ordersCreated > 0
        ? `Se generaron ${data.ordersCreated} orden(es) de mantenimiento.`
        : 'Ningún cronograma está vencido todavía.'
    )
    await loadData()
  } finally {
    evaluating.value = false
  }
}

function dueLabel(schedule: MaintenanceScheduleDto) {
  if (schedule.frequencyType === 'PorContador') {
    return schedule.nextDueCounter !== null ? `Contador ≥ ${schedule.nextDueCounter}` : '—'
  }
  return schedule.nextDueAt ? new Date(schedule.nextDueAt).toLocaleDateString() : '—'
}

onMounted(loadData)
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Cronogramas de mantenimiento</h1>
      <div class="page-header-actions">
        <el-button v-if="canEvaluateNow" :loading="evaluating" @click="evaluateNow">Evaluar ahora</el-button>
        <el-button type="primary" @click="openCreateDialog" :disabled="assets.length === 0">Nuevo cronograma</el-button>
      </div>
    </div>
    <p class="hint">
      Un job automático (Hangfire) evalúa diariamente estos cronogramas y genera órdenes de mantenimiento
      cuando corresponde. El botón "Evaluar ahora" dispara la misma evaluación sin esperar al cron.
    </p>

    <el-table :data="schedules" v-loading="loading" stripe>
      <el-table-column label="Activo">
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
      </el-table-column>
      <el-table-column prop="frequencyType" label="Frecuencia" width="130" />
      <el-table-column label="Próximo vencimiento" width="180">
        <template #default="{ row }">{{ dueLabel(row) }}</template>
      </el-table-column>
      <el-table-column label="Última ejecución" width="160">
        <template #default="{ row }">
          {{ row.lastExecutedAt ? new Date(row.lastExecutedAt).toLocaleDateString() : '—' }}
        </template>
      </el-table-column>
      <el-table-column label="Estado" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? 'Activo' : 'Pausado' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="110">
        <template #default="{ row }">
          <el-button link @click="toggleStatus(row)">{{ row.isActive ? 'Pausar' : 'Activar' }}</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo cronograma" width="440px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Activo">
          <el-select v-model="form.assetId" style="width: 100%" filterable placeholder="Selecciona un activo">
            <el-option
              v-for="a in assets"
              :key="a.id"
              :label="`${a.assetBrandName} ${a.model} — ${a.serialNumber}`"
              :value="a.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="Frecuencia">
          <el-select v-model="form.frequencyType" style="width: 100%">
            <el-option v-for="f in frequencyOptions" :key="f" :label="f" :value="f" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="isPorContador" label="Cada cuántas impresiones">
          <el-input-number v-model="form.printThreshold" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item v-else label="Cada cuántos días">
          <el-input-number v-model="form.timeIntervalDays" :min="1" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">Guardar</el-button>
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
  color: #6b7280;
  font-size: 0.85rem;
  margin: 0 0 1rem;
}
</style>
