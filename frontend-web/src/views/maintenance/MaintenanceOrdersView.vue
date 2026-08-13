<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as ordersApi from '../../api/maintenanceOrders'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto } from '../../api/types'

const router = useRouter()

const orders = ref<MaintenanceOrderDto[]>([])
const loading = ref(false)

async function loadOrders() {
  loading.value = true
  try {
    const { data } = await ordersApi.listMaintenanceOrders()
    orders.value = data
  } finally {
    loading.value = false
  }
}

async function complete(order: MaintenanceOrderDto) {
  await ElMessageBox.confirm(
    `¿Marcar como completada la orden de ${order.assetBrandName} ${order.assetModel} (${order.assetSerialNumber})?`,
    'Confirmar',
    { type: 'warning' }
  )
  await ordersApi.completeMaintenanceOrder(order.id)
  ElMessage.success('Orden completada. El cronograma recalculó su próximo vencimiento.')
  await loadOrders()
}

async function cancel(order: MaintenanceOrderDto) {
  await ElMessageBox.confirm(
    `¿Cancelar la orden de ${order.assetBrandName} ${order.assetModel} (${order.assetSerialNumber})?`,
    'Confirmar',
    { type: 'warning' }
  )
  await ordersApi.cancelMaintenanceOrder(order.id)
  ElMessage.success('Orden cancelada.')
  await loadOrders()
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

function goToDetail(order: MaintenanceOrderDto) {
  router.push({ name: 'maintenance-order-detail', params: { id: order.id } })
}

onMounted(loadOrders)
</script>

<template>
  <div>
    <h1>Órdenes de mantenimiento</h1>
    <p class="hint">Generadas automáticamente por el cronograma, o manualmente vía "Evaluar ahora".</p>

    <el-table :data="orders" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
      <el-table-column label="Activo">
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
      </el-table-column>
      <el-table-column label="Técnico" width="150">
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Programada" width="150">
        <template #default="{ row }">{{ new Date(row.scheduledDate).toLocaleDateString() }}</template>
      </el-table-column>
      <el-table-column label="Estado" width="130">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">
            {{ MaintenanceOrderStatusLabels[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="200">
        <template #default="{ row }">
          <template v-if="row.status === 'Pendiente' || row.status === 'Asignada'">
            <el-button link @click.stop="complete(row)">Completar</el-button>
            <el-button link @click.stop="cancel(row)">Cancelar</el-button>
          </template>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.hint {
  color: #6b7280;
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
