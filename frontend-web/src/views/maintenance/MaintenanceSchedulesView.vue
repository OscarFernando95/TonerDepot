<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as schedulesApi from '../../api/maintenanceSchedules'
import * as contractsApi from '../../api/contracts'
import { useAuthStore } from '../../stores/auth'
import { RoleNames, type ContractDto, type MaintenanceScheduleDto } from '../../api/types'

const auth = useAuthStore()
const canEvaluateNow = auth.hasRole(RoleNames.Administrador)

const schedules = ref<MaintenanceScheduleDto[]>([])
const contracts = ref<ContractDto[]>([])
const loading = ref(false)
const evaluating = ref(false)
const backfilling = ref(false)

const viewMode = ref<'grouped' | 'flat'>('grouped')
const filters = reactive({ cityName: '', clientId: '', contractId: '' })

type FilterKey = keyof typeof filters

function matches(s: MaintenanceScheduleDto, exclude: FilterKey) {
  const cityOk = exclude === 'cityName' || !filters.cityName || s.cityName === filters.cityName
  const clientOk = exclude === 'clientId' || !filters.clientId || s.clientId === filters.clientId
  const contractOk = exclude === 'contractId' || !filters.contractId || s.contractId === filters.contractId
  return cityOk && clientOk && contractOk
}

const cityOptions = computed(() =>
  [...new Set(schedules.value.filter((s) => matches(s, 'cityName') && s.cityName).map((s) => s.cityName as string))].sort()
)
const clientOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const s of schedules.value.filter((x) => matches(x, 'clientId'))) {
    seen.set(s.clientId, s.clientName)
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
})
const contractLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const c of contracts.value) {
    const start = new Date(c.startDate).toLocaleDateString(undefined, { timeZone: 'UTC' })
    const end = c.endDate ? new Date(c.endDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : 'indefinida'
    map.set(c.id, `${c.clientName} (${start} – ${end})`)
  }
  return map
})
const contractOptions = computed(() => {
  const seen = new Set<string>()
  for (const s of schedules.value.filter((x) => matches(x, 'contractId'))) {
    seen.add(s.contractId)
  }
  return [...seen].map((id) => ({ id, label: contractLabelById.value.get(id) ?? id })).sort((a, b) => a.label.localeCompare(b.label))
})

const filteredSchedules = computed(() =>
  schedules.value.filter((s) => matches(s, 'cityName') && matches(s, 'clientId') && matches(s, 'contractId'))
)

const emptySchedulesText = computed(() =>
  filters.cityName || filters.clientId || filters.contractId
    ? 'No hay cronogramas que coincidan con los filtros.'
    : 'No hay cronogramas registrados.'
)

const NO_CITY = 'Sin ciudad'

interface ContractGroup {
  contractId: string
  contractLabel: string
  schedules: MaintenanceScheduleDto[]
}
interface ClientGroup {
  clientId: string
  clientName: string
  contractGroups: ContractGroup[]
}
interface CityGroup {
  city: string
  clientGroups: ClientGroup[]
}

const groupedByCity = computed<CityGroup[]>(() => {
  const byCity = new Map<string, Map<string, Map<string, ContractGroup>>>()
  const clientNames = new Map<string, string>()
  for (const schedule of filteredSchedules.value) {
    const city = schedule.cityName ?? NO_CITY
    clientNames.set(schedule.clientId, schedule.clientName)
    if (!byCity.has(city)) byCity.set(city, new Map())
    const byClient = byCity.get(city)!
    if (!byClient.has(schedule.clientId)) byClient.set(schedule.clientId, new Map())
    const byContract = byClient.get(schedule.clientId)!
    if (!byContract.has(schedule.contractId)) {
      byContract.set(schedule.contractId, {
        contractId: schedule.contractId,
        contractLabel: contractLabelById.value.get(schedule.contractId) ?? schedule.contractId,
        schedules: []
      })
    }
    byContract.get(schedule.contractId)!.schedules.push(schedule)
  }

  return [...byCity.entries()]
    .map(([city, byClient]) => ({
      city,
      clientGroups: [...byClient.entries()]
        .map(([clientId, byContract]) => ({
          clientId,
          clientName: clientNames.get(clientId) ?? clientId,
          contractGroups: [...byContract.values()].sort((a, b) => a.contractLabel.localeCompare(b.contractLabel))
        }))
        .sort((a, b) => a.clientName.localeCompare(b.clientName))
    }))
    .sort((a, b) => a.city.localeCompare(b.city))
})

const openGroups = ref<string[]>([])
const groupsInitialized = ref(false)

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
  return `${new Date(at).toLocaleDateString()} / ${counter ?? '—'}`
}

