<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import * as zonesApi from '../../api/zones'
import * as assetsApi from '../../api/assets'
import type { TechnicianAssetDto, TechnicianDto } from '../../api/technicians'
import type { AssetDto } from '../../api/types'
import type { ZoneDto } from '../../api/zones'
import TechnicianScheduleDialog from '../../components/technicians/TechnicianScheduleDialog.vue'
import TechnicianTimeOffDialog from '../../components/technicians/TechnicianTimeOffDialog.vue'
import TechnicianVisitsDialog from '../../components/technicians/TechnicianVisitsDialog.vue'

const visitsDialogRef = ref<InstanceType<typeof TechnicianVisitsDialog> | null>(null)

const scheduleDialogRef = ref<InstanceType<typeof TechnicianScheduleDialog> | null>(null)
const timeOffDialogRef = ref<InstanceType<typeof TechnicianTimeOffDialog> | null>(null)

const dateTimeFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

// Disponibilidad calculada por el servidor: horario laboral + festivos + fuera de la oficina.
function availability(t: TechnicianDto): { label: string; type: 'success' | 'warning' | 'info' } {
  if (t.timeOffUntil) return { label: `Fuera de la oficina hasta ${dateTimeFormatter.format(new Date(t.timeOffUntil))}`, type: 'warning' }
  return t.isWorkingNow ? { label: 'En horario', type: 'success' } : { label: 'Fuera de horario', type: 'info' }
}

const technicians = ref<TechnicianDto[]>([])
const zones = ref<ZoneDto[]>([])
const loading = ref(false)

const zonesDialogVisible = ref(false)
const selectedTechnician = ref<TechnicianDto | null>(null)
const selectedZoneIds = ref<string[]>([])
const loadingZones = ref(false)
const savingZones = ref(false)

// Activos vinculados explícitamente al técnico — gobierna qué ve en "Lectura
// de contadores" en la app móvil (ver AssetService.ListForMeterReadingAsync,
// backend). Independiente de la cobertura por ciudad: esa ya no otorga
// visibilidad de activos por sí sola (decisión de arquitectura 2026-09-27).
const assetsDialogVisible = ref(false)
const linkedAssets = ref<TechnicianAssetDto[]>([])
const installedAssets = ref<AssetDto[]>([])
const loadingAssets = ref(false)
const savingAsset = ref(false)
const assetSearch = ref('')

// GET /assets no tiene filtro server-side (mismo comentario que en el
// catálogo de activos) — el buscador filtra client-side sobre el catálogo
// completo de activos Instalado ya cargado.
const availableAssets = computed(() => {
  const linkedIds = new Set(linkedAssets.value.map((l) => l.assetId))
  const q = assetSearch.value.trim().toLowerCase()
  return installedAssets.value.filter((a) => {
    if (linkedIds.has(a.id)) return false
    if (!q) return true
    return (
      a.serialNumber.toLowerCase().includes(q) ||
      a.model.toLowerCase().includes(q) ||
      (a.currentClientName?.toLowerCase().includes(q) ?? false) ||
      (a.currentClientLocationName?.toLowerCase().includes(q) ?? false) ||
      (a.cityName?.toLowerCase().includes(q) ?? false)
    )
  })
})

async function loadData() {
  loading.value = true
  try {
    const [techRes, zonesRes] = await Promise.all([techniciansApi.listTechnicians(), zonesApi.listZones()])
    technicians.value = techRes.data
    zones.value = zonesRes.data
  } catch (err: any) {
    console.error('TechniciansView.loadData failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los técnicos.')
  } finally {
    loading.value = false
  }
}

function statusTagType(status: string) {
  switch (status) {
    case 'Disponible':
      return 'success'
    case 'Ocupado':
      return 'warning'
    case 'EnTransito':
      return 'primary'
    default:
      return 'info'
  }
}

async function openZonesDialog(technician: TechnicianDto) {
  selectedTechnician.value = technician
  selectedZoneIds.value = []
  zonesDialogVisible.value = true
  loadingZones.value = true
  try {
    const { data } = await techniciansApi.listTechnicianZones(technician.id)
    selectedZoneIds.value = data.map((z) => z.zoneId)
  } catch (err: any) {
    console.error('TechniciansView.openZonesDialog failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las zonas.')
  } finally {
    loadingZones.value = false
  }
}

