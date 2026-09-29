<script setup lang="ts">
import { nextTick, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import { getCurrentPosition } from '../../composables/useGeolocation'
import LocationPickerDialog from '../../components/LocationPickerDialog.vue'
import * as clientsApi from '../../api/clients'
import * as locationsApi from '../../api/clientLocations'
import * as citiesApi from '../../api/cities'
import { useDepartmentCityCascade } from '../../composables/useDepartmentCityCascade'
import type { CityDto, ClientDto, ClientLocationDto, SupportCoverage } from '../../api/types'

const route = useRoute()
const router = useRouter()
const clientId = route.params.id as string

const client = ref<ClientDto | null>(null)
const locations = ref<ClientLocationDto[]>([])
const cities = ref<CityDto[]>([])
const loading = ref(false)

const clientForm = reactive({
  name: '',
  taxId: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
  isContractClient: true,
  supportCoverage: 'HorarioOficina' as SupportCoverage
})
const savingClient = ref(false)

const locationDialogVisible = ref(false)
const savingLocation = ref(false)
const editingLocationId = ref<string | null>(null)
const locationForm = reactive({
  cityId: '',
  name: '',
  address: '',
  contactName: '',
  contactPhone: '',
  latitude: undefined as number | undefined,
  longitude: undefined as number | undefined
})
const locatingSite = ref(false)
const pickerRef = ref<InstanceType<typeof LocationPickerDialog> | null>(null)

// Elegir la sede en un mapa (con búsqueda de dirección); parte de las coordenadas actuales o de la dirección escrita.
async function pickOnMap() {
  const chosen = await pickerRef.value?.pick(
    { lat: locationForm.latitude, lng: locationForm.longitude },
    [locationForm.address, cities.value.find((c) => c.id === locationForm.cityId)?.name].filter(Boolean).join(', ')
  )
  if (chosen) {
    locationForm.latitude = chosen.lat
    locationForm.longitude = chosen.lng
  }
}

// Llena las coordenadas con la ubicación actual del navegador (útil si quien edita está en la sede).
async function useMyLocation() {
  locatingSite.value = true
  try {
    const position = await getCurrentPosition()
    if (!position) {
      ElMessage.warning('No se pudo obtener la ubicación. Revisa el permiso del navegador.')
      return
    }
    locationForm.latitude = Number(position.latitude.toFixed(6))
    locationForm.longitude = Number(position.longitude.toFixed(6))
  } finally {
    locatingSite.value = false
  }
}

const { departmentName, departments, citiesInDepartment, setDepartmentForCity } = useDepartmentCityCascade(
  cities,
  () => {
    locationForm.cityId = ''
  }
)

async function loadAll() {
  loading.value = true
  try {
    const [clientRes, locationsRes, citiesRes] = await Promise.all([
      clientsApi.getClient(clientId),
      locationsApi.listClientLocations(clientId),
      citiesApi.listCities()
    ])
    client.value = clientRes.data
    locations.value = locationsRes.data
    cities.value = citiesRes.data
    syncClientForm()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el cliente.')
  } finally {
    loading.value = false
  }
}

function syncClientForm() {
  if (!client.value) return
  clientForm.name = client.value.name
  clientForm.taxId = client.value.taxId ?? ''
  clientForm.contactName = client.value.contactName ?? ''
  clientForm.contactEmail = client.value.contactEmail ?? ''
  clientForm.contactPhone = client.value.contactPhone ?? ''
  clientForm.isContractClient = client.value.isContractClient
  clientForm.supportCoverage = client.value.supportCoverage
}

async function saveClient() {
  savingClient.value = true
  try {
    const { data } = await clientsApi.updateClient(clientId, {
      name: clientForm.name,
      taxId: clientForm.taxId || null,
      contactName: clientForm.contactName || null,
      contactEmail: clientForm.contactEmail || null,
      contactPhone: clientForm.contactPhone || null,
      isContractClient: clientForm.isContractClient,
      supportCoverage: clientForm.supportCoverage
    })
    client.value = data
    ElMessage.success('Cliente actualizado.')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo actualizar el cliente.')
  } finally {
    savingClient.value = false
  }
}

async function toggleClientStatus() {
  if (!client.value) return
  const nextStatus = !client.value.isActive
  await ElMessageBox.confirm(`¿${nextStatus ? 'Activar' : 'Desactivar'} este cliente?`, 'Confirmar', {
    type: 'warning'
  })
  const { data } = await clientsApi.setClientStatus(clientId, nextStatus)
  client.value = data
  ElMessage.success('Cliente actualizado.')
}

function openCreateLocationDialog() {
  editingLocationId.value = null
  departmentName.value = ''
  locationForm.cityId = ''
  locationForm.name = ''
  locationForm.address = ''
  locationForm.contactName = ''
  locationForm.contactPhone = ''
  locationForm.latitude = undefined
  locationForm.longitude = undefined
  locationDialogVisible.value = true
}

async function openEditLocationDialog(location: ClientLocationDto) {
  editingLocationId.value = location.id
  // Pre-poblar el departamento a partir de la ciudad ya asignada — si no, el select de Departamento
  // aparece vacío al editar aunque la sede ya tenga ciudad. El watch de departmentName limpia cityId
  // de forma asíncrona (próximo tick) — hay que esperarlo antes de fijar el valor real, o lo pisaría
  // después de asignarlo acá.
  setDepartmentForCity(location.cityId)
  await nextTick()
  locationForm.cityId = location.cityId
  locationForm.name = location.name
  locationForm.address = location.address
  locationForm.contactName = location.contactName ?? ''
  locationForm.contactPhone = location.contactPhone ?? ''
  locationForm.latitude = location.latitude ?? undefined
  locationForm.longitude = location.longitude ?? undefined
  locationDialogVisible.value = true
}

async function saveLocation() {
  savingLocation.value = true
  const payload = {
    cityId: locationForm.cityId,
    name: locationForm.name,
    address: locationForm.address,
    contactName: locationForm.contactName || null,
    contactPhone: locationForm.contactPhone || null,
    latitude: locationForm.latitude ?? null,
    longitude: locationForm.longitude ?? null
  }
  try {
    if (editingLocationId.value) {
      await locationsApi.updateClientLocation(clientId, editingLocationId.value, payload)
    } else {
      await locationsApi.createClientLocation(clientId, payload)
    }
    ElMessage.success('Sede guardada.')
    locationDialogVisible.value = false
    const { data } = await locationsApi.listClientLocations(clientId)
    locations.value = data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar la sede.')
  } finally {
    savingLocation.value = false
  }
}

async function toggleLocationStatus(location: ClientLocationDto) {
  const nextStatus = !location.isActive
  await ElMessageBox.confirm(
    `¿${nextStatus ? 'Activar' : 'Desactivar'} la sede "${location.name}"?`,
    'Confirmar',
    { type: 'warning' }
  )
  await locationsApi.setClientLocationStatus(clientId, location.id, nextStatus)
  ElMessage.success('Sede actualizada.')
  const { data } = await locationsApi.listClientLocations(clientId)
  locations.value = data
}

onMounted(loadAll)
</script>

<template>
  <div v-loading="loading">
    <template v-if="client">
      <div class="page-header">
        <el-button :icon="ArrowLeft" circle title="Volver a Clientes" @click="router.push({ name: 'clients' })" />
        <h1>{{ client.name }}</h1>
        <el-tag :type="client.isActive ? 'success' : 'info'">{{ client.isActive ? 'Activo' : 'Inactivo' }}</el-tag>
      </div>

      <el-card class="section-card">
        <template #header>Datos del cliente</template>
        <el-form :model="clientForm" label-position="top">
          <div class="form-grid">
            <el-form-item label="Nombre">
              <el-input v-model="clientForm.name" />
            </el-form-item>
            <el-form-item label="NIT">
              <el-input v-model="clientForm.taxId" />
            </el-form-item>
            <el-form-item label="Contacto">
              <el-input v-model="clientForm.contactName" />
            </el-form-item>
            <el-form-item label="Correo de contacto">
              <el-input v-model="clientForm.contactEmail" type="email" />
            </el-form-item>
            <el-form-item label="Teléfono de contacto">
              <el-input v-model="clientForm.contactPhone" />
            </el-form-item>
          </div>
          <el-form-item>
            <el-checkbox v-model="clientForm.isContractClient">Cliente con contrato</el-checkbox>
          </el-form-item>
          <el-form-item label="Cobertura de soporte">
            <el-radio-group v-model="clientForm.supportCoverage">
              <el-radio-button value="HorarioOficina">Horario de oficina</el-radio-button>
              <el-radio-button value="Continuo24x7">24/7</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <p class="contract-hint">
            {{
              clientForm.supportCoverage === 'Continuo24x7'
                ? 'El SLA cuenta horas corridas y se le puede asignar un técnico a cualquier hora (menos si está fuera de la oficina).'
                : 'El SLA cuenta solo horas hábiles (horario del técnico, sin festivos ni permisos) y solo se le asignan técnicos en horario.'
            }}
          </p>
        </el-form>
        <div class="section-actions">
          <el-button @click="toggleClientStatus">
            {{ client.isActive ? 'Desactivar cliente' : 'Activar cliente' }}
          </el-button>
          <el-button type="primary" :loading="savingClient" @click="saveClient">Guardar cambios</el-button>
        </div>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-card-header">
            <span>Sedes</span>
            <el-button type="primary" size="small" @click="openCreateLocationDialog">Nueva sede</el-button>
          </div>
        </template>

        <el-table :data="locations" stripe>
          <el-table-column prop="name" label="Nombre" />
          <el-table-column prop="cityName" label="Ciudad" width="140" />
          <el-table-column prop="address" label="Dirección" />
          <el-table-column label="Coordenadas" width="170">
            <template #default="{ row }">
              <span v-if="row.latitude != null && row.longitude != null">{{ row.latitude.toFixed(5) }}, {{ row.longitude.toFixed(5) }}</span>
              <span v-else class="muted">Sin coordenadas</span>
            </template>
          </el-table-column>
          <el-table-column prop="contactName" label="Contacto" />
          <el-table-column prop="contactPhone" label="Teléfono" width="140" />
          <el-table-column label="Estado" width="110">
            <template #default="{ row }">
              <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? 'Activa' : 'Inactiva' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="" width="180">
            <template #default="{ row }">
              <el-button link @click="openEditLocationDialog(row)">Editar</el-button>
              <el-button link @click="toggleLocationStatus(row)">
                {{ row.isActive ? 'Desactivar' : 'Activar' }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="locationDialogVisible" :title="editingLocationId ? 'Editar sede' : 'Nueva sede'" width="480px">
      <el-form :model="locationForm" label-position="top">
        <el-form-item label="Departamento">
          <el-select v-model="departmentName" style="width: 100%" filterable placeholder="Selecciona un departamento">
            <el-option v-for="d in departments" :key="d" :label="d" :value="d" />
          </el-select>
        </el-form-item>
        <el-form-item label="Ciudad">
          <el-select
            v-model="locationForm.cityId"
            style="width: 100%"
            filterable
            :disabled="!departmentName"
            placeholder="Selecciona una ciudad"
          >
            <el-option v-for="c in citiesInDepartment" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Nombre de la sede">
          <el-input v-model="locationForm.name" placeholder="Ej: Sede Principal" />
        </el-form-item>
        <el-form-item label="Dirección">
          <el-input v-model="locationForm.address" />
        </el-form-item>
        <el-form-item label="Contacto">
          <el-input v-model="locationForm.contactName" />
        </el-form-item>
        <el-form-item label="Teléfono">
          <el-input v-model="locationForm.contactPhone" />
        </el-form-item>
        <el-form-item label="Coordenadas de la sede (opcional)">
          <div class="coords-row">
            <el-input-number v-model="locationForm.latitude" :min="-90" :max="90" :precision="6" :controls="false" placeholder="Latitud" />
            <el-input-number v-model="locationForm.longitude" :min="-180" :max="180" :precision="6" :controls="false" placeholder="Longitud" />
            <el-button type="primary" plain @click="pickOnMap">Elegir en el mapa</el-button>
            <el-button :loading="locatingSite" @click="useMyLocation">Usar mi ubicación</el-button>
          </div>
          <p class="coords-hint">Con ellas se verifica que el técnico llegó al hacer check-in. Van juntas: latitud y longitud.</p>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="locationDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingLocation" @click="saveLocation">Guardar</el-button>
      </template>
    </el-dialog>
    <LocationPickerDialog ref="pickerRef" />
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
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
.contract-hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}
.coords-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.coords-hint {
  color: var(--el-text-color-secondary);
  font-size: 0.8rem;
  margin: 0.25rem 0 0;
}

.muted {
  color: #9ca3af;
}
</style>
