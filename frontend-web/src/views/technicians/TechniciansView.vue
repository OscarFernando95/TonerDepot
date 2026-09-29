<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import * as citiesApi from '../../api/cities'
import * as assetsApi from '../../api/assets'
import { useDepartmentCityCascade } from '../../composables/useDepartmentCityCascade'
import type { TechnicianAssetDto, TechnicianCoverageDto, TechnicianDto } from '../../api/technicians'
import type { AssetDto, CityDto } from '../../api/types'
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
const cities = ref<CityDto[]>([])
const loading = ref(false)

const coverageDialogVisible = ref(false)
const selectedTechnician = ref<TechnicianDto | null>(null)
const coverage = ref<TechnicianCoverageDto[]>([])
const loadingCoverage = ref(false)
const savingCoverage = ref(false)

const addForm = reactive({ cityId: '' })
const { departmentName, departments, citiesInDepartment } = useDepartmentCityCascade(cities, () => {
  addForm.cityId = ''
})

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
    const [techRes, citiesRes] = await Promise.all([techniciansApi.listTechnicians(), citiesApi.listCities()])
    technicians.value = techRes.data
    cities.value = citiesRes.data
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

async function openCoverageDialog(technician: TechnicianDto) {
  selectedTechnician.value = technician
  departmentName.value = ''
  addForm.cityId = ''
  coverageDialogVisible.value = true
  loadingCoverage.value = true
  try {
    const { data } = await techniciansApi.listTechnicianCoverage(technician.id)
    coverage.value = data
  } catch (err: any) {
    console.error('TechniciansView.openCoverageDialog failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar la cobertura.')
  } finally {
    loadingCoverage.value = false
  }
}

async function addCoverage() {
  if (!selectedTechnician.value || !addForm.cityId) return
  savingCoverage.value = true
  try {
    await techniciansApi.addTechnicianCoverage(selectedTechnician.value.id, addForm.cityId)
    ElMessage.success('Cobertura agregada.')
    addForm.cityId = ''
    const { data } = await techniciansApi.listTechnicianCoverage(selectedTechnician.value.id)
    coverage.value = data
    await loadData()
  } catch (err: any) {
    console.error('TechniciansView.addCoverage failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo agregar la cobertura.')
  } finally {
    savingCoverage.value = false
  }
}

async function removeCoverage(item: TechnicianCoverageDto) {
  if (!selectedTechnician.value) return
  try {
    await techniciansApi.removeTechnicianCoverage(selectedTechnician.value.id, item.id)
    ElMessage.success('Cobertura eliminada.')
    const { data } = await techniciansApi.listTechnicianCoverage(selectedTechnician.value.id)
    coverage.value = data
    await loadData()
  } catch (err: any) {
    console.error('TechniciansView.removeCoverage failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo quitar la cobertura.')
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
useRealtimeUpdates(['Technician'], () => loadData())
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
      <el-table-column label="Cobertura">
        <template #default="{ row }">
          <span v-if="row.coverageCityNames.length === 0" class="muted">Sin ciudades asignadas</span>
          <el-tag v-for="c in row.coverageCityNames" :key="c" size="small" class="coverage-tag">{{ c }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="560">
        <template #default="{ row }">
          <el-button link @click="openCoverageDialog(row)">Cobertura</el-button>
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

    <el-dialog v-model="coverageDialogVisible" :title="`Cobertura — ${selectedTechnician?.fullName}`" width="520px">
      <div v-loading="loadingCoverage">
        <div class="coverage-list">
          <el-tag
            v-for="c in coverage"
            :key="c.id"
            closable
            class="coverage-tag"
            @close="removeCoverage(c)"
          >
            {{ c.cityName }}
          </el-tag>
          <span v-if="coverage.length === 0" class="muted">Todavía no tiene ciudades asignadas.</span>
        </div>
        <div class="add-coverage-row">
          <el-select v-model="departmentName" filterable placeholder="Departamento" style="flex: 1">
            <el-option v-for="d in departments" :key="d" :label="d" :value="d" />
          </el-select>
          <el-select
            v-model="addForm.cityId"
            filterable
            :disabled="!departmentName"
            placeholder="Ciudad"
            style="flex: 1"
          >
            <el-option v-for="c in citiesInDepartment" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </div>
        <el-button
          type="primary"
          :loading="savingCoverage"
          :disabled="!addForm.cityId"
          style="width: 100%; margin-top: 0.5rem"
          @click="addCoverage"
        >
          Agregar
        </el-button>
      </div>
      <template #footer>
        <el-button @click="coverageDialogVisible = false">Cerrar</el-button>
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

.add-coverage-row {
  display: flex;
  gap: 0.5rem;
}

.asset-search-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}
</style>
