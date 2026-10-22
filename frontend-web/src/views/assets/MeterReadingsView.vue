<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
import * as citiesApi from '../../api/cities'
import * as clientsApi from '../../api/clients'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as meterReadingsApi from '../../api/meterReadings'
import * as technicianSelfApi from '../../api/technicianSelf'
import type { CityDto, ClientDto, MeterReadingAssetDto } from '../../api/types'
import TonerDialog from '../../components/inventory/TonerDialog.vue'
import RentalRetirementTag from '../../components/assets/RentalRetirementTag.vue'
import MaintenanceChoiceDialog from '../../components/maintenance/MaintenanceChoiceDialog.vue'
import { maintenanceStartedMessage, type MaintenanceChoice } from '../../composables/useMaintenanceChoice'
import { useAuthStore } from '../../stores/auth'
import { RoleNames } from '../../api/types'

const auth = useAuthStore()
const router = useRouter()
// Para el técnico esta misma pantalla es "Mis máquinas": las que tiene vinculadas, con su contador y su tóner.
const isTechnician = computed(() => auth.hasRole(RoleNames.Tecnico))

// Dos pestañas, solo Técnico (espejo de MeterReadingsScreen en el móvil):
// "coverage" (por defecto) — activos instalados en los municipios de su zona, se vinculan solos al
// asignarle la zona (ver TechnicianService.LinkInstalledAssetsInZonesAsync, backend).
// "linked" ("Otras máquinas") — el caso excepcional: activos vinculados a mano por un admin que
// quedan FUERA de su zona (ver AssetService.ListForMeterReadingAsync, que excluye a propósito los que
// ya caen en cobertura, para no duplicarlos entre las dos pestañas).
const tab = ref<'linked' | 'coverage'>('coverage')
const activeTab = computed(() => (isTechnician.value ? tab.value : 'linked'))

const tonerDate = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
function lastTonerLabel(a: MeterReadingAssetDto) {
  if (!a.lastTonerAt) return 'Sin tóner registrado'
  const counter = a.lastTonerCounter != null ? ` · contador ${a.lastTonerCounter}` : ''
  return `${tonerDate.format(new Date(a.lastTonerAt))}${counter} · ${a.tonerUnitsLast90Days} en 90 días`
}

// Listado paginado y filtrado EN EL SERVIDOR (AUDIT.md H-11): antes se traían hasta 200 filas, se filtraban y
// agrupaban en el cliente y, pasado ese tope, la lista quedaba incompleta sin avisar. `scope` elige la pestaña.
const PAGE_SIZE = 25
const filters = reactive({ scope: 'linked' as 'linked' | 'coverage', search: '', cityId: '', clientId: '' })
watch(activeTab, (value) => (filters.scope = value), { immediate: true })

const {
  items: assets,
  total,
  page,
  loading,
  reload: loadAll
} = usePagedList<MeterReadingAssetDto, typeof filters>(
  ({ scope, ...params }) =>
    (scope === 'coverage'
      ? meterReadingsApi.listMeterReadingAssetsByCoverage(params)
      : meterReadingsApi.listMeterReadingAssets(params)
    ).then((r) => r.data),
  {
    filters,
    pageSize: PAGE_SIZE,
    label: 'MeterReadingsView',
    onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los activos.')
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

const hasFilters = computed(() => !!(filters.search || filters.cityId || filters.clientId))
function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { search: '', cityId: '', clientId: '' })
}

// Ciudades y clientes para filtrar: solo Staff (los endpoints son de staff). El Técnico filtra por texto, que ya
// busca también por ciudad, cliente y sede.
const cityOptions = ref<CityDto[]>([])
const clientOptions = ref<ClientDto[]>([])
if (!isTechnician.value) {
  citiesApi.listCities().then(({ data }) => (cityOptions.value = data))
    .catch((err) => console.error('MeterReadingsView: no se pudieron cargar las ciudades', err))
  clientsApi.listClients().then(({ data }) => (clientOptions.value = data))
    .catch((err) => console.error('MeterReadingsView: no se pudieron cargar los clientes', err))
}

const dialogVisible = ref(false)
const saving = ref(false)
const registeringAsset = ref<MeterReadingAssetDto | null>(null)
const form = reactive({ counterValue: undefined as number | undefined, readingDate: '' })

const tonerDialogRef = ref<InstanceType<typeof TonerDialog> | null>(null)

function openToner(asset: MeterReadingAssetDto) {
  tonerDialogRef.value?.open({ id: asset.assetId, label: `${asset.assetBrandName} ${asset.model} — ${asset.serialNumber}` })
}

// Mantenimiento a demanda por iniciativa del técnico: elige qué va a hacer, el servidor crea la orden ya asignada a él
// y la atiende desde "Mi trabajo" (check-in con foto, check-out con contador, que reinicia el cronograma).
const maintenanceDialogVisible = ref(false)
const startingMaintenance = ref(false)
const maintenanceAsset = ref<MeterReadingAssetDto | null>(null)
const maintenanceSubtitle = computed(() =>
  maintenanceAsset.value ? `${maintenanceAsset.value.assetBrandName} ${maintenanceAsset.value.model} — ${maintenanceAsset.value.serialNumber}` : ''
)