async function saveZones() {
  if (!selectedTechnician.value) return
  savingZones.value = true
  try {
    await techniciansApi.setTechnicianZones(selectedTechnician.value.id, selectedZoneIds.value)
    ElMessage.success('Zonas actualizadas.')
    zonesDialogVisible.value = false
    await loadData()
  } catch (err: any) {
    console.error('TechniciansView.saveZones failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron guardar las zonas.')
  } finally {
    savingZones.value = false
  }
}

async function openAssetsDialog(technician: TechnicianDto) {
  selectedTechnician.value = technician
  assetSearch.value = ''
  assetsDialogVisible.value = true
  loadingAssets.value = true
  try {
    const [linkedRes, assetsRes] = await Promise.all([
      techniciansApi.listTechnicianAssets(technician.id),
      assetsApi.listAssets()
    ])
    linkedAssets.value = linkedRes.data
    installedAssets.value = assetsRes.data.filter((a) => a.lifecycleStatus === 'Instalado')
  } catch (err: any) {
    console.error('TechniciansView.openAssetsDialog failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los activos.')
  } finally {
    loadingAssets.value = false
  }
}

async function linkAsset(asset: AssetDto) {
  if (!selectedTechnician.value) return
  savingAsset.value = true
  try {
    const { data } = await techniciansApi.addTechnicianAsset(selectedTechnician.value.id, asset.id)
    linkedAssets.value = [...linkedAssets.value, data]
  } catch (err: any) {
    console.error('TechniciansView.linkAsset failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo vincular el activo.')
  } finally {
    savingAsset.value = false
  }
}

// Cubre el caso "técnico con cobertura nueva en una ciudad, sin activos
// vinculados todavía": filtrar por el nombre del cliente en el buscador y
// vincular todo de una vez en lugar de uno por uno.
async function linkAllVisible() {
  if (!selectedTechnician.value) return
  const toLink = [...availableAssets.value]
  if (toLink.length === 0) return
  savingAsset.value = true
  try {
    for (const asset of toLink) {
      const { data } = await techniciansApi.addTechnicianAsset(selectedTechnician.value.id, asset.id)
      linkedAssets.value = [...linkedAssets.value, data]
    }
    ElMessage.success(`${toLink.length} activo(s) vinculado(s).`)
  } catch (err: any) {
    console.error('TechniciansView.linkAllVisible failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron vincular todos los activos.')
  } finally {
    savingAsset.value = false
  }
}

async function unlinkAsset(item: TechnicianAssetDto) {
  if (!selectedTechnician.value) return
  try {
    await techniciansApi.removeTechnicianAsset(selectedTechnician.value.id, item.id)
    linkedAssets.value = linkedAssets.value.filter((l) => l.id !== item.id)
    ElMessage.success('Activo desvinculado.')
  } catch (err: any) {
    console.error('TechniciansView.unlinkAsset failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo desvincular el activo.')
  }
}

onMounted(loadData)

// Disponibilidad, cobertura y estado del técnico cambian solos (check-in/out, fuera de la oficina).
useRealtimeUpdates(['Technician', 'Zone'], () => loadData())
</script>

