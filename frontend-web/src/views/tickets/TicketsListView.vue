<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import * as ticketsApi from '../../api/tickets'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
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
import { useStatusTag } from '../../composables/useStatusTag'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const isClient = auth.hasRole(RoleNames.Cliente)

const clients = ref<ClientDto[]>([])
const locations = ref<ClientLocationDto[]>([])
const assets = ref<AssetDto[]>([])
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

function assetOptionLabel(a: AssetDto) {
  const base = `${a.assetBrandName} ${a.model} — ${a.serialNumber}`
  return a.area ? `${base} · Área: ${a.area}` : base
}

const assetsAtLocation = computed(() => assets.value)

// El selector de Activo solo aparece cuando el cliente elegido tiene contrato (sus equipos están
// catalogados como Asset con hoja de vida). Para un cliente externo no tiene sentido — nunca tendrá
// activos propios que elegir; el técnico puede describir el equipo al cerrar el ticket.
const selectedClientIsContract = ref(true)

// Listado paginado y filtrado EN EL SERVIDOR (AUDIT.md H-11): antes se traía la primera página (200 filas) y se
// mostraba como si fuera todo.
const PAGE_SIZE = 25
const filters = reactive({ status: '', priority: '', clientId: '', search: '' })
const {
  items: tickets,
  total,
  page,
  loading,
  reload: loadTickets
} = usePagedList<ServiceTicketDto, typeof filters>((params) => ticketsApi.listTicketsPaged(params).then((r) => r.data), {
  filters,
  pageSize: PAGE_SIZE,
  label: 'TicketsListView',
  onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los tickets.')
})

// La búsqueda por texto espera a que se deje de escribir: sin esto cada tecla sería una petición.
const searchText = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(searchText, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (filters.search = value.trim()), 350)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

const statusOptions = Object.entries(ServiceTicketStatusLabels).map(([value, label]) => ({ value, label }))
const hasFilters = computed(() => !!(filters.status || filters.priority || filters.clientId || filters.search))
const emptyText = computed(() => (hasFilters.value ? 'Ningún ticket coincide con los filtros.' : 'No hay tickets registrados.'))

// Los clientes para el filtro (solo staff: un Cliente solo ve lo suyo).
const clientFilterOptions = ref<ClientDto[]>([])
if (!isClient) {
  clientsApi
    .listClients()
    .then(({ data }) => (clientFilterOptions.value = data))
    .catch((err) => console.error('TicketsListView: no se pudieron cargar los clientes del filtro', err))
}

function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { status: '', priority: '', clientId: '', search: '' })
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
  assets.value = []

  // Set last so the watcher below fires once now that clients/locations are ready to load.
  form.clientId = isClient ? (auth.user?.clientId ?? '') : ''

  if (isClient && form.clientId) {
    const { data } = await clientsApi.getClient(form.clientId)
    selectedClientIsContract.value = data.isContractClient
  }

  dialogVisible.value = true
}

// "Reportar un problema" del inicio del Cliente llega con ?nuevo=1: abre directo el diálogo de nuevo ticket. El parámetro
// se quita para que recargar la página o volver atrás no vuelva a abrirlo.
onMounted(async () => {
  if (route.query.nuevo === undefined) return
  const { nuevo: _nuevo, ...rest } = route.query
  await router.replace({ query: rest })
  try {
    await openCreateDialog()
  } catch (err) {
    console.error('TicketsListView: no se pudo abrir el diálogo de nuevo ticket', err)
    ElMessage.error('No se pudo abrir el formulario de nuevo ticket.')
  }
})

// Los activos del selector se piden por SEDE, en el servidor (AUDIT.md H-11): antes se cargaba todo el inventario y se
// truncaba en silencio a 200. El backend ya limita a un Cliente a los activos de sus propias sedes.
watch(
  () => form.clientLocationId,
  async (locationId) => {
    form.assetId = ''
    assets.value = []
    if (!locationId) return
    try {
      assets.value = await assetsApi.listAllAssets({ clientLocationId: locationId })
    } catch (err) {
      console.error('TicketsListView: no se pudieron cargar los activos de la sede', err)
      ElMessage.error('No se pudieron cargar los activos de la sede.')
    }
  }
)

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

const { tagType: statusTagType } = useStatusTag('ticket')

const { tagType: priorityTagType } = useStatusTag('ticketPriority')

function goToDetail(ticket: ServiceTicketDto) {
  router.push({ name: 'ticket-detail', params: { id: ticket.id } })
}

// Se actualiza sola cuando algo cambia un ticket (asignación, check-in/out, cierre) — en cualquier pestaña o dispositivo.
useRealtimeUpdates(['Ticket'], () => loadTickets())
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Tickets de soporte</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo ticket</el-button>
    </div>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por descripción, cliente, sede o serie" style="width: 320px" />
      <el-select v-model="filters.status" clearable placeholder="Estado" style="width: 160px">
        <el-option v-for="o in statusOptions" :key="o.value" :label="o.label" :value="o.value" />
      </el-select>
      <el-select v-model="filters.priority" clearable placeholder="Prioridad" style="width: 150px">
        <el-option v-for="p in priorityOptions" :key="p" :label="p" :value="p" />
      </el-select>
      <el-select v-if="!isClient" v-model="filters.clientId" clearable filterable placeholder="Cliente" style="width: 220px">
        <el-option v-for="c in clientFilterOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
      <el-button v-if="hasFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <!-- Sin columnas ordenables: con paginación del servidor un orden por columna solo reordenaría la página visible.
         El servidor entrega siempre lo más reciente primero. -->
    <el-table
      :data="tickets"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="emptyText"
    >
      <el-table-column label="Cliente / Sede">
        <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
      </el-table-column>
      <el-table-column prop="cityName" label="Ciudad" width="140">
        <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="description" label="Descripción" show-overflow-tooltip />
      <el-table-column prop="priority" label="Prioridad" width="110">
        <template #default="{ row }">
          <el-tag :type="priorityTagType(row.priority)" size="small">{{ row.priority }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="Estado" width="130">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">
            {{ ServiceTicketStatusLabels[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="technicianName" label="Técnico" width="150">
        <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="total > PAGE_SIZE"
      v-model:current-page="page"
      class="pager"
      layout="prev, pager, next"
      :page-size="PAGE_SIZE"
      :total="total"
      background
    />

    <el-dialog v-model="dialogVisible" title="Nuevo ticket" width="600px">
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
              :label="assetOptionLabel(a)"
              :value="a.id"
            >
              <div class="asset-option">
                <span class="asset-option__model">{{ a.assetBrandName }} {{ a.model }} — {{ a.serialNumber }}</span>
                <span class="asset-option__area">{{ a.area || 'Sin área' }}</span>
              </div>
            </el-option>
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

.filters-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.results-count {
  margin-left: auto;
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.pager {
  margin-top: 1rem;
  justify-content: flex-end;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}

.client-select-row {
  display: flex;
  gap: 0.5rem;
  width: 100%;
}

/* Dos columnas en la lista de activos: modelo + serie a la izquierda, área a la derecha. */
.asset-option {
  display: grid;
  grid-template-columns: 1fr 9rem;
  gap: 1rem;
  align-items: center;
}

.asset-option__model,
.asset-option__area {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-option__area {
  color: var(--el-text-color-secondary);
}
</style>
