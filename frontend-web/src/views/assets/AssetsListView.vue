<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Printer } from '@element-plus/icons-vue'
import * as assetsApi from '../../api/assets'
import * as assetBrandsApi from '../../api/assetBrands'
import * as assetModelsApi from '../../api/assetModels'
import * as contractsApi from '../../api/contracts'
import {
  AssetLifecycleStatusLabels,
  AssetTypes,
  type AssetBrandDto,
  type AssetDto,
  type AssetModelDto,
  type AssetTypeName,
  type ContractDto
} from '../../api/types'
import { formatDateUTC } from '../../utils/date'

const router = useRouter()

const assets = ref<AssetDto[]>([])
const brands = ref<AssetBrandDto[]>([])
const models = ref<AssetModelDto[]>([])
const contracts = ref<ContractDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const typeOptions = Object.values(AssetTypes)

const form = reactive({
  assetBrandId: '',
  assetModelId: '',
  serialNumber: '',
  type: AssetTypes.Impresora as AssetTypeName
})

async function loadModelsForBrand(brandId: string) {
  if (!brandId) {
    models.value = []
    return
  }
  const { data } = await assetModelsApi.listAssetModels(brandId)
  models.value = data
}

async function onBrandChange() {
  form.assetModelId = ''
  await loadModelsForBrand(form.assetBrandId)
}

const quickBrandDialogVisible = ref(false)
const savingQuickBrand = ref(false)
const quickBrandForm = reactive({ name: '' })

function openQuickBrandDialog() {
  quickBrandForm.name = ''
  quickBrandDialogVisible.value = true
}

async function saveQuickBrand() {
  savingQuickBrand.value = true
  try {
    const { data } = await assetBrandsApi.createAssetBrand({ name: quickBrandForm.name })
    ElMessage.success('Marca creada.')
    quickBrandDialogVisible.value = false
    const { data: brandsData } = await assetBrandsApi.listAssetBrands()
    brands.value = brandsData
    form.assetModelId = ''
    form.assetBrandId = data.id
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear la marca.')
    return
  } finally {
    savingQuickBrand.value = false
  }

  // La marca ya quedó creada y seleccionada aunque esto falle — no se reporta como error de creación.
  try {
    await loadModelsForBrand(form.assetBrandId)
  } catch {
    models.value = []
  }
}

const quickModelDialogVisible = ref(false)
const savingQuickModel = ref(false)
const quickModelForm = reactive({
  name: '',
  generalPrintThreshold: 30000,
  generalMonthsInterval: 6,
  unitsPrintThreshold: 30000,
  unitsMonthsInterval: 6,
  consumablesPrintThreshold: 60000
})

function openQuickModelDialog() {
  quickModelForm.name = ''
  quickModelForm.generalPrintThreshold = 30000
  quickModelForm.generalMonthsInterval = 6
  quickModelForm.unitsPrintThreshold = 30000
  quickModelForm.unitsMonthsInterval = 6
  quickModelForm.consumablesPrintThreshold = 60000
  quickModelDialogVisible.value = true
}

async function saveQuickModel() {
  if (!form.assetBrandId) return
  savingQuickModel.value = true
  try {
    const { data } = await assetModelsApi.createAssetModel(form.assetBrandId, { ...quickModelForm })
    ElMessage.success('Modelo creado.')
    quickModelDialogVisible.value = false
    await loadModelsForBrand(form.assetBrandId)
    form.assetModelId = data.id
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el modelo.')
  } finally {
    savingQuickModel.value = false
  }
}

const viewMode = ref<'grouped' | 'flat'>('grouped')

const filters = reactive({
  cityName: '' as string,
  clientId: '' as string,
  lifecycleStatus: '' as string
})

type FilterKey = keyof typeof filters

function matches(a: AssetDto, exclude: FilterKey) {
  const cityOk = exclude === 'cityName' || !filters.cityName || a.cityName === filters.cityName
  const clientOk = exclude === 'clientId' || !filters.clientId || a.currentClientId === filters.clientId
  const statusOk = exclude === 'lifecycleStatus' || !filters.lifecycleStatus || a.lifecycleStatus === filters.lifecycleStatus
  return cityOk && clientOk && statusOk
}

