<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { ElMessage } from 'element-plus'
import * as meterReadingsApi from '../../api/meterReadings'
import type { MeterReadingAssetDto } from '../../api/types'
import TonerDialog from '../../components/inventory/TonerDialog.vue'
import { useAuthStore } from '../../stores/auth'
import { RoleNames } from '../../api/types'

const auth = useAuthStore()
// Para el técnico esta misma pantalla es "Mis máquinas": las que tiene vinculadas, con su contador y su tóner.
const isTechnician = computed(() => auth.hasRole(RoleNames.Tecnico))

const assets = ref<MeterReadingAssetDto[]>([])
const tonerDate = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
function lastTonerLabel(a: MeterReadingAssetDto) {
  if (!a.lastTonerAt) return 'Sin tóner registrado'
  const counter = a.lastTonerCounter != null ? ` · contador ${a.lastTonerCounter}` : ''
  return `${tonerDate.format(new Date(a.lastTonerAt))}${counter} · ${a.tonerUnitsLast90Days} en 90 días`
}
const loading = ref(false)

const viewMode = ref<'grouped' | 'flat'>('grouped')
const filters = reactive({ cityName: '', clientId: '' })

type FilterKey = keyof typeof filters

function matches(a: MeterReadingAssetDto, exclude: FilterKey) {
  const cityOk = exclude === 'cityName' || !filters.cityName || a.cityName === filters.cityName
  const clientOk = exclude === 'clientId' || !filters.clientId || a.clientId === filters.clientId
  return cityOk && clientOk
}

const cityOptions = computed(() =>
  [...new Set(assets.value.filter((a) => matches(a, 'cityName') && a.cityName).map((a) => a.cityName as string))].sort()
)
const clientOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const a of assets.value.filter((x) => matches(x, 'clientId') && x.clientId)) {
    seen.set(a.clientId as string, a.clientName as string)
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
})

const filteredAssets = computed(() => assets.value.filter((a) => matches(a, 'cityName') && matches(a, 'clientId')))

const NO_CITY = 'Sin ciudad'
const NO_CLIENT = 'Sin cliente'

interface ClientGroup {
  clientKey: string
  clientLabel: string
  assets: MeterReadingAssetDto[]
}
interface CityGroup {
  city: string
  clientGroups: ClientGroup[]
}

const groupedByCity = computed<CityGroup[]>(() => {
  const byCity = new Map<string, Map<string, MeterReadingAssetDto[]>>()
  const clientLabels = new Map<string, string>()

  for (const asset of filteredAssets.value) {
    const city = asset.cityName ?? NO_CITY
    const clientKey = asset.clientId ?? NO_CLIENT
    clientLabels.set(clientKey, asset.clientName ?? NO_CLIENT)

    if (!byCity.has(city)) byCity.set(city, new Map())
    const byClient = byCity.get(city)!
    if (!byClient.has(clientKey)) byClient.set(clientKey, [])
    byClient.get(clientKey)!.push(asset)
  }

  return [...byCity.entries()]
    .map(([city, byClient]) => ({
      city,
      clientGroups: [...byClient.entries()]
        .map(([clientKey, list]) => ({ clientKey, clientLabel: clientLabels.get(clientKey) ?? NO_CLIENT, assets: list }))
        .sort((a, b) => a.clientLabel.localeCompare(b.clientLabel))
    }))
    .sort((a, b) => a.city.localeCompare(b.city))
})

const openGroups = ref<string[]>([])
const groupsInitialized = ref(false)

const dialogVisible = ref(false)
const saving = ref(false)
const registeringAsset = ref<MeterReadingAssetDto | null>(null)
const form = reactive({ counterValue: undefined as number | undefined, readingDate: '' })