<template>
  <div>
    <h1>Técnicos</h1>
    <p class="hint">
      La cobertura por ciudad, el horario laboral y los períodos fuera de la oficina son lo que usa el motor de
      asignación para elegir candidatos. El estado (Disponible/Ocupado) lo mueve automáticamente el
      check-in/check-out del técnico, no se edita aquí. Los activos vinculados son independientes de la
      cobertura: gobiernan qué equipos ve el técnico en "Lectura de contadores" en la app — un técnico con
      cobertura pero sin activos vinculados no verá ningún equipo ahí.
    </p>

    <el-table :data="technicians" v-loading="loading" stripe empty-text="No hay técnicos registrados.">
      <el-table-column prop="fullName" label="Nombre" sortable />
      <el-table-column prop="status" label="Estado" width="130" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Disponibilidad" width="240">
        <template #default="{ row }">
          <el-tag :type="availability(row).type" size="small">{{ availability(row).label }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Zona">
        <template #default="{ row }">
          <span v-if="row.zoneNames.length === 0" class="muted">Sin zona asignada</span>
          <el-tag v-for="z in row.zoneNames" :key="z" size="small" type="primary" class="coverage-tag">{{ z }}</el-tag>
          <div v-if="row.coverageCityNames.length" class="cities-line">{{ row.coverageCityNames.join(' · ') }}</div>
        </template>
      </el-table-column>
      <el-table-column label="" width="560">
        <template #default="{ row }">
          <el-button link @click="openZonesDialog(row)">Zona</el-button>
          <el-button link @click="openAssetsDialog(row)">Activos</el-button>
          <el-button link @click="scheduleDialogRef?.open(row)">Horario</el-button>
          <el-button link @click="timeOffDialogRef?.open(row)">Fuera de la oficina</el-button>
          <el-button link @click="visitsDialogRef?.open(row)">Visitas</el-button>
        </template>
      </el-table-column>
    </el-table>

    <TechnicianScheduleDialog ref="scheduleDialogRef" @saved="loadData" />
    <TechnicianTimeOffDialog ref="timeOffDialogRef" @changed="loadData" />
    <TechnicianVisitsDialog ref="visitsDialogRef" />

    <el-dialog v-model="zonesDialogVisible" :title="`Zona — ${selectedTechnician?.fullName}`" width="520px">
      <div v-loading="loadingZones">
        <p class="hint">Normalmente un técnico atiende una sola zona. Su cobertura por municipio sale de las zonas que elijas.</p>
        <el-select v-model="selectedZoneIds" multiple filterable placeholder="Selecciona la zona" style="width: 100%">
          <el-option v-for="z in zones" :key="z.id" :label="z.name" :value="z.id">
            <span>{{ z.name }}</span>
            <span class="option-detail">{{ z.cities.length }} municipio(s)</span>
          </el-option>
        </el-select>
        <p v-if="zones.length === 0" class="muted">Todavía no hay zonas. Créalas en la sección Zonas.</p>
      </div>
      <template #footer>
        <el-button @click="zonesDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingZones" @click="saveZones">Guardar</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="assetsDialogVisible"
      :title="`Activos vinculados — ${selectedTechnician?.fullName}`"
      width="720px"
    >
      <div v-loading="loadingAssets">
        <h4>Vinculados</h4>
        <div class="coverage-list">
          <el-tag
            v-for="a in linkedAssets"
            :key="a.id"
            closable
            class="coverage-tag"
            @close="unlinkAsset(a)"
          >
            {{ a.assetBrandName }} {{ a.model }} — {{ a.serialNumber }}
            <span v-if="a.clientName"> ({{ a.clientName }}<span v-if="a.area"> · {{ a.area }}</span
              ><span v-if="a.cityName"> · {{ a.cityName }}</span
              >)</span
            >
          </el-tag>
          <span v-if="linkedAssets.length === 0" class="muted">
            Este técnico no tiene activos vinculados todavía. No verá equipos en "Lectura de contadores".
          </span>
        </div>

        <el-divider />

        <h4>Vincular nuevo activo</h4>
        <div class="asset-search-row">
          <el-input v-model="assetSearch" placeholder="Buscar por ciudad, cliente, sede, serie o modelo" clearable style="flex: 1" />
          <el-button
            type="primary"
            plain
            :disabled="availableAssets.length === 0"
            :loading="savingAsset"
            @click="linkAllVisible"
          >
            Vincular todos ({{ availableAssets.length }})
          </el-button>
        </div>

        <el-table :data="availableAssets" max-height="320" size="small" empty-text="No hay activos disponibles.">
          <el-table-column label="Equipo">
            <template #default="{ row }">{{ row.assetBrandName }} {{ row.model }} — {{ row.serialNumber }}</template>
          </el-table-column>
          <el-table-column label="Cliente / sede">
            <template #default="{ row }">
              {{ row.currentClientName ?? 'Sin cliente' }}
              <span v-if="row.currentClientLocationName"> — {{ row.currentClientLocationName }}</span>
              <span v-if="row.area"> · {{ row.area }}</span>
            </template>
          </el-table-column>
          <el-table-column label="Ciudad" prop="cityName" width="140" />
          <el-table-column label="" width="100">
            <template #default="{ row }">
              <el-button link :loading="savingAsset" @click="linkAsset(row)">Vincular</el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
      <template #footer>
        <el-button @click="assetsDialogVisible = false">Cerrar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
}

.coverage-tag {
  margin-right: 0.4rem;
  margin-bottom: 0.25rem;
}

.coverage-list {
  min-height: 2rem;
  margin-bottom: 1rem;
}

.cities-line {
  margin-top: 0.25rem;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.hint {
  margin: 0 0 0.75rem;
  font-size: 0.8rem;
  color: var(--el-text-color-secondary);
}

.option-detail {
  float: right;
  margin-left: 1rem;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.asset-search-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}
</style>
