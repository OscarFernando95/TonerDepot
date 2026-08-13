<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Printer } from '@element-plus/icons-vue'
import * as assetsApi from '../../api/assets'
import * as assetBrandsApi from '../../api/assetBrands'
import * as clientsApi from '../../api/clients'
import * as citiesApi from '../../api/cities'
import {
  AssetLifecycleStatusLabels,
  AssetTypes,
  type AssetBrandDto,
  type AssetDto,
  type AssetTypeName,
  type CityDto,
  type ClientDto
} from '../../api/types'

const router = useRouter()

const assets = ref<AssetDto[]>([])
const brands = ref<AssetBrandDto[]>([])
const clients = ref<ClientDto[]>([])
const cities = ref<CityDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const typeOptions = Object.values(AssetTypes)

const form = reactive({
  assetBrandId: '',
  model: '',
  serialNumber: '',
  type: AssetTypes.Impresora as AssetTypeName
})

const filters = reactive({
  cityName: '' as string,
  clientId: '' as string
})

const filteredAssets = computed(() =>
  assets.value.filter(
    (a) =>
      (!filters.cityName || a.cityName === filters.cityName) &&
      (!filters.clientId || a.currentClientId === filters.clientId)
  )
)

async function loadData() {
  loading.value = true
  try {
    const [assetsRes, brandsRes, clientsRes, citiesRes] = await Promise.all([
      assetsApi.listAssets(),
      assetBrandsApi.listAssetBrands(),
      clientsApi.listClients(),
      citiesApi.listCities()
    ])
    assets.value = assetsRes.data
    brands.value = brandsRes.data
    clients.value = clientsRes.data
    cities.value = citiesRes.data
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.assetBrandId = ''
  form.model = ''
  form.serialNumber = ''
  form.type = AssetTypes.Impresora
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await assetsApi.createAsset({
      assetBrandId: form.assetBrandId,
      model: form.model,
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
      <el-button type="primary" @click="openCreateDialog" :disabled="brands.length === 0">Nuevo activo</el-button>
    </div>
    <el-alert
      v-if="!loading && brands.length === 0"
      type="warning"
      :closable="false"
      class="page-alert"
      title="Primero crea una marca en la sección Marcas para poder registrar activos."
    />

    <div class="filters-bar">
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 220px">
        <el-option v-for="c in cities" :key="c.id" :label="c.name" :value="c.name" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Filtrar por cliente" style="width: 240px">
        <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
    </div>

    <el-table :data="filteredAssets" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
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
      <el-table-column label="Ubicación actual">
        <template #default="{ row }">
          <span v-if="row.currentClientLocationName">
            {{ row.currentClientName }} — {{ row.currentClientLocationName }}
          </span>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column label="Área" width="140">
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Ciudad" width="140">
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Última lectura" width="130">
        <template #default="{ row }">{{ row.lastMeterReading ?? '—' }}</template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo activo" width="440px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Marca">
          <el-select v-model="form.assetBrandId" style="width: 100%" placeholder="Selecciona una marca">
            <el-option v-for="b in brands" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Modelo">
          <el-input v-model="form.model" placeholder="Ej: MP2014" />
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

.page-alert {
  margin-bottom: 1rem;
}

.filters-bar {
  display: flex;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}

.muted {
  color: #9ca3af;
}
</style>
