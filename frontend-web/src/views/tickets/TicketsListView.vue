<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import * as ticketsApi from '../../api/tickets'
import * as clientsApi from '../../api/clients'
import * as clientLocationsApi from '../../api/clientLocations'
import * as assetsApi from '../../api/assets'
import { useAuthStore } from '../../stores/auth'
import CreateClientDialog from '../../components/CreateClientDialog.vue'
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
const createClientDialogRef = ref<InstanceType<typeof CreateClientDialog> | null>(null)

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

// El selector de Activo solo aparece cuando el cliente elegido tiene contrato (sus equipos están
// catalogados como Asset con hoja de vida). Para un cliente externo no tiene sentido — nunca tendrá
// activos propios que elegir; el técnico puede describir el equipo al cerrar el ticket.
const selectedClientIsContract = ref(true)

async function loadTickets() {
  loading.value = true
  try {
    const { data } = await ticketsApi.listTickets()
    tickets.value = data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los tickets.')
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
  selectedClientIsContract.value = true

  if (!isClient && clients.value.length === 0) {
    const { data } = await clientsApi.listClients()
    clients.value = data
  }
  // El inventario de activos para Staff es global; para un Cliente con contrato se resuelve más abajo
  // (assetsApi.listAssets ya se autofiltra a los activos de su propio cliente en el backend).
  if (!isClient && assets.value.length === 0) {
    const { data } = await assetsApi.listAssets()
    assets.value = data
  }

  // Set last so the watcher below fires once now that clients/locations are ready to load.
  form.clientId = isClient ? (auth.user?.clientId ?? '') : ''

  if (isClient && form.clientId) {
    const { data } = await clientsApi.getClient(form.clientId)
    selectedClientIsContract.value = data.isContractClient
    if (selectedClientIsContract.value && assets.value.length === 0) {
      const { data: assetsData } = await assetsApi.listAssets()
      assets.value = assetsData
    }
  }

  dialogVisible.value = true
}

watch(
  () => form.clientId,
  async (clientId) => {
    if (!isClient) {
      selectedClientIsContract.value = clients.value.find((c) => c.id === clientId)?.isContractClient ?? true
    }
    form.clientLocationId = ''
    if (!clientId) {
      locations.value = []
      return
    }
    const { data } = await clientLocationsApi.listClientLocations(clientId)
    locations.value = data
  }
)

function onClientCreatedFromTicket(newClient: ClientDto) {
  clients.value.push(newClient)
  // Dispara el watch de form.clientId de arriba, que carga las sedes del cliente recién creado — ya
  // no van a estar vacías, porque ahora todo cliente nace con al menos una.
  form.clientId = newClient.id
}

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

    <el-table
      :data="tickets"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      empty-text="No hay tickets registrados."
    >
      <el-table-column
        label="Cliente / Sede"
        sortable
        :sort-method="(a: ServiceTicketDto, b: ServiceTicketDto) => a.clientName.localeCompare(b.clientName)"
      >
        <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140" sortable>
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="description" label="Descripción" show-overflow-tooltip sortable />
      <el-table-column prop="priority" label="Prioridad" width="110" sortable>
        <template #default="{ row }">
          <el-tag :type="priorityTagType(row.priority)" size="small">{{ row.priority }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="Estado" width="130" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">
            {{ ServiceTicketStatusLabels[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="technicianName" label="Técnico" width="150" sortable>
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo ticket" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item v-if="!isClient" label="Cliente">
          <div class="client-select-row">
            <el-select v-model="form.clientId" style="flex: 1" filterable placeholder="Selecciona un cliente">
              <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
            </el-select>
            <el-button :icon="Plus" circle title="Crear cliente" @click="createClientDialogRef?.open()" />
          </div>
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
        <el-form-item v-if="selectedClientIsContract" label="Activo (opcional)">
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

    <CreateClientDialog ref="createClientDialogRef" @created="onClientCreatedFromTicket" />
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

.client-select-row {
  display: flex;
  gap: 0.5rem;
  width: 100%;
}
</style>
