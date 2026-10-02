<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as api from '../../api/tonerAnalytics'
import * as zonesApi from '../../api/zones'
import * as clientsApi from '../../api/clients'
import * as brandsApi from '../../api/assetBrands'
import * as modelsApi from '../../api/assetModels'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import MonthlyBarChart from '../../components/analytics/MonthlyBarChart.vue'
import type { AssetBrandDto, AssetModelDto, ClientDto } from '../../api/types'
import type { ZoneDto } from '../../api/zones'

// BI de consumo de tóner (solo Administrador). Sin precios ni costos por decisión de negocio: mide unidades y duración.
const zones = ref<ZoneDto[]>([])
const clients = ref<ClientDto[]>([])
const brands = ref<AssetBrandDto[]>([])
const models = ref<AssetModelDto[]>([])

function isoDay(d: Date) {
  return d.toISOString().slice(0, 10)
}
const today = new Date()
const sixMonthsAgo = new Date(today.getFullYear(), today.getMonth() - 6, today.getDate())
const filter = reactive({ range: [isoDay(sixMonthsAgo), isoDay(today)] as [string, string], zoneId: '', clientId: '', brandId: '', modelId: '' })

const summary = ref<api.TonerSummary | null>(null)
const machines = ref<api.TonerMachineRow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 15
const loading = ref(false)
const exporting = ref(false)
const tab = ref('machines')

const apiFilter = computed<api.TonerFilter>(() => ({
  // Hasta el final del día elegido, para no dejar fuera los registros de hoy.
  from: filter.range?.[0] ? `${filter.range[0]}T00:00:00` : undefined,
  to: filter.range?.[1] ? `${filter.range[1]}T23:59:59` : undefined,
  zoneId: filter.zoneId || undefined,
  clientId: filter.clientId || undefined,
  brandId: filter.brandId || undefined,
  modelId: filter.modelId || undefined
}))

const monthLabels = new Intl.DateTimeFormat('es-CO', { month: 'short', year: '2-digit' })
const chartData = computed(() =>
  (summary.value?.monthly ?? []).map((m) => {
    const [y, mo] = m.month.split('-').map(Number)
    return { label: monthLabels.format(new Date(y, mo - 1, 1)), value: m.units }
  })
)

const numberFormat = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 })
const dateFormat = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const fmt = (n: number | null | undefined) => (n == null ? '—' : numberFormat.format(n))

// 100 = rinde igual que el promedio del modelo. Menos de 85: gasta tóner de más (revisar la máquina); más de 115: rinde más.
function vsModelType(v: number | null): 'success' | 'danger' | 'info' {
  if (v == null) return 'info'
  if (v < 85) return 'danger'
  return v > 115 ? 'success' : 'info'
}

async function loadSummary() {
  const { data } = await api.getSummary(apiFilter.value)
  summary.value = data
}

async function loadMachines() {
  const { data } = await api.listMachines(apiFilter.value, page.value, pageSize)
  machines.value = data.items
  total.value = data.totalCount ?? data.items.length
}

async function reload(silent = false) {
  if (!silent) loading.value = true
  try {
    await Promise.all([loadSummary(), loadMachines()])
  } catch (err: any) {
    console.error('TonerBiView.reload failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el BI de tóner.')
  } finally {
    loading.value = false
  }
}

