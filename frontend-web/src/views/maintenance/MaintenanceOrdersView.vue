<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as ordersApi from '../../api/maintenanceOrders'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto } from '../../api/types'
import { useRealtimeUpdates } from '../../composables/useRealtime'

const router = useRouter()

const orders = ref<MaintenanceOrderDto[]>([])
const loading = ref(false)

const completeDialogVisible = ref(false)
const completing = ref(false)
const completingOrder = ref<MaintenanceOrderDto | null>(null)
const completeForm = reactive({ counterValue: undefined as number | undefined, readingDate: '' })

function orderComboLabel(order: MaintenanceOrderDto) {
  const parts: string[] = []
  if (order.includesGeneral) parts.push('General')
  if (order.includesUnits) parts.push('Unidades')
  if (order.includesConsumables) parts.push('Insumos')
  return parts.join(' + ')
}

async function loadOrders() {
  loading.value = true
  try {
    const { data } = await ordersApi.listMaintenanceOrders()
    orders.value = data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las órdenes.')
  } finally {
    loading.value = false
  }
}

function openCompleteDialog(order: MaintenanceOrderDto) {
  completingOrder.value = order
  completeForm.counterValue = undefined
  completeForm.readingDate = ''
  completeDialogVisible.value = true
}

async function confirmComplete() {
  if (!completingOrder.value || completeForm.counterValue === undefined) return
  completing.value = true
  try {
    await ordersApi.completeMaintenanceOrder(completingOrder.value.id, {
      counterValue: completeForm.counterValue,
      readingDate: completeForm.readingDate ? new Date(completeForm.readingDate).toISOString() : null
    })
    ElMessage.success('Orden completada. El cronograma recalculó su próximo vencimiento.')
    completeDialogVisible.value = false
    await loadOrders()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo completar la orden.')
  } finally {
    completing.value = false
  }
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

useRealtimeUpdates(['MaintenanceOrder'], () => loadOrders())
</script>

<template>
  <div>
    <h1>Órdenes de mantenimiento</h1>
    <p class="hint">Generadas automáticamente por el cronograma, con anticipación, a medida que se registran contadores.</p>

    <el-table
      :data="orders"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      empty-text="No hay órdenes de mantenimiento registradas."
    >
      <el-table-column
        label="Activo"
        sortable
        :sort-method="
          (a: MaintenanceOrderDto, b: MaintenanceOrderDto) =>
            `${a.assetBrandName} ${a.assetModel}`.localeCompare(`${b.assetBrandName} ${b.assetModel}`)
        "
      >
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
      </el-table-column>
      <el-table-column label="Tipo" width="190">
        <template #default="{ row }">
          {{ orderComboLabel(row) }}
          <el-tag v-if="row.isManual" size="small" type="warning" effect="plain" :title="row.reason ?? ''">Manual</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="technicianName" label="Técnico" width="150" sortable>
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="scheduledDate" label="Programada" width="150" sortable>
        <template #default="{ row }">{{ new Date(row.scheduledDate).toLocaleDateString() }}</template>
      </el-table-column>
      <el-table-column prop="status" label="Estado" width="130" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">
            {{ MaintenanceOrderStatusLabels[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="200">
        <template #default="{ row }">
          <template v-if="row.status === 'Pendiente' || row.status === 'Asignada'">
            <el-button link @click.stop="openCompleteDialog(row)">Completar</el-button>
            <el-button link @click.stop="cancel(row)">Cancelar</el-button>
          </template>
        </template>
      </el-table-column>
    </el-table>

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
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
