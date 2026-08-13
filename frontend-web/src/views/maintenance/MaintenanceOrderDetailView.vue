<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as ordersApi from '../../api/maintenanceOrders'
import * as techniciansApi from '../../api/technicians'
import { MaintenanceOrderStatusLabels, type AssignmentHistoryDto, type MaintenanceOrderDto } from '../../api/types'

const route = useRoute()
const orderId = route.params.id as string

const order = ref<MaintenanceOrderDto | null>(null)
const history = ref<AssignmentHistoryDto[]>([])
const technicians = ref<techniciansApi.TechnicianDto[]>([])
const loading = ref(false)

const assignDialogVisible = ref(false)
const savingAssign = ref(false)
const assignForm = reactive({ technicianId: '', reason: '' })

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

async function complete() {
  if (!order.value) return
  await ElMessageBox.confirm('¿Marcar esta orden como completada?', 'Confirmar', { type: 'warning' })
  const { data } = await ordersApi.completeMaintenanceOrder(orderId)
  order.value = data
  ElMessage.success('Orden completada. El cronograma recalculó su próximo vencimiento.')
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
</script>

<template>
  <div v-loading="loading">
    <template v-if="order">
      <div class="page-header">
        <h1>{{ order.assetBrandName }} {{ order.assetModel }} — {{ order.assetSerialNumber }}</h1>
        <el-tag :type="statusTagType(order.status)" size="large">
          {{ MaintenanceOrderStatusLabels[order.status] ?? order.status }}
        </el-tag>
      </div>

      <el-card class="section-card">
        <template #header>Detalle</template>
        <dl class="detail-grid">
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
            @click="complete"
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
  color: #6b7280;
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