async function loadData() {
  loading.value = true
  try {
    const [schedulesRes, contractsRes] = await Promise.all([
      schedulesApi.listMaintenanceSchedules(),
      contractsApi.listContracts()
    ])
    schedules.value = schedulesRes.data
    contracts.value = contractsRes.data

    if (!groupsInitialized.value) {
      openGroups.value = []
      groupsInitialized.value = true
    }
  } finally {
    loading.value = false
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
        : 'Ningún cronograma está por vencer todavía.'
    )
    await loadData()
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
  } finally {
    backfilling.value = false
  }
}

onMounted(loadData)
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
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 200px">
        <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Filtrar por cliente" style="width: 220px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.contractId" clearable filterable placeholder="Filtrar por contrato" style="width: 240px">
        <el-option v-for="c in contractOptions" :key="c.id" :label="c.label" :value="c.id" />
      </el-select>
      <el-radio-group v-model="viewMode" class="view-toggle">
        <el-radio-button value="grouped">Agrupar por ciudad</el-radio-button>
        <el-radio-button value="flat">Ver como lista</el-radio-button>
      </el-radio-group>
    </div>

    <el-table
      v-if="viewMode === 'flat'"
      :data="filteredSchedules"
      v-loading="loading"
      stripe
      :row-class-name="rowUrgencyClass"
      :empty-text="emptySchedulesText"
    >
      <el-table-column
        label="Activo"
        sortable
        :sort-method="
          (a: MaintenanceScheduleDto, b: MaintenanceScheduleDto) =>
            `${a.assetBrandName} ${a.assetModel}`.localeCompare(`${b.assetBrandName} ${b.assetModel}`)
        "
      >
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="130" sortable>
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="130" sortable />
      <el-table-column prop="clientName" label="Cliente" sortable />
      <el-table-column prop="lastKnownCounter" label="Último contador" width="130" sortable>
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
              <span>{{ new Date(row.lastMaintenanceAt).toLocaleDateString() }}</span>
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
              <span>{{ row.nextMaintenanceAt ? new Date(row.nextMaintenanceAt).toLocaleDateString() : 'Por contador' }} / {{ row.nextMaintenanceCounter }}</span>
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
      <el-table-column label="Estado" width="100" prop="isActive" sortable>
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

    <el-collapse v-else v-model="openGroups" v-loading="loading">
      <el-collapse-item v-for="cityGroup in groupedByCity" :key="cityGroup.city" :name="cityGroup.city">
        <template #title>
          {{ cityGroup.city }} ({{ cityGroup.clientGroups.reduce((n, g) => n + g.contractGroups.reduce((m, c) => m + c.schedules.length, 0), 0) }})
        </template>
        <el-collapse v-model="openGroups" class="nested-collapse">
          <el-collapse-item
            v-for="clientGroup in cityGroup.clientGroups"
            :key="clientGroup.clientId"
            :name="`${cityGroup.city}::${clientGroup.clientId}`"
          >
            <template #title>
              {{ clientGroup.clientName }} ({{ clientGroup.contractGroups.reduce((n, c) => n + c.schedules.length, 0) }})
            </template>
            <el-collapse v-model="openGroups" class="nested-collapse">
              <el-collapse-item
                v-for="contractGroup in clientGroup.contractGroups"
                :key="contractGroup.contractId"
                :name="`${cityGroup.city}::${clientGroup.clientId}::${contractGroup.contractId}`"
              >
                <template #title>{{ contractGroup.contractLabel }} ({{ contractGroup.schedules.length }})</template>
                <el-table :data="contractGroup.schedules" :row-class-name="rowUrgencyClass">
                  <el-table-column label="Activo">
                    <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
                  </el-table-column>
                  <el-table-column label="Área" width="130">
                    <template #default="{ row }">{{ row.area ?? '—' }}</template>
                  </el-table-column>
                  <el-table-column label="Último contador" width="130">
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
                          <span>{{ new Date(row.lastMaintenanceAt).toLocaleDateString() }}</span>
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
                          <span>{{ row.nextMaintenanceAt ? new Date(row.nextMaintenanceAt).toLocaleDateString() : 'Por contador' }} / {{ row.nextMaintenanceCounter }}</span>
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
                  <el-table-column label="Estado" width="100">
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
              </el-collapse-item>
            </el-collapse>
          </el-collapse-item>
        </el-collapse>
      </el-collapse-item>
    </el-collapse>
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
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.view-toggle {
  margin-left: auto;
}

.nested-collapse {
  margin-left: 1.5rem;
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