const cityOptions = computed(() =>
  [...new Set(assets.value.filter((a) => matches(a, 'cityName') && a.cityName).map((a) => a.cityName as string))].sort()
)
const clientOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const a of assets.value.filter((x) => matches(x, 'clientId') && x.currentClientId)) {
    seen.set(a.currentClientId as string, a.currentClientName as string)
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
})
const statusOptions = computed(() =>
  [...new Set(assets.value.filter((a) => matches(a, 'lifecycleStatus')).map((a) => a.lifecycleStatus))]
)

const filteredAssets = computed(() =>
  assets.value.filter((a) => matches(a, 'cityName') && matches(a, 'clientId') && matches(a, 'lifecycleStatus'))
)

const hasActiveAssetFilters = computed(() => !!(filters.cityName || filters.clientId || filters.lifecycleStatus))
const emptyAssetsText = computed(() =>
  hasActiveAssetFilters.value ? 'No hay activos que coincidan con los filtros.' : 'No hay activos registrados.'
)

const contractLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const c of contracts.value) {
    const start = formatDateUTC(c.startDate)
    const end = c.endDate ? formatDateUTC(c.endDate) : 'indefinida'
    map.set(c.id, `${c.clientName} (${start} – ${end})`)
  }
  return map
})

const NO_CITY = 'Sin ciudad'
const WAREHOUSE = 'Bodega — Oficina principal'
const NO_CLIENT = 'Sin cliente'
const NO_CONTRACT = 'Sin contrato'

interface ContractGroup {
  contractId: string
  contractLabel: string
  assets: AssetDto[]
}
interface ClientGroup {
  clientKey: string
  clientLabel: string
  contractGroups: ContractGroup[]
  assetCount: number
}
interface CityGroup {
  city: string
  clientGroups: ClientGroup[]
  assetCount: number
}

const groupedByCity = computed<CityGroup[]>(() => {
  const byCity = new Map<string, Map<string, Map<string, ContractGroup>>>()
  const clientLabels = new Map<string, string>()

  for (const asset of filteredAssets.value) {
    // Un activo EnBodega no tiene sede/ciudad porque físicamente está en la bodega de la empresa, no en
    // la de un cliente — no es lo mismo que "no sabemos su ciudad" (ej. DadoDeBaja sin ubicación previa).
    const city = asset.cityName ?? (asset.lifecycleStatus === 'EnBodega' ? WAREHOUSE : NO_CITY)
    const clientKey = asset.currentClientId ?? NO_CLIENT
    clientLabels.set(clientKey, asset.currentClientName ?? NO_CLIENT)
    const contractKey = asset.activeContractId ?? NO_CONTRACT
    const contractLabel = asset.activeContractId
      ? (contractLabelById.value.get(asset.activeContractId) ?? NO_CONTRACT)
      : NO_CONTRACT

    if (!byCity.has(city)) byCity.set(city, new Map())
    const byClient = byCity.get(city)!
    if (!byClient.has(clientKey)) byClient.set(clientKey, new Map())
    const byContract = byClient.get(clientKey)!
    if (!byContract.has(contractKey)) {
      byContract.set(contractKey, { contractId: contractKey, contractLabel, assets: [] })
    }
    byContract.get(contractKey)!.assets.push(asset)
  }

  return [...byCity.entries()]
    .map(([city, byClient]) => {
      const clientGroups = [...byClient.entries()]
        .map(([clientKey, byContract]) => {
          const contractGroups = [...byContract.values()].sort((a, b) => a.contractLabel.localeCompare(b.contractLabel))
          return {
            clientKey,
            clientLabel: clientLabels.get(clientKey) ?? NO_CLIENT,
            contractGroups,
            assetCount: contractGroups.reduce((n, c) => n + c.assets.length, 0)
          }
        })
        .sort((a, b) => a.clientLabel.localeCompare(b.clientLabel))
      return {
        city,
        clientGroups,
        assetCount: clientGroups.reduce((n, g) => n + g.assetCount, 0)
      }
    })
    .sort((a, b) => a.city.localeCompare(b.city))
})

const openGroups = ref<string[]>([])
const groupsInitialized = ref(false)

