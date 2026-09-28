<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as ordersApi from '../../api/maintenanceOrders'
import * as techniciansApi from '../../api/technicians'
import { MaintenanceOrderStatusLabels, type AssignmentHistoryDto, type MaintenanceOrderDto } from '../../api/types'
import EvidenceGallery from '../../components/EvidenceGallery.vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'

const route = useRoute()
const router = useRouter()
const orderId = route.params.id as string

const order = ref<MaintenanceOrderDto | null>(null)
const history = ref<AssignmentHistoryDto[]>([])
const technicians = ref<techniciansApi.TechnicianDto[]>([])
const loading = ref(false)

const assignDialogVisible = ref(false)
const savingAssign = ref(false)
const assignForm = reactive({ technicianId: '', reason: '' })

const completeDialogVisible = ref(false)
const completing = ref(false)
const completeForm = reactive({ counterValue: undefined as number | undefined, readingDate: '' })

function orderComboLabel(o: MaintenanceOrderDto) {
  const parts: string[] = []
  if (o.includesGeneral) parts.push('General')
  if (o.includesUnits) parts.push('Unidades')
  if (o.includesConsumables) parts.push('Insumos')
  return parts.join(' + ')
}

async function loadAll() {
  loading.value = true
  try {
    const [orderRes, historyRes, techRes] = await Promise.all([
      ordersApi.getMaintenanceOrder(orderId),
      ordersApi.getMaintenanceOrderAssignmentHistory(orderId),
      techniciansApi.listTechnicians()
    ])
    order.value = orderRes.data
    history.value = historyRes.data
    technicians.value = techRes.data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar la orden.')
  } finally {
    loading.value = false
  }
}

function openAssignDialog() {
  assignForm.technicianId = order.value?.technicianId ?? ''
  assignForm.reason = ''
  assignDialogVisible.value = true
}

async function saveAssign() {
  savingAssign.value = true
  try {
    const { data } = await ordersApi.assignMaintenanceOrder(orderId, assignForm.technicianId, assignForm.reason || null)
    order.value = data
    ElMessage.success('Orden asignada.')
    assignDialogVisible.value = false
    const { data: historyData } = await ordersApi.getMaintenanceOrderAssignmentHistory(orderId)
    history.value = historyData
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo asignar la orden.')
  } finally {
    savingAssign.value = false
  }
}

function openCompleteDialog() {
  completeForm.counterValue = undefined
  completeForm.readingDate = ''
  completeDialogVisible.value = true
}

async function confirmComplete() {
  if (!order.value || completeForm.counterValue === undefined) return
  completing.value = true
  try {
    const { data } = await ordersApi.completeMaintenanceOrder(orderId, {
      counterValue: completeForm.counterValue,
      readingDate: completeForm.readingDate ? new Date(completeForm.readingDate).toISOString() : null
    })
    order.value = data
    ElMessage.success('Orden completada. El cronograma recalculó su próximo vencimiento.')
    completeDialogVisible.value = false
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo completar la orden.')
  } finally {
    completing.value = false
  }
}

async function cancel() {
  if (!order.value) return
  await ElMessageBox.confirm('¿Cancelar esta orden?', 'Confirmar', { type: 'warning' })
  const { data } = await ordersApi.cancelMaintenanceOrder(orderId)
  order.value = data
  ElMessage.success('Orden cancelada.')
}

function statusTagType(status: string) {
  switch (status) {
    case 'Completada':
      return 'success'
    case 'Cancelada':
      return 'info'
    case 'Pendiente':
      return 'warning'
    default:
      return 'primary'
  }
}

onMounted(loadAll)

useRealtimeUpdates(['MaintenanceOrder'], (event) => {
  if (event.id === orderId) loadAll()
})
</script>

