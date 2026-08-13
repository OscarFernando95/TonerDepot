<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as ticketsApi from '../../api/tickets'
import * as clientsApi from '../../api/clients'
import * as clientLocationsApi from '../../api/clientLocations'
import * as assetsApi from '../../api/assets'
import { useAuthStore } from '../../stores/auth'
import {
  RoleNames,
  ServiceTicketPriorities,
  ServiceTicketStatusLabels,
  type AssetDto,
  type ClientDto,
  type ClientLocationDto,
  type ServiceTicketDto,
  type ServiceTicketPriorityName
} from '../../api/types'

const router = useRouter()
const auth = useAuthStore()
const isClient = auth.hasRole(RoleNames.Cliente)

const tickets = ref<ServiceTicketDto[]>([])
const clients = ref<ClientDto[]>([])
const locations = ref<ClientLocationDto[]>([])
const assets = ref<AssetDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const priorityOptions = Object.values(ServiceTicketPriorities)

const form = reactive({
  clientId: '',
  clientLocationId: '',
  assetId: '',
  description: '',
  priority: ServiceTicketPriorities.Media as ServiceTicketPriorityName
})

const assetsAtLocation = computed(() =>
  form.clientLocationId ? assets.value.filter((a) => a.currentClientLocationId === form.clientLocationId) : []
)

async function loadTickets() {
  loading.value = true
  try {
    const { data } = await ticketsApi.listTickets()
    tickets.value = data
  } finally {
    loading.value = false
  }
}

async function openCreateDialog() {
  form.clientLocationId = ''
  form.assetId = ''
  form.description = ''
  form.priority = ServiceTicketPriorities.Media
  locations.value = []

  if (!isClient && clients.value.length === 0) {
    const { data } = await clientsApi.listClients()
    clients.value = data
  }
  // El inventario de activos es solo-Staff; un Cliente reporta la falla sin elegir un activo puntual.
  if (!isClient && assets.value.length === 0) {
    const { data } = await assetsApi.listAssets()
    assets.value = data
  }

  // Set last so the watcher below fires once now that clients/locations are ready to load.
  form.clientId = isClient ? (auth.user?.clientId ?? '') : ''

  dialogVisible.value = true
}

watch(
  () => form.clientId,
  async (clientId) => {
    form.clientLocationId = ''
    if (!clientId) {
      locations.value = []
      return
    }
    const { data } = await clientLocationsApi.listClientLocations(clientId)
    locations.value = data
  }
)

async function handleSave() {
  saving.value = true
  try {
    const { data: created } = await ticketsApi.createTicket({
      clientLocationId: form.clientLocationId,
      assetId: form.assetId || null,
      description: form.description,
      priority: form.priority
    })
    ElMessage.success(
      created.technicianName
        ? `Ticket creado y asignado automáticamente a ${created.technicianName}.`
        : 'Ticket creado. No hay técnicos disponibles en la zona por ahora, quedó sin asignar.'
    )
    dialogVisible.value = false
    await loadTickets()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el ticket.')
  } finally {
    saving.value = false
  }
}

function statusTagType(status: string) {
  switch (status) {
    case 'Resuelto':
    case 'Cerrado':
      return 'success'
    case 'Cancelado':
      return 'info'
    case 'Abierto':
    case 'SinAsignar':
      return 'warning'
    default:
      return 'primary'
  }
}

function priorityTagType(priority: string) {
  switch (priority) {
    case 'Critica':
      return 'danger'
    case 'Alta':
      return 'warning'
    default:
      return 'info'
  }
}

function goToDetail(ticket: ServiceTicketDto) {
  router.push({ name: 'ticket-detail', params: { id: ticket.id } })
}

onMounted(loadTickets)
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Tickets de soporte</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo ticket</el-button>
    </div>

    <el-table :data="tickets" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
      <el-table-column label="Cliente / Sede">
        <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
      </el-table-column>
      <el-table-column prop="description" label="Descripción" show-overflow-tooltip />
      <el-table-column label="Prioridad" width="110">
        <template #default="{ row }">
          <el-tag :type="priorityTagType(row.priority)" size="small">{{ row.priority }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Estado" width="130">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">
            {{ ServiceTicketStatusLabels[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Técnico" width="150">
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo ticket" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item v-if="!isClient" label="Cliente">
          <el-select v-model="form.clientId" style="width: 100%" filterable placeholder="Selecciona un cliente">
            <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Sede">
          <el-select
            v-model="form.clientLocationId"
            style="width: 100%"
            filterable
            placeholder="Selecciona una sede"
            :disabled="!isClient && !form.clientId"
          >
            <el-option v-for="l in locations" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="!isClient" label="Activo (opcional)">
          <el-select
            v-model="form.assetId"
            style="width: 100%"
            filterable
            clearable
            placeholder="Selecciona un activo instalado en la sede"
            :disabled="!form.clientLocationId"
          >
            <el-option
              v-for="a in assetsAtLocation"
              :key="a.id"
              :label="`${a.assetBrandName} ${a.model} — ${a.serialNumber}`"
              :value="a.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="Descripción de la falla">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="Prioridad">
          <el-select v-model="form.priority" style="width: 100%">
            <el-option v-for="p in priorityOptions" :key="p" :label="p" :value="p" />
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

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
