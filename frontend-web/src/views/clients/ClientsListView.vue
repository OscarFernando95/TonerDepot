<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as clientsApi from '../../api/clients'
import type { ClientDto } from '../../api/types'

const router = useRouter()

const clients = ref<ClientDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const form = reactive({
  name: '',
  taxId: '',
  contactName: '',
  contactEmail: '',
  contactPhone: ''
})

async function loadClients() {
  loading.value = true
  try {
    const { data } = await clientsApi.listClients()
    clients.value = data
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.name = ''
  form.taxId = ''
  form.contactName = ''
  form.contactEmail = ''
  form.contactPhone = ''
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await clientsApi.createClient({
      name: form.name,
      taxId: form.taxId || null,
      contactName: form.contactName || null,
      contactEmail: form.contactEmail || null,
      contactPhone: form.contactPhone || null
    })
    ElMessage.success('Cliente creado.')
    dialogVisible.value = false
    await loadClients()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el cliente.')
  } finally {
    saving.value = false
  }
}

async function toggleStatus(client: ClientDto) {
  const nextStatus = !client.isActive
  await ElMessageBox.confirm(
    `¿${nextStatus ? 'Activar' : 'Desactivar'} a ${client.name}?`,
    'Confirmar',
    { type: 'warning' }
  )
  await clientsApi.setClientStatus(client.id, nextStatus)
  ElMessage.success('Cliente actualizado.')
  await loadClients()
}

function goToDetail(client: ClientDto) {
  router.push({ name: 'client-detail', params: { id: client.id } })
}

onMounted(loadClients)
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Clientes</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo cliente</el-button>
    </div>

    <el-table :data="clients" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
      <el-table-column prop="name" label="Nombre" />
      <el-table-column prop="taxId" label="NIT" width="150" />
      <el-table-column prop="contactName" label="Contacto" />
      <el-table-column prop="contactPhone" label="Teléfono" width="140" />
      <el-table-column label="Sedes" width="90">
        <template #default="{ row }">{{ row.locationCount }}</template>
      </el-table-column>
      <el-table-column label="Estado" width="120">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'">{{ row.isActive ? 'Activo' : 'Inactivo' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="120">
        <template #default="{ row }">
          <el-button link @click.stop="toggleStatus(row)">
            {{ row.isActive ? 'Desactivar' : 'Activar' }}
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo cliente" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="NIT">
          <el-input v-model="form.taxId" />
        </el-form-item>
        <el-form-item label="Contacto">
          <el-input v-model="form.contactName" />
        </el-form-item>
        <el-form-item label="Correo de contacto">
          <el-input v-model="form.contactEmail" type="email" />
        </el-form-item>
        <el-form-item label="Teléfono de contacto">
          <el-input v-model="form.contactPhone" />
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

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
