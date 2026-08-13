<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { User } from '@element-plus/icons-vue'
import * as usersApi from '../../api/users'
import * as clientsApi from '../../api/clients'
import * as citiesApi from '../../api/cities'
import { RoleNames, type CityDto, type ClientDto, type UserDto } from '../../api/types'

const users = ref<UserDto[]>([])
const clients = ref<ClientDto[]>([])
const cities = ref<CityDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const roleOptions = Object.values(RoleNames)

const form = reactive({
  cedula: '',
  email: '',
  fullName: '',
  phone: '',
  address: '',
  cityId: '' as string,
  roleName: RoleNames.Coordinador as string,
  clientId: '' as string
})

const requiresClient = computed(() => form.roleName === RoleNames.Cliente)

// Cascada Departamento→Ciudad: departmentName es local al formulario, no se manda al backend.
const departmentName = ref('')
const departments = computed(() => [...new Set(cities.value.map((c) => c.stateOrProvince))].sort())
const citiesInDepartment = computed(() => cities.value.filter((c) => c.stateOrProvince === departmentName.value))
watch(departmentName, () => {
  form.cityId = ''
})

async function loadData() {
  loading.value = true
  try {
    const [usersRes, clientsRes, citiesRes] = await Promise.all([
      usersApi.listUsers(),
      clientsApi.listClients(),
      citiesApi.listCities()
    ])
    users.value = usersRes.data
    clients.value = clientsRes.data
    cities.value = citiesRes.data
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.cedula = ''
  form.email = ''
  form.fullName = ''
  form.phone = ''
  form.address = ''
  form.cityId = ''
  departmentName.value = ''
  form.roleName = RoleNames.Coordinador
  form.clientId = ''
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await usersApi.createUser({
      cedula: form.cedula,
      email: form.email || null,
      fullName: form.fullName,
      phone: form.phone,
      address: form.address,
      cityId: form.cityId,
      roleName: form.roleName as any,
      clientId: requiresClient.value ? form.clientId : null
    })
    ElMessage.success('Usuario creado. Contraseña inicial: Toner123')
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el usuario.')
  } finally {
    saving.value = false
  }
}

async function toggleStatus(user: UserDto) {
  const nextStatus = !user.isActive
  await ElMessageBox.confirm(
    `¿${nextStatus ? 'Activar' : 'Desactivar'} a ${user.fullName}?`,
    'Confirmar',
    { type: 'warning' }
  )
  await usersApi.setUserStatus(user.id, nextStatus)
  ElMessage.success('Usuario actualizado.')
  await loadData()
}

async function resetPassword(user: UserDto) {
  await ElMessageBox.confirm(
    `¿Restablecer la contraseña de ${user.fullName} a la contraseña genérica?`,
    'Confirmar',
    { type: 'warning' }
  )
  await usersApi.resetUserPassword(user.id)
  ElMessage.success('Contraseña restablecida a Toner123. El usuario deberá cambiarla en su próximo inicio de sesión.')
  await loadData()
}

onMounted(loadData)
</script>

<template>
  <div>
    <div class="page-header">
      <h1><el-icon><User /></el-icon> Usuarios</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo usuario</el-button>
    </div>

    <el-table :data="users" v-loading="loading" stripe>
      <el-table-column prop="fullName" label="Nombre" />
      <el-table-column prop="cedula" label="Cédula" width="130" />
      <el-table-column label="Correo">
        <template #default="{ row }">{{ row.email ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="roleName" label="Rol" width="140" />
      <el-table-column label="Estado" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'">{{ row.isActive ? 'Activo' : 'Inactivo' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="220">
        <template #default="{ row }">
          <el-button link @click="toggleStatus(row)">
            {{ row.isActive ? 'Desactivar' : 'Activar' }}
          </el-button>
          <el-button link @click="resetPassword(row)">Restablecer contraseña</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo usuario" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Nombre completo">
          <el-input v-model="form.fullName" />
        </el-form-item>
        <el-form-item label="Cédula">
          <el-input v-model="form.cedula" />
        </el-form-item>
        <el-form-item label="Celular">
          <el-input v-model="form.phone" />
        </el-form-item>
        <el-form-item label="Correo (opcional)">
          <el-input v-model="form.email" type="email" />
        </el-form-item>
        <el-form-item label="Dirección">
          <el-input v-model="form.address" />
        </el-form-item>
        <el-form-item label="Departamento">
          <el-select v-model="departmentName" style="width: 100%" filterable placeholder="Selecciona un departamento">
            <el-option v-for="d in departments" :key="d" :label="d" :value="d" />
          </el-select>
        </el-form-item>
        <el-form-item label="Ciudad">
          <el-select
            v-model="form.cityId"
            style="width: 100%"
            filterable
            :disabled="!departmentName"
            placeholder="Selecciona una ciudad"
          >
            <el-option v-for="c in citiesInDepartment" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Rol">
          <el-select v-model="form.roleName" style="width: 100%">
            <el-option v-for="r in roleOptions" :key="r" :label="r" :value="r" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="requiresClient" label="Cliente">
          <el-select v-model="form.clientId" style="width: 100%" filterable placeholder="Selecciona un cliente">
            <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
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

.page-header h1 {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
</style>
