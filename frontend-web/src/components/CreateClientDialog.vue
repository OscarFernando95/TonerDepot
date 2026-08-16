<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as clientsApi from '../api/clients'
import * as citiesApi from '../api/cities'
import type { CityDto, ClientDto } from '../api/types'

const emit = defineEmits<{ created: [client: ClientDto] }>()

const cities = ref<CityDto[]>([])
const dialogVisible = ref(false)
const saving = ref(false)

const clientForm = reactive({
  name: '',
  taxId: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
  isContractClient: true
})

const multipleLocations = ref(false)

interface LocationSubForm {
  departmentName: string
  cityId: string
  name: string
  address: string
  contactName: string
  contactPhone: string
}

function emptyLocation(): LocationSubForm {
  return { departmentName: '', cityId: '', name: '', address: '', contactName: '', contactPhone: '' }
}

const locationForms = ref<LocationSubForm[]>([emptyLocation()])

// Mismo patrón (repetido, sin composable) que se usa en UsersView.vue y ClientDetailView.vue.
function departmentsFor() {
  return [...new Set(cities.value.map((c) => c.stateOrProvince))].sort()
}
function citiesInDepartment(departmentName: string) {
  return cities.value.filter((c) => c.stateOrProvince === departmentName)
}

// Un solo watch sobre el array (no N watches individuales) para resetear la ciudad de la fila cuyo
// departamento cambió.
watch(
  () => locationForms.value.map((l) => l.departmentName),
  (newDepts, oldDepts) => {
    locationForms.value.forEach((loc, i) => {
      if (newDepts[i] !== oldDepts?.[i]) {
        loc.cityId = ''
      }
    })
  }
)

watch(multipleLocations, (checked) => {
  if (!checked && locationForms.value.length > 1) {
    locationForms.value = [locationForms.value[0]]
  }
})

function addLocation() {
  locationForms.value.push(emptyLocation())
}

function removeLocation(index: number) {
  if (locationForms.value.length > 1) {
    locationForms.value.splice(index, 1)
  }
}

async function open() {
  Object.assign(clientForm, { name: '', taxId: '', contactName: '', contactEmail: '', contactPhone: '', isContractClient: true })
  multipleLocations.value = false
  locationForms.value = [emptyLocation()]
  // La sede principal se sobreentiende: no se le pregunta nombre ni contacto propio (el del cliente ya
  // cubre eso arriba). Solo las sedes adicionales piden nombre y contacto, para poder distinguirlas.
  locationForms.value[0].name = 'Sede Principal'

  if (cities.value.length === 0) {
    const { data } = await citiesApi.listCities()
    cities.value = data
  }

  dialogVisible.value = true
}

defineExpose({ open })

async function handleSave() {
  saving.value = true
  try {
    const { data: created } = await clientsApi.createClient({
      name: clientForm.name,
      taxId: clientForm.taxId || null,
      contactName: clientForm.contactName || null,
      contactEmail: clientForm.contactEmail || null,
      contactPhone: clientForm.contactPhone || null,
      isContractClient: clientForm.isContractClient,
      locations: locationForms.value.map((l) => ({
        cityId: l.cityId,
        name: l.name,
        address: l.address,
        contactName: l.contactName || null,
        contactPhone: l.contactPhone || null
      }))
    })
    ElMessage.success('Cliente creado.')
    dialogVisible.value = false
    emit('created', created)
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el cliente.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog v-model="dialogVisible" title="Nuevo cliente" width="560px">
    <el-form :model="clientForm" label-position="top">
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
      <el-form-item>
        <el-checkbox v-model="clientForm.isContractClient">Cliente con contrato</el-checkbox>
      </el-form-item>
      <p class="contract-hint">
        {{
          clientForm.isContractClient
            ? 'Sus activos quedan bajo contrato de alquiler: los tickets se ligan a un activo real, con su hoja de vida.'
            : 'Cliente externo: pide servicio sobre equipos propios. El técnico puede registrar marca/modelo/contador del equipo de forma opcional al cerrar el ticket.'
        }}
      </p>

      <el-divider content-position="left">Ubicación</el-divider>

      <el-form-item label="Departamento">
        <el-select
          v-model="locationForms[0].departmentName"
          style="width: 100%"
          filterable
          placeholder="Selecciona un departamento"
        >
          <el-option v-for="d in departmentsFor()" :key="d" :label="d" :value="d" />
        </el-select>
      </el-form-item>
      <el-form-item label="Ciudad">
        <el-select
          v-model="locationForms[0].cityId"
          style="width: 100%"
          filterable
          :disabled="!locationForms[0].departmentName"
          placeholder="Selecciona una ciudad"
        >
          <el-option
            v-for="c in citiesInDepartment(locationForms[0].departmentName)"
            :key="c.id"
            :label="c.name"
            :value="c.id"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="Dirección">
        <el-input v-model="locationForms[0].address" />
      </el-form-item>

      <el-checkbox v-model="multipleLocations" class="more-locations">Cliente con más de una sede</el-checkbox>

      <el-card
        v-for="(loc, idx) in locationForms.slice(1)"
        :key="idx + 1"
        class="location-card"
        shadow="never"
      >
        <template #header>
          <div class="location-card-header">
            <span>Sede {{ idx + 2 }}</span>
            <el-button link type="danger" @click="removeLocation(idx + 1)">Quitar sede</el-button>
          </div>
        </template>

        <el-form-item label="Departamento">
          <el-select v-model="loc.departmentName" style="width: 100%" filterable placeholder="Selecciona un departamento">
            <el-option v-for="d in departmentsFor()" :key="d" :label="d" :value="d" />
          </el-select>
        </el-form-item>
        <el-form-item label="Ciudad">
          <el-select
            v-model="loc.cityId"
            style="width: 100%"
            filterable
            :disabled="!loc.departmentName"
            placeholder="Selecciona una ciudad"
          >
            <el-option v-for="c in citiesInDepartment(loc.departmentName)" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Nombre de la sede">
          <el-input v-model="loc.name" placeholder="Ej: Sede Norte" />
        </el-form-item>
        <el-form-item label="Dirección">
          <el-input v-model="loc.address" />
        </el-form-item>
        <el-form-item label="Contacto (opcional)">
          <el-input v-model="loc.contactName" />
        </el-form-item>
        <el-form-item label="Teléfono (opcional)">
          <el-input v-model="loc.contactPhone" />
        </el-form-item>
      </el-card>

      <el-button v-if="multipleLocations" @click="addLocation">Agregar otra sede</el-button>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false">Cancelar</el-button>
      <el-button type="primary" :loading="saving" @click="handleSave">Guardar</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.more-locations {
  display: block;
  margin: 0.75rem 0 0.25rem;
}

.contract-hint {
  color: var(--el-text-color-secondary);
  font-size: 0.8rem;
  margin: -0.5rem 0 1rem;
}

.location-card {
  margin: 1rem 0;
}

.location-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
