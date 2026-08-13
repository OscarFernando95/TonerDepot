<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as citiesApi from '../../api/cities'
import type { CityDto } from '../../api/types'
import { useAuthStore } from '../../stores/auth'
import { RoleNames } from '../../api/types'

const auth = useAuthStore()
const canCreate = auth.hasRole(RoleNames.Administrador)

const cities = ref<CityDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const form = reactive({ name: '', stateOrProvince: '' })

async function loadCities() {
  loading.value = true
  try {
    const { data } = await citiesApi.listCities()
    cities.value = data
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.name = ''
  form.stateOrProvince = ''
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await citiesApi.createCity({
      name: form.name,
      stateOrProvince: form.stateOrProvince || null
    })
    ElMessage.success('Ciudad creada.')
    dialogVisible.value = false
    await loadCities()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear la ciudad.')
  } finally {
    saving.value = false
  }
}

onMounted(loadCities)
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Ciudades</h1>
      <el-button v-if="canCreate" type="primary" @click="openCreateDialog">Nueva ciudad</el-button>
    </div>

    <el-table :data="cities" v-loading="loading" stripe>
      <el-table-column prop="name" label="Nombre" />
      <el-table-column prop="stateOrProvince" label="Departamento / Estado" />
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nueva ciudad" width="400px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="Departamento / Estado">
          <el-input v-model="form.stateOrProvince" />
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
</style>
