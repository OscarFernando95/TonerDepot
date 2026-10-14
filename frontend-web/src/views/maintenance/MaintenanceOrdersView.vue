<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as ordersApi from '../../api/maintenanceOrders'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto } from '../../api/types'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
import * as clientsApi from '../../api/clients'
import type { ClientDto } from '../../api/types'
import { useStatusTag } from '../../composables/useStatusTag'

const router = useRouter()


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

// Listado paginado y filtrado EN EL SERVIDOR (AUDIT.md H-11): antes se traían hasta 200 órdenes de golpe.
const PAGE_SIZE = 25
const filters = reactive({ status: '', clientId: '', search: '' })
const {
  items: orders,
  total,
  page,
  loading,
  reload: loadOrders
} = usePagedList<MaintenanceOrderDto, typeof filters>((params) => ordersApi.listMaintenanceOrdersPaged(params).then((r) => r.data), {
  filters,
  pageSize: PAGE_SIZE,
  label: 'MaintenanceOrdersView',
  onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las órdenes.')
})

// La búsqueda por texto espera a que se deje de escribir: sin esto cada tecla sería una petición.
const searchText = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(searchText, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (filters.search = value.trim()), 350)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

const statusOptions = Object.entries(MaintenanceOrderStatusLabels).map(([value, label]) => ({ value, label }))
const hasFilters = computed(() => !!(filters.status || filters.clientId || filters.search))
const clientFilterOptions = ref<ClientDto[]>([])
clientsApi
  .listClients()
  .then(({ data }) => (clientFilterOptions.value = data))
  .catch((err) => console.error('MaintenanceOrdersView: no se pudieron cargar los clientes del filtro', err))

function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { status: '', clientId: '', search: '' })
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

const { tagType: statusTagType } = useStatusTag('maintenanceOrder')

function goToDetail(order: MaintenanceOrderDto) {
  router.push({ name: 'maintenance-order-detail', params: { id: order.id } })
}

useRealtimeUpdates(['MaintenanceOrder'], () => loadOrders())
</script>

<template>
  <div>
    <h1>Órdenes de mantenimiento</h1>
    <p class="hint">Generadas automáticamente por el cronograma, con anticipación, a medida que se registran contadores.</p>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por serie, modelo o marca" style="width: 300px" />
      <el-select v-model="filters.status" clearable placeholder="Estado" style="width: 170px">
        <el-option v-for="o in statusOptions" :key="o.value" :label="o.label" :value="o.value" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Cliente" style="width: 220px">
        <el-option v-for="c in clientFilterOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-button v-if="hasFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <!-- Sin columnas ordenables: con paginación del servidor solo reordenarían la página visible. -->
    <el-table
      :data="orders"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="hasFilters ? 'Ninguna orden coincide con los filtros.' : 'No hay órdenes de mantenimiento registradas.'"
    >
      <el-table-column
        label="Activo"
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
      <el-table-column prop="technicianName" label="Técnico" width="150">
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="scheduledDate" label="Programada" width="150">
        <template #default="{ row }">{{ new Date(row.scheduledDate).toLocaleDateString() }}</template>
      </el-table-column>
      <el-table-column prop="status" label="Estado" width="130">
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

    <el-pagination
      v-if="total > PAGE_SIZE"
      v-model:current-page="page"
      class="pager"
      layout="prev, pager, next"
      :page-size="PAGE_SIZE"
      :total="total"
      background
    />

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
.filters-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.results-count {
  margin-left: auto;
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.pager {
  margin-top: 1rem;
  justify-content: flex-end;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
