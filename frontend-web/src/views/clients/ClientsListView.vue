<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as clientsApi from '../../api/clients'
import type { ClientDto } from '../../api/types'
import CreateClientDialog from '../../components/CreateClientDialog.vue'

const router = useRouter()

const clients = ref<ClientDto[]>([])
const loading = ref(false)
const createClientDialogRef = ref<InstanceType<typeof CreateClientDialog> | null>(null)

async function loadClients() {
  loading.value = true
  try {
    const { data } = await clientsApi.listClients()
    clients.value = data
  } finally {
    loading.value = false
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
      <el-button type="primary" @click="createClientDialogRef?.open()">Nuevo cliente</el-button>
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

    <CreateClientDialog ref="createClientDialogRef" @created="loadClients" />
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