async function exportCsv() {
  exporting.value = true
  try {
    const { data } = await api.exportCsv(apiFilter.value)
    const url = URL.createObjectURL(data)
    const link = document.createElement('a')
    link.href = url
    link.download = `toner-por-maquina-${isoDay(new Date())}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (err: any) {
    console.error('TonerBiView.exportCsv failed', err)
    ElMessage.error('No se pudo exportar el CSV.')
  } finally {
    exporting.value = false
  }
}

async function loadCatalogs() {
  try {
    const [z, c, b] = await Promise.all([zonesApi.listZones(), clientsApi.listClients(), brandsApi.listAssetBrands()])
    zones.value = z.data
    clients.value = c.data
    brands.value = b.data
  } catch (err) {
    console.error('TonerBiView.loadCatalogs failed', err)
  }
}

watch(() => filter.brandId, async (brandId) => {
  filter.modelId = ''
  models.value = []
  if (!brandId) return
  try {
    models.value = (await modelsApi.listAssetModels(brandId)).data
  } catch (err) {
    console.error('TonerBiView.loadModels failed', err)
  }
})

watch(apiFilter, () => {
  page.value = 1
  void reload()
})

onMounted(async () => {
  await loadCatalogs()
  await reload()
})

useRealtimeUpdates(['Inventory'], () => void reload(true))
</script>

<template>
  <div>
    <div class="page-header">
      <h1>BI de tóner</h1>
      <p class="muted">
        Cuánto tóner gasta cada máquina y cuánto dura. La duración se estima con el contador: lo que recorre entre una entrega
        y la siguiente del mismo tóner, dividido entre las unidades de la entrega anterior.
      </p>
    </div>

    <div class="filters">
      <el-date-picker v-model="filter.range" type="daterange" range-separator="a" start-placeholder="Desde" end-placeholder="Hasta" value-format="YYYY-MM-DD" :clearable="false" />
      <el-select v-model="filter.zoneId" clearable filterable placeholder="Todas las zonas" style="width: 190px">
        <el-option v-for="z in zones" :key="z.id" :label="z.name" :value="z.id" />
      </el-select>
      <el-select v-model="filter.clientId" clearable filterable placeholder="Todos los clientes" style="width: 210px">
        <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filter.brandId" clearable filterable placeholder="Toda marca" style="width: 150px">
        <el-option v-for="b in brands" :key="b.id" :label="b.name" :value="b.id" />
      </el-select>
      <el-select v-model="filter.modelId" clearable filterable :disabled="!filter.brandId" placeholder="Todo modelo" style="width: 160px">
        <el-option v-for="m in models" :key="m.id" :label="m.name" :value="m.id" />
      </el-select>
      <span class="spacer" />
      <el-button :loading="exporting" @click="exportCsv">Exportar CSV</el-button>
    </div>

    <div v-loading="loading">
      <div v-if="summary" class="kpis">
        <div class="kpi">
          <span class="kpi-label">Tóner usado</span>
          <span class="kpi-value">{{ fmt(summary.totalUnits) }}</span>
          <span class="kpi-note">{{ summary.changedByTechnicianUnits }} cambiados por técnico · {{ summary.deliveredToUserUnits }} entregados al usuario</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Máquinas con consumo</span>
          <span class="kpi-value">{{ fmt(summary.machines) }}</span>
          <span class="kpi-note">en el rango y filtros elegidos</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Páginas por tóner</span>
          <span class="kpi-value">{{ fmt(summary.avgPagesPerUnit) }}</span>
          <span class="kpi-note">promedio de duración medida</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Tóner con duración medida</span>
          <span class="kpi-value">{{ fmt(summary.measuredUnits) }}</span>
          <span class="kpi-note">de {{ fmt(summary.totalUnits) }} usados</span>
        </div>
      </div>

      <section v-if="summary" class="panel">
        <h3>Tóner usado por mes</h3>
        <MonthlyBarChart v-if="chartData.length" :data="chartData" unit="tóner" />
        <p v-else class="muted">No hay tóner registrado en este rango.</p>
      </section>

      <el-tabs v-model="tab" class="tabs">
        <el-tab-pane label="Por máquina" name="machines">
          <el-table :data="machines" stripe empty-text="Sin consumo de tóner en este rango.">
            <el-table-column label="Máquina" min-width="190">
              <template #default="{ row }">
                <div class="machine">{{ row.brand }} {{ row.model }}</div>
                <div class="sub">{{ row.serialNumber }}</div>
              </template>
            </el-table-column>
            <el-table-column label="Cliente / sede" min-width="190">
              <template #default="{ row }">
                <div>{{ row.clientName ?? '—' }}</div>
                <div class="sub">{{ row.locationName ?? '' }}<span v-if="row.zoneName"> · {{ row.zoneName }}</span></div>
              </template>
            </el-table-column>
            <el-table-column label="Tóner usado" width="130">
              <template #default="{ row }">
                <strong class="num">{{ row.totalUnits }}</strong>
                <div class="sub">{{ row.changedByTechnicianUnits }} téc. · {{ row.deliveredToUserUnits }} usuario</div>
              </template>
            </el-table-column>
            <el-table-column label="Páginas por tóner" width="150">
              <template #default="{ row }">
                <span class="num">{{ fmt(row.avgPagesPerUnit) }}</span>
                <div class="sub">{{ row.measuredUnits }} medidos</div>
              </template>
            </el-table-column>
            <el-table-column label="Páginas en el rango" width="140">
              <template #default="{ row }"><span class="num">{{ fmt(row.pagesInRange) }}</span></template>
            </el-table-column>
            <el-table-column width="160">
              <template #header>
                <el-tooltip content="100% = rinde igual que el promedio del mismo tóner en otras máquinas del modelo" placement="top">
                  <span>Vs. modelo ⓘ</span>
                </el-tooltip>
              </template>
              <template #default="{ row }">
                <el-tag v-if="row.vsModelPercent != null" :type="vsModelType(row.vsModelPercent)" size="small">{{ fmt(row.vsModelPercent) }}%</el-tag>
                <span v-else class="sub">sin comparación</span>
              </template>
            </el-table-column>
            <el-table-column label="Último registro" width="170">
              <template #default="{ row }">
                {{ row.lastEventAt ? dateFormat.format(new Date(row.lastEventAt)) : '—' }}
                <div class="sub">contador {{ fmt(row.lastCounter) }}</div>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination v-model:current-page="page" class="pager" layout="prev, pager, next" :page-size="pageSize" :total="total" @current-change="reload(true)" />
        </el-tab-pane>

        <el-tab-pane v-for="g in [{ k: 'byClient', l: 'Por cliente' }, { k: 'byZone', l: 'Por zona' }, { k: 'byModel', l: 'Por modelo' }]" :key="g.k" :label="g.l" :name="g.k">
          <el-table :data="summary ? (summary as any)[g.k] : []" stripe empty-text="Sin datos en este rango.">
            <el-table-column prop="name" :label="g.l.replace('Por ', '')" min-width="220" />
            <el-table-column prop="machines" label="Máquinas" width="120" />
            <el-table-column prop="totalUnits" label="Tóner usado" width="140" />
            <el-table-column label="Páginas por tóner" width="170"><template #default="{ row }">{{ fmt(row.avgPagesPerUnit) }}</template></el-table-column>
          </el-table>
        </el-tab-pane>
      </el-tabs>
    </div>
  </div>
</template>

<style scoped>
.page-header h1 { margin: 0 0 0.25rem; }
.page-header p { margin: 0 0 1rem; max-width: 70ch; }
.filters { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; margin-bottom: 1.25rem; }
.spacer { flex: 1; }
.kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1px; background: var(--board-seam-soft); border: 1px solid var(--board-seam-soft); margin-bottom: 1.25rem; }
.kpi { display: flex; flex-direction: column; gap: 0.35rem; padding: 1rem 1.1rem; background: var(--board-panel); }
.kpi-label { font-size: 0.72rem; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: var(--flap-ink-dim); }
.kpi-value { font-size: 2rem; font-weight: 700; line-height: 1; color: var(--flap-ink); font-variant-numeric: tabular-nums; }
.kpi-note { font-size: 0.78rem; color: var(--flap-ink-dim); }
.panel { background: var(--board-panel); border: 1px solid var(--board-seam-soft); padding: 1rem 1.1rem; margin-bottom: 1.25rem; }
.panel h3 { margin: 0 0 0.5rem; font-size: 0.72rem; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: var(--flap-ink-dim); }
.muted { color: var(--flap-ink-dim); }
.machine { font-weight: 600; }
.sub { font-size: 0.75rem; color: var(--flap-ink-dim); }
.num { font-variant-numeric: tabular-nums; }
.pager { margin-top: 1rem; justify-content: flex-end; }
@media (max-width: 640px) { .filters > * { width: 100% !important; } }
</style>
