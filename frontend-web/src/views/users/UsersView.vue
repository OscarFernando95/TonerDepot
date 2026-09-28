<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
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

// null = creando un usuario nuevo; con valor = editando ese usuario (rol y cliente no se tocan
// en edición, ver UpdateUserRequest en el backend).
const editingUserId = ref<string | null>(null)

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

// Al precargar el formulario de edición seteamos departmento y ciudad a la vez; sin esta guarda,
// el watcher de más abajo borraría la ciudad que acabamos de precargar.
let suppressCityReset = false
watch(departmentName, () => {
  if (suppressCityReset) return
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
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los usuarios.')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  editingUserId.value = null
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

async function openEditDialog(user: UserDto) {
  editingUserId.value = user.id
  form.cedula = user.cedula
  form.email = user.email ?? ''
  form.fullName = user.fullName
  form.phone = user.phone ?? ''
  form.address = user.address ?? ''
  form.roleName = user.roleName
  form.clientId = user.clientId ?? ''

  const city = cities.value.find((c) => c.id === user.cityId)
  suppressCityReset = true
  departmentName.value = city?.stateOrProvince ?? ''
  form.cityId = user.cityId ?? ''
  await nextTick()
  suppressCityReset = false

  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    if (editingUserId.value) {
      await usersApi.updateUser(editingUserId.value, {
        cedula: form.cedula,
        email: form.email || null,
        fullName: form.fullName,
        phone: form.phone,
        address: form.address,
        cityId: form.cityId
      })
      ElMessage.success('Usuario actualizado.')
    } else {
      const { data } = await usersApi.createUser({
        cedula: form.cedula,
        email: form.email || null,
        fullName: form.fullName,
        phone: form.phone,
        address: form.address,
        cityId: form.cityId,
        roleName: form.roleName as any,
        clientId: requiresClient.value ? form.clientId : null
      })
      ElMessage.success(`Usuario creado. Contraseña inicial: ${data.generatedPassword}`)
    }
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? (editingUserId.value ? 'No se pudo actualizar el usuario.' : 'No se pudo crear el usuario.'))
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

async function revokeSessions(user: UserDto) {
  await ElMessageBox.confirm(
    `¿Cerrar todas las sesiones activas de ${user.fullName}? Tendrá que iniciar sesión de nuevo.`,
    'Confirmar',
    { type: 'warning' }
  )
  const { data } = await usersApi.revokeUserSessions(user.id)
  ElMessage.success(data.revoked > 0 ? `Se cerraron ${data.revoked} sesión(es).` : 'El usuario no tenía sesiones activas.')
}

async function resetPassword(user: UserDto) {
  await ElMessageBox.confirm(
    `¿Restablecer la contraseña de ${user.fullName} a una nueva generada?`,
    'Confirmar',
    { type: 'warning' }
  )
  const { data } = await usersApi.resetUserPassword(user.id)
  ElMessage.success(`Contraseña restablecida: ${data.generatedPassword}. El usuario deberá cambiarla en su próximo inicio de sesión.`)
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

    <el-table :data="users" v-loading="loading" stripe empty-text="No hay usuarios registrados.">
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
      <el-table-column label="Acciones" width="330">
        <template #default="{ row }">
          <div class="user-actions">
            <el-button link class="user-actions-toggle" @click="toggleStatus(row)">
              {{ row.isActive ? 'Desactivar' : 'Activar' }}
            </el-button>
            <el-button link @click="resetPassword(row)">Restablecer contraseña</el-button>
            <el-button link @click="revokeSessions(row)">Cerrar sesiones</el-button>
            <el-button link @click="openEditDialog(row)">Editar</el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingUserId ? 'Editar usuario' : 'Nuevo usuario'" width="480px">
      <el-form :model="form" label-position="top">
        <p v-if="editingUserId" class="edit-role-note">
          Rol: <strong>{{ form.roleName }}</strong>
          <template v-if="requiresClient"> · Cliente: <strong>{{ clients.find((c) => c.id === form.clientId)?.name ?? '—' }}</strong></template>
          — no editable acá.
        </p>
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
        <template v-if="!editingUserId">
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
        </template>
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

.user-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.user-actions :deep(.el-button + .el-button) {
  margin-left: 0;
}

/* Fixed width so "Restablecer contraseña" and "Editar" start at the same x
   regardless of whether this row says "Activar" or "Desactivar". */
.user-actions-toggle {
  min-width: 66px;
  justify-content: flex-start;
}

.edit-role-note {
  margin: -0.5rem 0 1rem;
  padding: 0.5rem 0.75rem;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}
</style>
