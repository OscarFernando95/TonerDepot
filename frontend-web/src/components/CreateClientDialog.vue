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
  contactPhone: ''
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
  Object.assign(clientForm, { name: '', taxId: '', contactName: '', contactEmail: '', contactPhone: '' })
  multipleLocations.value = false
  locationForms.value = [emptyLocation()]

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

      <el-checkbox v-model="multipleLocations">Cliente con más de una sede</el-checkbox>

      <el-card
        v-for="(loc, idx) in locationForms"
        :key="idx"
        class="location-card"
        shadow="never"
      >
        <template #header>
          <div class="location-card-header">
            <span>Sede {{ idx + 1 }}</span>
            <el-button
              v-if="multipleLocations && locationForms.length > 1"
              link
              type="danger"
              @click="removeLocation(idx)"
            >
              Quitar sede
            </el-button>
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
          <el-input v-model="loc.name" placeholder="Ej: Sede Principal" />
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
.location-card {
  margin: 1rem 0;
}

.location-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
