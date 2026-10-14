<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
import * as citiesApi from '../../api/cities'
import * as clientsApi from '../../api/clients'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as schedulesApi from '../../api/maintenanceSchedules'
import * as contractsApi from '../../api/contracts'
import { useAuthStore } from '../../stores/auth'
import { RoleNames, type CityDto, type ClientDto, type ContractDto, type MaintenanceScheduleDto } from '../../api/types'
import { formatDateLocal, formatDateUTC } from '../../utils/date'

const auth = useAuthStore()
const canEvaluateNow = auth.hasRole(RoleNames.Administrador)

const evaluating = ref(false)
const backfilling = ref(false)

// Listado paginado y filtrado EN EL SERVIDOR (AUDIT.md H-11): antes se traían hasta 200 cronogramas, se filtraban y
// agrupaban en el cliente y pasado ese tope la lista quedaba incompleta sin avisar.
const PAGE_SIZE = 25
const filters = reactive({ search: '', cityId: '', clientId: '', contractId: '', isActive: '' as '' | 'true' | 'false' })
const {
  items: schedules,
  total,
  page,
  loading,
  reload: loadData
} = usePagedList<MaintenanceScheduleDto, typeof filters>(
  (params) =>
    schedulesApi
      .listMaintenanceSchedulesPaged({ ...params, isActive: params.isActive === '' ? undefined : params.isActive === 'true' })
      .then((r) => r.data),
  {
    filters,
    pageSize: PAGE_SIZE,
    label: 'MaintenanceSchedulesView',
    onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los cronogramas.')
  }
)

// La búsqueda por texto espera a que se deje de escribir: sin esto cada tecla sería una petición.
const searchText = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(searchText, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (filters.search = value.trim()), 350)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

// Opciones de filtro desde catálogos (ya no se derivan de la lista cargada).
const cityOptions = ref<CityDto[]>([])
const clientOptions = ref<ClientDto[]>([])
const contracts = ref<ContractDto[]>([])
citiesApi.listCities().then(({ data }) => (cityOptions.value = data))
  .catch((err) => console.error('MaintenanceSchedulesView: no se pudieron cargar las ciudades', err))
clientsApi.listClients().then(({ data }) => (clientOptions.value = data))
  .catch((err) => console.error('MaintenanceSchedulesView: no se pudieron cargar los clientes', err))
contractsApi.listContracts().then(({ data }) => (contracts.value = data))
  .catch((err) => console.error('MaintenanceSchedulesView: no se pudieron cargar los contratos', err))

const contractOptions = computed(() =>
  contracts.value
    .filter((c) => !filters.clientId || c.clientId === filters.clientId)
    .map((c) => ({
      id: c.id,
      label: `${c.clientName} (${formatDateUTC(c.startDate)} – ${c.endDate ? formatDateUTC(c.endDate) : 'indefinida'})`
    }))
    .sort((x, y) => x.label.localeCompare(y.label))
)

const hasFilters = computed(() => !!(filters.search || filters.cityId || filters.clientId || filters.contractId || filters.isActive))
const emptySchedulesText = computed(() =>
  hasFilters.value ? 'No hay cronogramas que coincidan con los filtros.' : 'No hay cronogramas registrados.'
)

function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { search: '', cityId: '', clientId: '', contractId: '', isActive: '' })
}

function printsRemaining(schedule: MaintenanceScheduleDto): number | null {
  if (schedule.lastKnownCounter == null) return null
  return schedule.nextMaintenanceCounter - schedule.lastKnownCounter
}
function daysRemaining(schedule: MaintenanceScheduleDto): number | null {
  if (!schedule.nextMaintenanceAt) return null
  return Math.ceil((new Date(schedule.nextMaintenanceAt).getTime() - Date.now()) / (1000 * 60 * 60 * 24))
}

type Urgency = 'far' | 'soon' | 'urgent' | 'overdue'

// El disparo real es "lo que ocurra primero" (días O impresiones), así que "cerca" alcanza con estar
// cerca en UNA medida, pero "lejos" exige estar lejos en AMBAS. Los cortes de 15 días/5.000 impresiones
// son la misma ventana de anticipación que ya usa el motor para generar la orden real (por eso "urgent"
// = orden ya generada o a punto); 30 días/15.000 impresiones es el aviso previo.
function urgency(schedule: MaintenanceScheduleDto): Urgency {
  const days = daysRemaining(schedule)
  const prints = printsRemaining(schedule)

  if ((days !== null && days <= 0) || (prints !== null && prints <= 0)) return 'overdue'
  if ((days !== null && days <= 15) || (prints !== null && prints <= 5000)) return 'urgent'
  if ((days !== null && days <= 30) || (prints !== null && prints <= 15000)) return 'soon'
  return 'far'
}

const urgencyColors: Record<Urgency, string> = {
  far: '#67c23a',
  soon: '#eab308',
  urgent: '#f97316',
  overdue: '#f56c6c'
}
const urgencyLabels: Record<Urgency, string> = {
  far: 'Lejano',
  soon: 'Próximo',
  urgent: 'Muy próximo',
  overdue: 'Vencido'
}

function urgencyTagStyle(schedule: MaintenanceScheduleDto) {
  return { backgroundColor: urgencyColors[urgency(schedule)], color: '#fff', border: 'none' }
}

function rowUrgencyClass({ row }: { row: MaintenanceScheduleDto }) {
  return `urgency-row-${urgency(row)}`
}

