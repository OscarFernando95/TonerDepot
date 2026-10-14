<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
import * as citiesApi from '../../api/cities'
import * as clientsApi from '../../api/clients'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Printer } from '@element-plus/icons-vue'
import * as assetsApi from '../../api/assets'
import * as assetBrandsApi from '../../api/assetBrands'
import * as assetModelsApi from '../../api/assetModels'
import {
  AssetLifecycleStatusLabels,
  AssetTypes,
  type AssetBrandDto,
  type AssetDto,
  type AssetModelDto,
  type AssetTypeName,
  type CityDto,
  type ClientDto
} from '../../api/types'
import { useStatusTag } from '../../composables/useStatusTag'

const router = useRouter()

const brands = ref<AssetBrandDto[]>([])
const models = ref<AssetModelDto[]>([])
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

// Listado paginado y filtrado EN EL SERVIDOR (AUDIT.md H-11): antes se traían hasta 200 activos, se filtraban y
// agrupaban en el cliente y, pasado ese tope, la lista quedaba incompleta sin avisar.
const PAGE_SIZE = 25
const filters = reactive({ search: '', cityId: '', clientId: '', status: '' })
const {
  items: assets,
  total,
  page,
  loading,
  reload: loadAssets
} = usePagedList<AssetDto, typeof filters>((params) => assetsApi.listAssetsPaged(params).then((r) => r.data), {
  filters,
  pageSize: PAGE_SIZE,
  label: 'AssetsListView',
  onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los activos.')
})

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
const statusOptions = Object.entries(AssetLifecycleStatusLabels).map(([value, label]) => ({ value, label }))

const hasActiveAssetFilters = computed(() => !!(filters.search || filters.cityId || filters.clientId || filters.status))
const emptyAssetsText = computed(() =>
  hasActiveAssetFilters.value ? 'No hay activos que coincidan con los filtros.' : 'No hay activos registrados.'
)

function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { search: '', cityId: '', clientId: '', status: '' })
}

// Marcas (para el alta) y catálogos de los filtros.
async function loadCatalogs() {
  try {
    const { data } = await assetBrandsApi.listAssetBrands()
    brands.value = data
  } catch (err: any) {
    console.error('AssetsListView: no se pudieron cargar las marcas', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las marcas.')
  }
  citiesApi.listCities().then(({ data }) => (cityOptions.value = data))
    .catch((err) => console.error('AssetsListView: no se pudieron cargar las ciudades', err))
  clientsApi.listClients().then(({ data }) => (clientOptions.value = data))
    .catch((err) => console.error('AssetsListView: no se pudieron cargar los clientes', err))
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
    await loadAssets()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el activo.')
  } finally {
    saving.value = false
  }
}

const { tagType: statusTagType } = useStatusTag('asset')

function goToDetail(asset: AssetDto) {
  router.push({ name: 'asset-detail', params: { id: asset.id } })
}

onMounted(loadCatalogs)
useRealtimeUpdates(['Asset', 'Client'], () => loadAssets())
</script>

<template>
  <div>
    <div class="page-header">
      <h1><el-icon><Printer /></el-icon> Activos</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo activo</el-button>
    </div>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por serie, modelo, marca, cliente, sede o área" style="width: 320px" />
      <el-select v-model="filters.cityId" clearable filterable placeholder="Ciudad" style="width: 200px">
        <el-option v-for="c in cityOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Cliente" style="width: 220px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-model="filters.status" clearable placeholder="Estado" style="width: 190px">
        <el-option v-for="s in statusOptions" :key="s.value" :label="s.label" :value="s.value" />
      </el-select>
      <el-button v-if="hasActiveAssetFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <!-- Sin columnas ordenables: con paginación del servidor solo reordenarían la página visible. -->
    <el-table
      :data="assets"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="emptyAssetsText"
    >
      <el-table-column prop="assetBrandName" label="Marca" width="120" />
      <el-table-column prop="model" label="Modelo" />
      <el-table-column prop="serialNumber" label="Serie" width="140" />
      <el-table-column prop="type" label="Tipo" width="120" />
      <el-table-column prop="lifecycleStatus" label="Estado" width="160">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.lifecycleStatus)" size="small">
            {{ AssetLifecycleStatusLabels[row.lifecycleStatus as keyof typeof AssetLifecycleStatusLabels] }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        label="Ubicación actual"
      >
        <template #default="{ row }">
          <span v-if="row.currentClientLocationName">
            {{ row.currentClientName }} — {{ row.currentClientLocationName }}
          </span>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="140">
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140">
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="lastMeterReading" label="Última lectura" width="130">
        <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
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