async function loadData() {
  loading.value = true
  try {
    const [assetsRes, brandsRes, contractsRes] = await Promise.all([
      assetsApi.listAssets(),
      assetBrandsApi.listAssetBrands(),
      contractsApi.listContracts()
    ])
    assets.value = assetsRes.data
    brands.value = brandsRes.data
    contracts.value = contractsRes.data

    if (!groupsInitialized.value) {
      // Colapsadas por defecto — el usuario expande a demanda el grupo que le interesa.
      openGroups.value = []
      groupsInitialized.value = true
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los activos.')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.assetBrandId = ''
  form.assetModelId = ''
  form.serialNumber = ''
  form.type = AssetTypes.Impresora
  models.value = []
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await assetsApi.createAsset({
      assetModelId: form.assetModelId,
      serialNumber: form.serialNumber,
      type: form.type
    })
    ElMessage.success('Activo creado.')
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el activo.')
  } finally {
    saving.value = false
  }
}

function statusTagType(status: string) {
  switch (status) {
    case 'Instalado':
      return 'success'
    case 'EnMantenimiento':
    case 'PendienteInstalacion':
      return 'warning'
    case 'DadoDeBaja':
      return 'danger'
    default:
      return 'info'
  }
}

function goToDetail(asset: AssetDto) {
  router.push({ name: 'asset-detail', params: { id: asset.id } })
}

onMounted(loadData)
</script>

<template>
  <div>
    <div class="page-header">
      <h1><el-icon><Printer /></el-icon> Activos</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo activo</el-button>
    </div>

    <div class="filters-bar">
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 220px">
        <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Filtrar por cliente" style="width: 240px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.lifecycleStatus" clearable filterable placeholder="Filtrar por estado" style="width: 200px">
        <el-option
          v-for="s in statusOptions"
          :key="s"
          :label="AssetLifecycleStatusLabels[s as keyof typeof AssetLifecycleStatusLabels]"
          :value="s"
        />
      </el-select>
      <el-radio-group v-model="viewMode" class="view-toggle">
        <el-radio-button value="grouped">Agrupar por ciudad</el-radio-button>
        <el-radio-button value="flat">Ver como lista</el-radio-button>
      </el-radio-group>
    </div>

    <el-table
      v-if="viewMode === 'flat'"
      :data="filteredAssets"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="emptyAssetsText"
    >
      <el-table-column prop="assetBrandName" label="Marca" width="120" sortable />
      <el-table-column prop="model" label="Modelo" sortable />
      <el-table-column prop="serialNumber" label="Serie" width="140" sortable />
      <el-table-column prop="type" label="Tipo" width="120" sortable />
      <el-table-column prop="lifecycleStatus" label="Estado" width="160" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.lifecycleStatus)" size="small">
            {{ AssetLifecycleStatusLabels[row.lifecycleStatus as keyof typeof AssetLifecycleStatusLabels] }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        label="Ubicación actual"
        sortable
        :sort-method="(a: AssetDto, b: AssetDto) => (a.currentClientName ?? '').localeCompare(b.currentClientName ?? '')"
      >
        <template #default="{ row }">
          <span v-if="row.currentClientLocationName">
            {{ row.currentClientName }} — {{ row.currentClientLocationName }}
          </span>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="140" sortable>
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140" sortable>
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="lastMeterReading" label="Última lectura" width="130" sortable>
        <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
      </el-table-column>
    </el-table>

    <el-collapse v-else v-model="openGroups" v-loading="loading">
      <el-collapse-item v-for="cityGroup in groupedByCity" :key="cityGroup.city" :name="cityGroup.city">
        <template #title>
          {{ cityGroup.city }} ({{ cityGroup.assetCount }})
        </template>
        <el-collapse v-model="openGroups" class="nested-collapse">
          <el-collapse-item
            v-for="clientGroup in cityGroup.clientGroups"
            :key="clientGroup.clientKey"
            :name="`${cityGroup.city}::${clientGroup.clientKey}`"
          >
            <template #title>
              {{ clientGroup.clientLabel }} ({{ clientGroup.assetCount }})
            </template>
            <el-collapse v-model="openGroups" class="nested-collapse">
              <el-collapse-item
                v-for="contractGroup in clientGroup.contractGroups"
                :key="contractGroup.contractId"
                :name="`${cityGroup.city}::${clientGroup.clientKey}::${contractGroup.contractId}`"
              >
                <template #title>{{ contractGroup.contractLabel }} ({{ contractGroup.assets.length }})</template>
                <el-table :data="contractGroup.assets" @row-click="goToDetail" class="clickable-rows">
                  <el-table-column prop="assetBrandName" label="Marca" width="120" />
                  <el-table-column prop="model" label="Modelo" />
                  <el-table-column prop="serialNumber" label="Serie" width="140" />
                  <el-table-column prop="type" label="Tipo" width="120" />
                  <el-table-column label="Estado" width="160">
                    <template #default="{ row }">
                      <el-tag :type="statusTagType(row.lifecycleStatus)" size="small">
                        {{ AssetLifecycleStatusLabels[row.lifecycleStatus as keyof typeof AssetLifecycleStatusLabels] }}
                      </el-tag>
                    </template>
                  </el-table-column>
                  <el-table-column label="Área" width="140">
                    <template #default="{ row }">{{ row.area ?? '—' }}</template>
                  </el-table-column>
                  <el-table-column label="Última lectura" width="130">
                    <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
                  </el-table-column>
                </el-table>
              </el-collapse-item>
            </el-collapse>
          </el-collapse-item>
        </el-collapse>
      </el-collapse-item>
    </el-collapse>

    <el-dialog v-model="dialogVisible" title="Nuevo activo" width="440px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Marca">
          <div class="select-with-quick-create">
            <el-select
              v-model="form.assetBrandId"
              style="width: 100%"
              placeholder="Selecciona una marca"
              @change="onBrandChange"
            >
              <el-option v-for="b in brands" :key="b.id" :label="b.name" :value="b.id" />
            </el-select>
            <el-button :icon="Plus" circle title="Nueva marca" @click="openQuickBrandDialog" />
          </div>
        </el-form-item>
        <el-form-item label="Modelo">
          <div class="select-with-quick-create">
            <el-select
              v-model="form.assetModelId"
              style="width: 100%"
              :disabled="!form.assetBrandId"
              placeholder="Selecciona un modelo"
            >
              <el-option v-for="m in models" :key="m.id" :label="m.name" :value="m.id" />
            </el-select>
            <el-button :icon="Plus" circle title="Nuevo modelo" :disabled="!form.assetBrandId" @click="openQuickModelDialog" />
          </div>
        </el-form-item>
        <el-form-item label="Número de serie">
          <el-input v-model="form.serialNumber" />
        </el-form-item>
        <el-form-item label="Tipo">
          <el-select v-model="form.type" style="width: 100%">
            <el-option v-for="t in typeOptions" :key="t" :label="t" :value="t" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">Guardar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="quickBrandDialogVisible" title="Nueva marca" width="360px">
      <el-form :model="quickBrandForm" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="quickBrandForm.name" placeholder="Ej: Ricoh" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="quickBrandDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingQuickBrand" @click="saveQuickBrand">Guardar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="quickModelDialogVisible" title="Nuevo modelo" width="480px">
      <el-form :model="quickModelForm" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="quickModelForm.name" placeholder="Ej: MP2014" />
        </el-form-item>
        <div class="form-grid">
          <el-form-item label="Mantenimiento general — impresiones">
            <el-input-number v-model="quickModelForm.generalPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento general — meses">
            <el-input-number v-model="quickModelForm.generalMonthsInterval" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento de unidades — impresiones">
            <el-input-number v-model="quickModelForm.unitsPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento de unidades — meses">
            <el-input-number v-model="quickModelForm.unitsMonthsInterval" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Cambio de insumos — impresiones">
            <el-input-number v-model="quickModelForm.consumablesPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="quickModelDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingQuickModel" @click="saveQuickModel">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.page-header h1 {
  display: flex;
  align-items: center;
  gap: 0.5rem;
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

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}

.muted {
  color: #9ca3af;
}

.select-with-quick-create {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 1rem;
}
</style>