<template>
  <div v-loading="loading">
    <template v-if="order">
      <div class="page-header">
        <el-button :icon="ArrowLeft" circle title="Volver a Órdenes" @click="router.push({ name: 'maintenance-orders' })" />
        <h1>{{ order.assetBrandName }} {{ order.assetModel }} — {{ order.assetSerialNumber }}</h1>
        <el-tag :type="statusTagType(order.status)" size="large">
          {{ MaintenanceOrderStatusLabels[order.status] ?? order.status }}
        </el-tag>
      </div>

      <el-card class="section-card">
        <template #header>Detalle</template>
        <dl class="detail-grid">
          <dt>Tipo</dt>
          <dd>{{ orderComboLabel(order) }}</dd>
          <dt>Técnico asignado</dt>
          <dd>{{ order.technicianName ?? '—' }}</dd>
          <dt>Programada</dt>
          <dd>{{ new Date(order.scheduledDate).toLocaleString() }}</dd>
          <template v-if="order.completedAt">
            <dt>Completada</dt>
            <dd>{{ new Date(order.completedAt).toLocaleString() }}</dd>
          </template>
        </dl>
        <div class="section-actions">
          <el-button v-if="order.status === 'Pendiente' || order.status === 'Asignada'" @click="openAssignDialog">
            {{ order.technicianId ? 'Reasignar técnico' : 'Asignar técnico' }}
          </el-button>
          <el-button
            v-if="order.status === 'Pendiente' || order.status === 'Asignada'"
            type="primary"
            plain
            @click="openCompleteDialog"
          >
            Completar
          </el-button>
          <el-button
            v-if="order.status === 'Pendiente' || order.status === 'Asignada'"
            type="danger"
            plain
            @click="cancel"
          >
            Cancelar
          </el-button>
        </div>
      </el-card>

      <el-card class="section-card">
        <template #header>Evidencia fotográfica</template>
        <EvidenceGallery :order-id="orderId" />
      </el-card>

      <el-card class="section-card">
        <template #header>Historial de asignación</template>
        <el-table :data="history" stripe>
          <el-table-column prop="technicianName" label="Técnico">
            <template #default="{ row }">{{ row.technicianName ?? 'Sin asignar' }}</template>
          </el-table-column>
          <el-table-column label="Tipo" width="120">
            <template #default="{ row }">
              <el-tag size="small" :type="row.assignmentType === 'Manual' ? 'info' : 'primary'">
                {{ row.assignmentType }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="assignedByUserName" label="Asignado por" width="180">
            <template #default="{ row }">{{ row.assignedByUserName ?? 'Sistema (automático)' }}</template>
          </el-table-column>
          <el-table-column label="Fecha" width="180">
            <template #default="{ row }">{{ new Date(row.assignedAt).toLocaleString() }}</template>
          </el-table-column>
          <el-table-column prop="reason" label="Motivo" />
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="assignDialogVisible" title="Asignar técnico" width="420px">
      <el-form :model="assignForm" label-position="top">
        <el-form-item label="Técnico">
          <el-select v-model="assignForm.technicianId" style="width: 100%" filterable placeholder="Selecciona un técnico">
            <el-option v-for="t in technicians" :key="t.id" :label="t.fullName" :value="t.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Motivo (opcional)">
          <el-input v-model="assignForm.reason" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="assignDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingAssign" @click="saveAssign">Confirmar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="completeDialogVisible" title="Completar orden" width="420px">
      <el-form label-position="top">
        <el-form-item label="Contador">
          <el-input-number v-model="completeForm.counterValue" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="Fecha de la lectura (opcional, hoy por defecto)">
          <el-date-picker v-model="completeForm.readingDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="completeDialogVisible = false">Cancelar</el-button>
        <el-button
          type="primary"
          :loading="completing"
          :disabled="completeForm.counterValue === undefined"
          @click="confirmComplete"
        >
          Completar
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.section-card {
  margin-bottom: 1.5rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: 160px 1fr;
  row-gap: 0.5rem;
  margin: 0;
}

.detail-grid dt {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.detail-grid dd {
  margin: 0;
}

.section-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 1.25rem;
}
</style>