function openMaintenanceDialog(asset: MeterReadingAssetDto) {
  maintenanceAsset.value = asset
  maintenanceDialogVisible.value = true
}

async function startMaintenance(choice: MaintenanceChoice) {
  if (!maintenanceAsset.value) return
  startingMaintenance.value = true
  try {
    await technicianSelfApi.startMaintenance({ assetId: maintenanceAsset.value.assetId, ...choice })
    maintenanceDialogVisible.value = false
    ElMessage.success(maintenanceStartedMessage(choice.plannedAt))
    router.push({ name: 'my-work' })
  } catch (err: any) {
    console.error('No se pudo iniciar el mantenimiento:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo iniciar el mantenimiento.')
  } finally {
    startingMaintenance.value = false
  }
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
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo registrar el contador.')
  } finally {
    saving.value = false
  }
}

// "Technician"/"Zone": al asignarle una zona a este técnico (o agregarle un municipio a una zona que
// ya tenía) se auto-vinculan sus activos ya instalados ahí — sin escuchar estos dos tópicos, "Por
// cobertura" se quedaba mostrando el municipio nuevo pero vacío hasta recargar la página a mano.
useRealtimeUpdates(['MeterReading', 'Asset', 'TechnicianAsset', 'Inventory', 'Technician', 'Zone'], () => loadAll())
</script>

<template>
  <div>
    <h1>{{ isTechnician ? 'Mis máquinas' : 'Lectura de contadores' }}</h1>
    <p class="hint">
      <template v-if="isTechnician">
        Registra el contador y el tóner que entregas o cambias; así se mantienen al día los cronogramas de mantenimiento y
        se mide cuánto dura cada tóner.
      </template>
      <template v-else>Registrar el contador aquí mantiene actualizados los cronogramas de mantenimiento de cada activo.</template>
    </p>

    <el-radio-group v-if="isTechnician" v-model="tab" class="tab-toggle">
      <el-radio-button value="coverage">Por cobertura</el-radio-button>
      <el-radio-button value="linked">Otras máquinas</el-radio-button>
    </el-radio-group>

    <el-empty
      v-if="isTechnician && !loading && total === 0 && !hasFilters"
      :description="activeTab === 'coverage' ? 'No hay equipos instalados en las ciudades de tu cobertura.' : 'No tienes otras máquinas vinculadas.'"
    >
      <p v-if="activeTab === 'linked'" class="hint">
        Aquí aparecen solo los equipos que un administrador te vincula a mano cuando quedan fuera de tu zona de cobertura
        (ej. para que revises algo puntual). Las máquinas de tu zona están en la pestaña "Por cobertura".
      </p>
    </el-empty>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por serie, modelo, cliente, sede o ciudad" style="width: 320px" />
      <el-select v-if="!isTechnician" v-model="filters.cityId" clearable filterable placeholder="Ciudad" style="width: 220px">
        <el-option v-for="c in cityOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-select v-if="!isTechnician" v-model="filters.clientId" clearable filterable placeholder="Cliente" style="width: 240px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-button v-if="hasFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <!-- Sin columnas ordenables: con paginación del servidor un orden por columna solo reordenaría la página visible. -->
    <el-table :data="assets" v-loading="loading" stripe :empty-text="hasFilters ? 'Ninguna máquina coincide con los filtros.' : 'No hay máquinas.'">
      <el-table-column prop="assetBrandName" label="Marca" width="120" />
      <el-table-column prop="model" label="Modelo" />
      <el-table-column prop="serialNumber" label="Serie" width="140" />
      <el-table-column prop="clientName" label="Cliente">
        <template #default="{ row }">{{ row.clientName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="clientLocationName" label="Sede">
        <template #default="{ row }">{{ row.clientLocationName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="area" label="Área" width="140">
        <template #default="{ row }">{{ row.area ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140">
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="lastMeterReading" label="Último contador" width="210">
        <template #default="{ row }">
          {{ row.lastMeterReading ?? '—' }}
          <RentalRetirementTag :subject="row" />
        </template>
      </el-table-column>
      <el-table-column label="Último tóner" min-width="220">
        <template #default="{ row }">{{ lastTonerLabel(row) }}</template>
      </el-table-column>
      <el-table-column label="" :width="isTechnician ? 380 : 250">
        <template #default="{ row }">
          <el-button type="primary" size="small" @click="openRegisterDialog(row)">Registrar contador</el-button>
          <el-button size="small" @click="openToner(row)">Tóner</el-button>
          <!-- Solo el técnico: el staff pide el mantenimiento desde el detalle del activo. -->
          <el-button v-if="isTechnician" size="small" @click="openMaintenanceDialog(row)">Mantenimiento</el-button>
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
  <MaintenanceChoiceDialog
    v-model="maintenanceDialogVisible"
    title="Iniciar mantenimiento"
    confirm-label="Iniciar"
    :subtitle="maintenanceSubtitle"
    hint="Marca lo que vas a hacer. Al cerrar la visita, el cronograma del equipo reinicia solo eso."
    :loading="startingMaintenance"
    allow-schedule
    @confirm="startMaintenance"
  />
  <TonerDialog ref="tonerDialogRef" @saved="loadAll()" />
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.tab-toggle {
  margin-bottom: 1rem;
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
