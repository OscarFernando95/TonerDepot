<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as clientsApi from '../../api/clients'
import * as locationsApi from '../../api/clientLocations'
import * as citiesApi from '../../api/cities'
import type { CityDto, ClientDto, ClientLocationDto } from '../../api/types'

const route = useRoute()
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
  contactPhone: ''
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
  contactPhone: ''
})

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
}

async function saveClient() {
  savingClient.value = true
  try {
    const { data } = await clientsApi.updateClient(clientId, {
      name: clientForm.name,
      taxId: clientForm.taxId || null,
      contactName: clientForm.contactName || null,
      contactEmail: clientForm.contactEmail || null,
      contactPhone: clientForm.contactPhone || null
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
  locationForm.cityId = ''
  locationForm.name = ''
  locationForm.address = ''
  locationForm.contactName = ''
  locationForm.contactPhone = ''
  locationDialogVisible.value = true
}

function openEditLocationDialog(location: ClientLocationDto) {
  editingLocationId.value = location.id
  locationForm.cityId = location.cityId
  locationForm.name = location.name
  locationForm.address = location.address
  locationForm.contactName = location.contactName ?? ''
  locationForm.contactPhone = location.contactPhone ?? ''
  locationDialogVisible.value = true
}

async function saveLocation() {
  savingLocation.value = true
  const payload = {
    cityId: locationForm.cityId,
    name: locationForm.name,
    address: locationForm.address,
    contactName: locationForm.contactName || null,
    contactPhone: locationForm.contactPhone || null
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
        <el-form-item label="Ciudad">
          <el-select v-model="locationForm.cityId" style="width: 100%" filterable placeholder="Selecciona una ciudad">
            <el-option v-for="c in cities" :key="c.id" :label="c.name" :value="c.id" />
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
      </el-form>
      <template #footer>
        <el-button @click="locationDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingLocation" @click="saveLocation">Guardar</el-button>
      </template>
    </el-dialog>
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
</style>
