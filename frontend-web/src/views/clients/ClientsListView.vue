<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as clientsApi from '../../api/clients'
import type { ClientDto } from '../../api/types'
import CreateClientDialog from '../../components/CreateClientDialog.vue'

const router = useRouter()

const clients = ref<ClientDto[]>([])
const loading = ref(false)
const createClientDialogRef = ref<InstanceType<typeof CreateClientDialog> | null>(null)

const filters = reactive({ cityName: '' })

const cityOptions = computed(() =>
  [...new Set(clients.value.flatMap((c) => c.cityNames))].sort()
)

const filteredClients = computed(() =>
  clients.value.filter((c) => !filters.cityName || c.cityNames.includes(filters.cityName))
)

const emptyClientsText = computed(() =>
  filters.cityName ? 'No hay clientes en esa ciudad.' : 'No hay clientes registrados.'
)

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

    <div class="filters-bar">
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 220px">
        <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
      </el-select>
    </div>

    <el-table
      :data="filteredClients"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="emptyClientsText"
    >
      <el-table-column prop="name" label="Nombre" sortable />
      <el-table-column prop="isContractClient" label="Tipo" width="110" sortable>
        <template #default="{ row }">
          <el-tag :type="row.isContractClient ? 'primary' : 'warning'" size="small" effect="plain">
            {{ row.isContractClient ? 'Contrato' : 'Externo' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="taxId" label="NIT" width="150" sortable />
      <el-table-column prop="contactName" label="Contacto" sortable />
      <el-table-column prop="contactPhone" label="Teléfono" width="140" sortable />
      <el-table-column prop="locationCount" label="Sedes" width="90" sortable>
        <template #default="{ row }">{{ row.locationCount }}</template>
      </el-table-column>
      <el-table-column prop="isActive" label="Estado" width="120" sortable>
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

.filters-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
