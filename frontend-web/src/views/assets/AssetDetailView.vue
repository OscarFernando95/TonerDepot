<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as assetsApi from '../../api/assets'
import * as assetBrandsApi from '../../api/assetBrands'
import * as assetModelsApi from '../../api/assetModels'
import * as locationsApi from '../../api/clientLocations'
import {
  AssetAllowedTransitions,
  AssetLifecycleStatusLabels,
  AssetTypes,
  type AssetBrandDto,
  type AssetDto,
  type AssetLifecycleStatusName,
  type AssetModelDto,
  type AssetStatusLogDto,
  type AssetTypeName,
  type ClientLocationDto,
  type MeterReadingDto
} from '../../api/types'

const route = useRoute()
const router = useRouter()
const assetId = route.params.id as string

const asset = ref<AssetDto | null>(null)
const brands = ref<AssetBrandDto[]>([])
const models = ref<AssetModelDto[]>([])
const locations = ref<ClientLocationDto[]>([])
const history = ref<AssetStatusLogDto[]>([])
const meterReadings = ref<MeterReadingDto[]>([])
const loading = ref(false)

const readingDialogVisible = ref(false)
const savingReading = ref(false)
const readingForm = reactive({ counterValue: undefined as number | undefined })

const typeOptions = Object.values(AssetTypes)

const infoForm = reactive({
  assetBrandId: '',
  assetModelId: '',
  serialNumber: '',
  type: AssetTypes.Impresora as AssetTypeName
})
const savingInfo = ref(false)

async function loadModelsForBrand(brandId: string) {
  if (!brandId) {
    models.value = []
    return
  }
  const { data } = await assetModelsApi.listAssetModels(brandId)
  models.value = data
}

async function onBrandChange() {
  infoForm.assetModelId = ''
  await loadModelsForBrand(infoForm.assetBrandId)
}

const statusDialogVisible = ref(false)
const savingStatus = ref(false)
const statusForm = reactive({
  newStatus: '' as AssetLifecycleStatusName | '',
  clientLocationId: '',
  area: '',
  notes: ''
})

// PendienteInstalacion solo se alcanza automáticamente al vincular un activo a un contrato — no es una
// opción manual del selector, aunque la tabla espejo la incluya como transición válida para otros usos.
const availableTransitions = computed(() =>
  (asset.value ? AssetAllowedTransitions[asset.value.lifecycleStatus] : []).filter(
    (s) => s !== 'PendienteInstalacion'
  )
)
const requiresLocationPicker = computed(
  () => statusForm.newStatus === 'Instalado' && !asset.value?.currentClientLocationId
)

async function loadAll() {
  loading.value = true
  try {
    const [assetRes, brandsRes, locationsRes, historyRes, readingsRes] = await Promise.all([
      assetsApi.getAsset(assetId),
      assetBrandsApi.listAssetBrands(),
      locationsApi.listAllLocations(),
      assetsApi.getAssetStatusHistory(assetId),
      assetsApi.getMeterReadings(assetId)
    ])
    asset.value = assetRes.data
    brands.value = brandsRes.data
    locations.value = locationsRes.data
    history.value = historyRes.data
    meterReadings.value = readingsRes.data
    await syncInfoForm()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el activo.')
  } finally {
    loading.value = false
  }
}

async function syncInfoForm() {
  if (!asset.value) return
  const matchedBrand = brands.value.find((b) => b.name === asset.value!.assetBrandName)
  infoForm.assetBrandId = matchedBrand?.id ?? ''
  infoForm.serialNumber = asset.value.serialNumber
  infoForm.type = asset.value.type
  await loadModelsForBrand(infoForm.assetBrandId)
  infoForm.assetModelId = asset.value.assetModelId
}

async function saveInfo() {
  savingInfo.value = true
  try {
    const { data } = await assetsApi.updateAsset(assetId, {
      assetModelId: infoForm.assetModelId,
      serialNumber: infoForm.serialNumber,
      type: infoForm.type
    })
    asset.value = data
    ElMessage.success('Activo actualizado.')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo actualizar el activo.')
  } finally {
    savingInfo.value = false
  }
}

function openStatusDialog() {
  statusForm.newStatus = ''
  statusForm.clientLocationId = ''
  statusForm.area = ''
  statusForm.notes = ''
  statusDialogVisible.value = true
}