async function loadData(silent = false) {
  if (!silent) loading.value = true
  try {
    const { data } = await meterReadingsApi.listMeterReadingAssets()
    assets.value = data
    if (!groupsInitialized.value) {
      openGroups.value = []
      groupsInitialized.value = true
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los activos.')
  } finally {
    loading.value = false
  }
}

const tonerDialogRef = ref<InstanceType<typeof TonerDialog> | null>(null)

function openToner(asset: MeterReadingAssetDto) {
  tonerDialogRef.value?.open({ id: asset.assetId, label: `${asset.assetBrandName} ${asset.model} — ${asset.serialNumber}` })
}

function openRegisterDialog(asset: MeterReadingAssetDto) {
  registeringAsset.value = asset
  form.counterValue = undefined
  form.readingDate = ''
  dialogVisible.value = true
}

async function handleSave() {
  if (!registeringAsset.value || form.counterValue === undefined) return
  const lastReading = registeringAsset.value.lastMeterReading
  if (lastReading != null && form.counterValue < lastReading) {
    ElMessage.error(`El contador no puede ser menor al último registrado (${lastReading}).`)
    return
  }
  saving.value = true
  try {
    await meterReadingsApi.registerMeterReading(registeringAsset.value.assetId, {
      counterValue: form.counterValue,
      readingDate: form.readingDate ? new Date(form.readingDate).toISOString() : null
    })
    ElMessage.success('Contador registrado.')
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo registrar el contador.')
  } finally {
    saving.value = false
  }
}

onMounted(loadData)
useRealtimeUpdates(['MeterReading', 'Asset', 'TechnicianAsset', 'Inventory'], () => loadData(true))
</script>

<template>
  <div>
    <h1>{{ isTechnician ? 'Mis máquinas' : 'Lectura de contadores' }}</h1>
    <p class="hint">
      <template v-if="isTechnician">
        Las máquinas que tienes vinculadas. Registra su contador y el tóner que entregas o cambias; así se mantienen al día
        los cronogramas de mantenimiento y se mide cuánto dura cada tóner.
      </template>
      <template v-else>Registrar el contador aquí mantiene actualizados los cronogramas de mantenimiento de cada activo.</template>
    </p>

    <el-empty
      v-if="isTechnician && !loading && assets.length === 0"
      description="Todavía no tienes máquinas vinculadas."
    >
      <p class="hint">
        Se vinculan solas cuando completas la instalación de un equipo. Si necesitas otras, pídele a un administrador que
        te las vincule desde Técnicos → Activos.
      </p>
    </el-empty>

    <div class="filters-bar">
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 220px">
        <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Filtrar por cliente" style="width: 240px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-radio-group v-model="viewMode" class="view-toggle">
        <el-radio-button value="grouped">Agrupar por ciudad</el-radio-button>
        <el-radio-button value="flat">Ver como lista</el-radio-button>
      </el-radio-group>
    </div>

    <el-table v-if="viewMode === 'flat'" :data="filteredAssets" v-loading="loading" stripe>
      <el-table-column prop="assetBrandName" label="Marca" width="120" sortable />
      <el-table-column prop="model" label="Modelo" sortable />
      <el-table-column prop="serialNumber" label="Serie" width="140" sortable />
      <el-table-column prop="clientName" label="Cliente" sortable>
        <template #default="{ row }">{{ row.clientName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="clientLocationName" label="Sede" sortable>
        <template #default="{ row }">{{ row.clientLocationName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="140" sortable>
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140" sortable>
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="lastMeterReading" label="Último contador" width="140" sortable>
        <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Último tóner" min-width="220">
        <template #default="{ row }">{{ lastTonerLabel(row) }}</template>
      </el-table-column>
      <el-table-column label="" width="250">
        <template #default="{ row }">
          <el-button type="primary" size="small" @click="openRegisterDialog(row)">Registrar contador</el-button>
          <el-button size="small" @click="openToner(row)">Tóner</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-collapse v-else v-model="openGroups" v-loading="loading">
      <el-collapse-item v-for="cityGroup in groupedByCity" :key="cityGroup.city" :name="cityGroup.city">
        <template #title>
          {{ cityGroup.city }} ({{ cityGroup.clientGroups.reduce((n, g) => n + g.assets.length, 0) }})
        </template>
        <el-collapse v-model="openGroups" class="nested-collapse">
          <el-collapse-item
            v-for="clientGroup in cityGroup.clientGroups"
            :key="clientGroup.clientKey"
            :name="`${cityGroup.city}::${clientGroup.clientKey}`"
          >
            <template #title>{{ clientGroup.clientLabel }} ({{ clientGroup.assets.length }})</template>
            <el-table :data="clientGroup.assets">
              <el-table-column label="Activo">
                <template #default="{ row }">{{ row.assetBrandName }} {{ row.model }} — {{ row.serialNumber }}</template>
              </el-table-column>
              <el-table-column label="Sede">
                <template #default="{ row }">{{ row.clientLocationName ?? '—' }}</template>
              </el-table-column>
              <el-table-column label="Área" width="140">
                <template #default="{ row }">{{ row.area ?? '—' }}</template>
              </el-table-column>
              <el-table-column label="Último contador" width="140">
                <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
              </el-table-column>
              <el-table-column label="Último tóner" min-width="200">
                <template #default="{ row }">{{ lastTonerLabel(row) }}</template>
              </el-table-column>
              <el-table-column label="" width="250">
                <template #default="{ row }">
                  <el-button type="primary" size="small" @click="openRegisterDialog(row)">Registrar contador</el-button>
                  <el-button size="small" @click="openToner(row)">Tóner</el-button>
                </template>
              </el-table-column>
            </el-table>
          </el-collapse-item>
        </el-collapse>
      </el-collapse-item>
    </el-collapse>

    <el-dialog v-model="dialogVisible" title="Registrar contador" width="420px">
      <p v-if="registeringAsset" class="dialog-subtitle">
        {{ registeringAsset.assetBrandName }} {{ registeringAsset.model }} — {{ registeringAsset.serialNumber }}
      </p>
      <el-form label-position="top">
        <el-form-item>
          <template #label>
            Contador
            <span v-if="registeringAsset?.lastMeterReading != null" class="last-reading-hint">
              — Último: {{ registeringAsset.lastMeterReading }}
            </span>
          </template>
          <el-input-number v-model="form.counterValue" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="Fecha de la lectura (opcional, hoy por defecto)">
          <el-date-picker v-model="form.readingDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
        </el-form-item>
        <p class="dialog-hint">El contador no puede ser menor al último registrado.</p>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" :disabled="form.counterValue === undefined" @click="handleSave">
          Guardar
        </el-button>
      </template>
    </el-dialog>
  </div>
  <TonerDialog ref="tonerDialogRef" @saved="loadData(true)" />
</template>

<style scoped>
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

.dialog-subtitle {
  margin: 0 0 1rem;
  font-weight: 500;
}

.dialog-hint {
  color: var(--el-text-color-secondary);
  font-size: 0.8rem;
  margin: 0;
}

.last-reading-hint {
  color: var(--el-text-color-secondary);
  font-weight: 400;
}
</style>
