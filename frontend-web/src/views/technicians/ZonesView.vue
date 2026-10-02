<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as zonesApi from '../../api/zones'
import * as citiesApi from '../../api/cities'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import type { CityDto } from '../../api/types'
import type { ZoneDto } from '../../api/zones'

const zones = ref<ZoneDto[]>([])
const cities = ref<CityDto[]>([])
const loading = ref(false)

const nameDialogVisible = ref(false)
const editingZone = ref<ZoneDto | null>(null)
const nameForm = reactive({ name: '' })
const savingName = ref(false)

const citiesDialogVisible = ref(false)
const citiesZone = ref<ZoneDto | null>(null)
const selectedCityIds = ref<string[]>([])
const savingCities = ref(false)

// A qué zona pertenece hoy cada municipio, para avisar en el selector que al elegirlo se MUEVE de zona.
const zoneNameByCity = computed(() => {
  const map = new Map<string, { zoneId: string; zoneName: string }>()
  for (const z of zones.value) {
    for (const c of z.cities) map.set(c.id, { zoneId: z.id, zoneName: z.name })
  }
  return map
})

function movesFrom(cityId: string) {
  const current = zoneNameByCity.value.get(cityId)
  return current && current.zoneId !== citiesZone.value?.id ? current.zoneName : null
}

async function loadData(silent = false) {
  if (!silent) loading.value = true
  try {
    const [zonesRes, citiesRes] = await Promise.all([zonesApi.listZones(), citiesApi.listCities()])
    zones.value = zonesRes.data
    cities.value = citiesRes.data
  } catch (err: any) {
    console.error('ZonesView.loadData failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las zonas.')
  } finally {
    loading.value = false
  }
}

function openNameDialog(zone: ZoneDto | null) {
  editingZone.value = zone
  nameForm.name = zone?.name ?? ''
  nameDialogVisible.value = true
}

async function saveName() {
  const name = nameForm.name.trim()
  if (!name) return
  savingName.value = true
  try {
    if (editingZone.value) {
      await zonesApi.renameZone(editingZone.value.id, name)
      ElMessage.success('Zona renombrada.')
    } else {
      await zonesApi.createZone(name)
      ElMessage.success('Zona creada.')
    }
    nameDialogVisible.value = false
    await loadData(true)
  } catch (err: any) {
    console.error('ZonesView.saveName failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar la zona.')
  } finally {
    savingName.value = false
  }
}

function openCitiesDialog(zone: ZoneDto) {
  citiesZone.value = zone
  selectedCityIds.value = zone.cities.map((c) => c.id)
  citiesDialogVisible.value = true
}

async function saveCities() {
  if (!citiesZone.value) return
  savingCities.value = true
  try {
    await zonesApi.setZoneCities(citiesZone.value.id, selectedCityIds.value)
    ElMessage.success('Municipios actualizados.')
    citiesDialogVisible.value = false
    await loadData(true)
  } catch (err: any) {
    console.error('ZonesView.saveCities failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron guardar los municipios.')
  } finally {
    savingCities.value = false
  }
}

async function removeZone(zone: ZoneDto) {
  try {
    await ElMessageBox.confirm(
      `Se eliminará la zona "${zone.name}" y sus ${zone.cities.length} municipio(s) quedarán sin zona.`,
      'Eliminar zona',
      { confirmButtonText: 'Eliminar', cancelButtonText: 'Cancelar', type: 'warning' }
    )
  } catch {
    return
  }
  try {
    await zonesApi.deleteZone(zone.id)
    ElMessage.success('Zona eliminada.')
    await loadData(true)
  } catch (err: any) {
    console.error('ZonesView.removeZone failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo eliminar la zona.')
  }
}

onMounted(loadData)
useRealtimeUpdates(['Zone', 'Technician'], () => loadData(true))
</script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1>Zonas</h1>
        <p class="muted">Agrupan municipios. Cada técnico atiende una zona y de ahí sale su cobertura; más adelante, también su inventario.</p>
      </div>
      <el-button type="primary" @click="openNameDialog(null)">Nueva zona</el-button>
    </div>

    <el-table v-loading="loading" :data="zones" stripe empty-text="Todavía no hay zonas.">
      <el-table-column prop="name" label="Zona" width="220" sortable />
      <el-table-column label="Municipios">
        <template #default="{ row }">
          <span v-if="row.cities.length === 0" class="muted">Sin municipios</span>
          <el-tag v-for="c in row.cities" :key="c.id" size="small" class="city-tag">{{ c.name }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="technicianCount" label="Técnicos" width="110" />
      <el-table-column label="" width="300">
        <template #default="{ row }">
          <el-button link @click="openCitiesDialog(row)">Municipios</el-button>
          <el-button link @click="openNameDialog(row)">Renombrar</el-button>
          <el-button link type="danger" :disabled="row.technicianCount > 0" @click="removeZone(row)">Eliminar</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="nameDialogVisible" :title="editingZone ? 'Renombrar zona' : 'Nueva zona'" width="420px">
      <el-form label-position="top" @submit.prevent="saveName">
        <el-form-item label="Nombre">
          <el-input v-model="nameForm.name" maxlength="100" placeholder="Ej: Sur del Huila" autofocus />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="nameDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :disabled="!nameForm.name.trim()" :loading="savingName" @click="saveName">Guardar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="citiesDialogVisible" :title="`Municipios — ${citiesZone?.name}`" width="560px">
      <p class="hint">Un municipio pertenece a una sola zona: si eliges uno que hoy está en otra, se mueve a esta.</p>
      <el-select v-model="selectedCityIds" multiple filterable collapse-tags-tooltip placeholder="Busca y selecciona municipios" style="width: 100%">
        <el-option v-for="c in cities" :key="c.id" :label="`${c.name} (${c.stateOrProvince})`" :value="c.id">
          <span>{{ c.name }} <span class="option-detail-inline">{{ c.stateOrProvince }}</span></span>
          <span v-if="movesFrom(c.id)" class="option-detail">hoy en {{ movesFrom(c.id) }}</span>
        </el-option>
      </el-select>
      <template #footer>
        <el-button @click="citiesDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingCities" @click="saveCities">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 1rem;
}

.page-header h1 {
  margin: 0 0 0.25rem;
}

.page-header p {
  margin: 0;
}

.city-tag {
  margin: 0 0.25rem 0.25rem 0;
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
  color: var(--el-color-warning);
}

.option-detail-inline {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}
</style>