async function saveStatus() {
  if (!statusForm.newStatus) return
  savingStatus.value = true
  try {
    const { data } = await assetsApi.changeAssetStatus(assetId, {
      newStatus: statusForm.newStatus,
      clientLocationId: statusForm.clientLocationId || null,
      area: statusForm.area || null,
      notes: statusForm.notes || null
    })
    asset.value = data
    ElMessage.success('Estado actualizado.')
    statusDialogVisible.value = false
    const { data: historyData } = await assetsApi.getAssetStatusHistory(assetId)
    history.value = historyData
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cambiar el estado.')
  } finally {
    savingStatus.value = false
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

function statusLabel(status: string) {
  return AssetLifecycleStatusLabels[status as AssetLifecycleStatusName] ?? status
}

function openReadingDialog() {
  readingForm.counterValue = undefined
  readingDialogVisible.value = true
}

async function saveReading() {
  if (readingForm.counterValue === undefined) return
  const lastReading = asset.value?.lastMeterReading
  if (lastReading != null && readingForm.counterValue < lastReading) {
    ElMessage.error(`El contador no puede ser menor al último registrado (${lastReading}).`)
    return
  }
  savingReading.value = true
  try {
    await assetsApi.addMeterReading(assetId, { counterValue: readingForm.counterValue })
    ElMessage.success('Lectura registrada.')
    readingDialogVisible.value = false
    const { data } = await assetsApi.getMeterReadings(assetId)
    meterReadings.value = data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo registrar la lectura.')
  } finally {
    savingReading.value = false
  }
}

onMounted(loadAll)
</script>

<template>
  <div v-loading="loading">
    <template v-if="asset">
      <div class="page-header">
        <el-button :icon="ArrowLeft" circle title="Volver a Activos" @click="router.push({ name: 'assets' })" />
        <h1>{{ asset.assetBrandName }} {{ asset.model }}</h1>
        <el-tag :type="statusTagType(asset.lifecycleStatus)">{{ statusLabel(asset.lifecycleStatus) }}</el-tag>
      </div>
      <p class="location-line" v-if="asset.currentClientLocationName">
        Ubicado en <strong>{{ asset.currentClientName }} — {{ asset.currentClientLocationName }}</strong>
        <span v-if="asset.area"> ({{ asset.area }})</span>
      </p>

      <el-card class="section-card">
        <template #header>Datos del activo</template>
        <el-form :model="infoForm" label-position="top">
          <div class="form-grid">
            <el-form-item label="Marca">
              <el-select v-model="infoForm.assetBrandId" style="width: 100%" @change="onBrandChange">
                <el-option v-for="b in brands" :key="b.id" :label="b.name" :value="b.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="Modelo">
              <el-select
                v-model="infoForm.assetModelId"
                style="width: 100%"
                :disabled="!infoForm.assetBrandId"
                placeholder="Selecciona un modelo"
              >
                <el-option v-for="m in models" :key="m.id" :label="m.name" :value="m.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="Número de serie">
              <el-input v-model="infoForm.serialNumber" />
            </el-form-item>
            <el-form-item label="Tipo">
              <el-select v-model="infoForm.type" style="width: 100%">
                <el-option v-for="t in typeOptions" :key="t" :label="t" :value="t" />
              </el-select>
            </el-form-item>
          </div>
        </el-form>
        <div class="section-actions">
          <el-button
            type="primary"
            plain
            :disabled="availableTransitions.length === 0"
            @click="openStatusDialog"
          >
            Cambiar estado
          </el-button>
          <el-button type="primary" :loading="savingInfo" @click="saveInfo">Guardar cambios</el-button>
        </div>
      </el-card>

      <el-card class="section-card">
        <template #header>Historial de estado</template>
        <el-table :data="history" stripe>
          <el-table-column label="Cambio" width="260">
            <template #default="{ row }">
              {{ statusLabel(row.previousStatus) }} → {{ statusLabel(row.newStatus) }}
            </template>
          </el-table-column>
          <el-table-column prop="changedAt" label="Fecha" width="180" sortable>
            <template #default="{ row }">{{ new Date(row.changedAt).toLocaleString() }}</template>
          </el-table-column>
          <el-table-column prop="changedByUserName" label="Realizado por" width="180" sortable />
          <el-table-column prop="notes" label="Notas" sortable />
        </el-table>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-card-header">
            <span>Lecturas de contador</span>
            <el-button type="primary" size="small" @click="openReadingDialog">Registrar lectura</el-button>
          </div>
        </template>
        <el-table :data="meterReadings" stripe>
          <el-table-column prop="readingDate" label="Fecha" width="180" sortable>
            <template #default="{ row }">{{ new Date(row.readingDate).toLocaleString() }}</template>
          </el-table-column>
          <el-table-column prop="counterValue" label="Contador" width="140" sortable />
          <el-table-column prop="registeredByUserName" label="Registrado por" sortable />
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="statusDialogVisible" title="Cambiar estado" width="440px">
      <el-form label-position="top">
        <el-form-item label="Nuevo estado">
          <el-select v-model="statusForm.newStatus" style="width: 100%">
            <el-option
              v-for="s in availableTransitions"
              :key="s"
              :label="AssetLifecycleStatusLabels[s]"
              :value="s"
            />
          </el-select>
        </el-form-item>
        <el-form-item
          v-if="statusForm.newStatus === 'Instalado'"
          :label="requiresLocationPicker ? 'Sede de instalación' : 'Sede (opcional, deja vacío para conservar la actual)'"
        >
          <el-select v-model="statusForm.clientLocationId" style="width: 100%" filterable placeholder="Selecciona una sede">
            <el-option
              v-for="l in locations"
              :key="l.id"
              :label="`${l.clientName} — ${l.name}`"
              :value="l.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="statusForm.newStatus === 'Instalado'" label="Área">
          <el-input v-model="statusForm.area" placeholder="Ej: Contabilidad, Recepción" />
        </el-form-item>
        <el-form-item label="Notas">
          <el-input v-model="statusForm.notes" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="statusDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingStatus" @click="saveStatus">Confirmar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="readingDialogVisible" title="Registrar lectura" width="360px">
      <el-form label-position="top">
        <el-form-item>
          <template #label>
            Valor del contador
            <span v-if="asset?.lastMeterReading != null" class="last-reading-hint"> — Último: {{ asset.lastMeterReading }}</span>
          </template>
          <el-input-number v-model="readingForm.counterValue" :min="0" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="readingDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingReading" @click="saveReading">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.25rem;
}

.location-line {
  color: var(--el-text-color-secondary);
  margin: 0 0 1rem;
}

.last-reading-hint {
  color: var(--el-text-color-secondary);
  font-weight: 400;
}

.section-card {
  margin-bottom: 1.5rem;
}

.section-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 1rem;
}

.section-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