function counterDetail(at: string | null, counter: number | null | undefined): string {
  if (!at) return '—'
  return `${formatDateLocal(at)} / ${counter ?? '—'}`
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
        : 'Ningún cronograma está por vencer todavía.'
    )
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo evaluar los cronogramas.')
  } finally {
    evaluating.value = false
  }
}

async function backfill() {
  backfilling.value = true
  try {
    const { data } = await schedulesApi.backfillMaintenanceSchedules()
    ElMessage.success(
      data.created > 0 ? `Se crearon ${data.created} cronograma(s) nuevo(s).` : 'No había activos instalados sin cronograma.'
    )
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo completar el backfill de cronogramas.')
  } finally {
    backfilling.value = false
  }
}

useRealtimeUpdates(['Schedule'], () => loadData())
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Cronogramas de mantenimiento</h1>
      <div class="page-header-actions">
        <el-button :loading="backfilling" @click="backfill">Regenerar cronogramas faltantes</el-button>
        <el-button v-if="canEvaluateNow" :loading="evaluating" @click="evaluateNow">Evaluar ahora</el-button>
      </div>
    </div>
    <p class="hint">
      El cronograma de cada activo se crea automáticamente al instalarse bajo un contrato, y se recalcula
      con cada lectura de contador. Un job diario evalúa además el disparador por tiempo; "Evaluar ahora"
      dispara la misma evaluación sin esperar al cron.
    </p>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por serie, modelo, cliente, sede o ciudad" style="width: 300px" />
      <el-select v-model="filters.cityId" clearable filterable placeholder="Ciudad" style="width: 190px">
        <el-option v-for="c in cityOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Cliente" style="width: 210px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.contractId" clearable filterable placeholder="Contrato" style="width: 240px">
        <el-option v-for="c in contractOptions" :key="c.id" :label="c.label" :value="c.id" />
      </el-select>
      <el-select v-model="filters.isActive" clearable placeholder="Estado" style="width: 130px">
        <el-option label="Activo" value="true" />
        <el-option label="Pausado" value="false" />
      </el-select>
      <el-button v-if="hasFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <!-- Sin columnas ordenables: con paginación del servidor solo reordenarían la página visible. -->
    <el-table
      :data="schedules"
      v-loading="loading"
      stripe
      :row-class-name="rowUrgencyClass"
      :empty-text="emptySchedulesText"
    >
      <el-table-column
        label="Activo"
      >
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="130">
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="130" />
      <el-table-column prop="clientName" label="Cliente" />
      <el-table-column prop="lastKnownCounter" label="Último contador" width="130">
        <template #default="{ row }">{{ row.lastKnownCounter ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Último mantenimiento" width="190">
        <template #default="{ row }">
          <el-tooltip v-if="row.lastMaintenanceAt" placement="top">
            <template #content>
              <div>General: {{ counterDetail(row.lastGeneralMaintenanceAt, row.lastGeneralMaintenanceCounter) }}</div>
              <div>Unidades: {{ counterDetail(row.lastUnitsMaintenanceAt, row.lastUnitsMaintenanceCounter) }}</div>
              <div>Insumos: {{ counterDetail(row.lastConsumablesChangeAt, row.lastConsumablesChangeCounter) }}</div>
            </template>
            <div class="maintenance-cell">
              <span>{{ formatDateLocal(row.lastMaintenanceAt) }}</span>
              <el-tag v-for="code in row.lastMaintenanceCodes" :key="code" size="small">{{ code }}</el-tag>
            </div>
          </el-tooltip>
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column label="Próximo mantenimiento" width="210">
        <template #default="{ row }">
          <el-tooltip placement="top">
            <template #content>
              <div>General: {{ counterDetail(row.nextGeneralDueAt, row.nextGeneralDueCounter) }}</div>
              <div>Unidades: {{ counterDetail(row.nextUnitsDueAt, row.nextUnitsDueCounter) }}</div>
              <div>Insumos: contador {{ row.nextConsumablesDueCounter }}</div>
            </template>
            <div class="maintenance-cell">
              <span>{{ row.nextMaintenanceAt ? formatDateLocal(row.nextMaintenanceAt) : 'Por contador' }} / {{ row.nextMaintenanceCounter }}</span>
              <el-tag v-for="code in row.nextMaintenanceCodes" :key="code" size="small" type="warning">{{ code }}</el-tag>
            </div>
          </el-tooltip>
        </template>
      </el-table-column>
      <el-table-column label="Faltante" width="180">
        <template #default="{ row }">
          <div class="faltante-cell">
            <el-tag :style="urgencyTagStyle(row)" size="small">{{ urgencyLabels[urgency(row)] }}</el-tag>
            <span v-if="printsRemaining(row) !== null">{{ printsRemaining(row) }} impr.</span>
            <span v-if="daysRemaining(row) !== null">{{ daysRemaining(row) }} días</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="Estado" width="100" prop="isActive">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? 'Activo' : 'Pausado' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="100">
        <template #default="{ row }">
          <el-button link @click="toggleStatus(row)">{{ row.isActive ? 'Pausar' : 'Activar' }}</el-button>
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

.maintenance-cell {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.faltante-cell {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  font-size: 0.85rem;
}

:deep(tr.urgency-row-soon td.el-table__cell) {
  background-color: rgba(234, 179, 8, 0.08);
}

:deep(tr.urgency-row-urgent td.el-table__cell) {
  background-color: rgba(249, 115, 22, 0.1);
}

:deep(tr.urgency-row-overdue td.el-table__cell) {
  background-color: rgba(245, 108, 108, 0.12);
}
</style>
