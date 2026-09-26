<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Calendar, Printer, Tickets, Tools } from '@element-plus/icons-vue'
import * as selfApi from '../../api/technicianSelf'
import * as ticketsApi from '../../api/tickets'
import * as ordersApi from '../../api/maintenanceOrders'
import { ServiceTicketStatusLabels, type ServiceTicketDto } from '../../api/types'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto, type MaintenanceScheduleDto } from '../../api/types'

const status = ref<selfApi.TechnicianSelfStatusDto | null>(null)
const tickets = ref<ServiceTicketDto[]>([])
const coverageTickets = ref<ServiceTicketDto[]>([])
const orders = ref<MaintenanceOrderDto[]>([])
const coverageOrders = ref<MaintenanceOrderDto[]>([])
const pendingInstallations = ref<selfApi.PendingInstallationDto[]>([])
const coverageSchedules = ref<MaintenanceScheduleDto[]>([])
const loading = ref(false)
const checkingIn = ref<string | null>(null)
const claiming = ref<string | null>(null)
const checkingOut = ref(false)

const ticketsTab = ref('mine')
const ordersTab = ref('mine')

const checkoutForm = reactive({
  resolved: true,
  notes: '',
  area: '',
  initialCounterValue: undefined as number | undefined,
  initialCounterDate: '',
  generalMaintenanceDone: false,
  // true: "mantenimiento de unidades realizado" (revela existingConsumablesPrints). false: "insumos
  // nuevos" — mutuamente excluyentes.
  unitsMaintenanceDone: undefined as boolean | undefined,
  existingConsumablesPrints: undefined as number | undefined,
  // Solo tienen efecto cerrando un ticket sin activo asociado (cliente externo) — de forma opcional.
  externalAssetBrand: '',
  externalAssetModel: '',
  externalAssetCounter: undefined as number | undefined
})

const installationFilter = reactive({ clientId: '' })

const isBusy = computed(() => status.value?.status === 'Ocupado')

const activeTicket = computed(() =>
  status.value?.activeServiceTicketId ? tickets.value.find((t) => t.id === status.value!.activeServiceTicketId) : null
)
const activeOrder = computed(() =>
  status.value?.activeMaintenanceOrderId
    ? orders.value.find((o) => o.id === status.value!.activeMaintenanceOrderId)
    : null
)
const activeInstallation = computed(() =>
  status.value?.activeAssetInstallationId
    ? pendingInstallations.value.find((i) => i.assetId === status.value!.activeAssetInstallationId)
    : null
)

// Ticket de un cliente externo (sin Asset catalogado) — habilita los campos opcionales del equipo.
const isExternalTicket = computed(() => !!activeTicket.value && !activeTicket.value.assetId)

const checkInableTickets = computed(() =>
  tickets.value.filter((t) => t.status === 'Asignado' || t.status === 'EnProceso')
)
const checkInableOrders = computed(() =>
  orders.value.filter((o) => o.status === 'Asignada' || o.status === 'EnProceso')
)

// EnProceso significa que otro técnico ya está trabajando en ello — ahí ya no se puede tomar, solo
// queda como información. Cualquier otro estado activo (Abierto/SinAsignar/Asignado/Pendiente) sí.
function isClaimable(itemStatus: string) {
  return itemStatus !== 'EnProceso'
}

const installationClients = computed(() => {
  const seen = new Map<string, string>()
  for (const i of pendingInstallations.value) {
    seen.set(i.clientId, i.clientName)
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
})

const filteredInstallations = computed(() =>
  pendingInstallations.value.filter((i) => !installationFilter.clientId || i.clientId === installationFilter.clientId)
)

// Restringe el selector de fecha a la vigencia del contrato cuando se conoce (instalaciones) — evita
// que el técnico elija una fecha que el backend va a rechazar de todos modos (ver
// TechnicianCheckInService.EnsureCounterDateWithinContractAsync). Comparación por día calendario local,
// no por instante, para que no dependa de a qué hora del día se abra el selector.
function toDayNumber(d: Date) {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}

const counterDateBounds = computed(() => {
  const inst = activeInstallation.value
  if (!inst?.contractStartDate) return null
  return {
    start: new Date(inst.contractStartDate),
    end: inst.contractEndDate ? new Date(inst.contractEndDate) : null
  }
})

function isCounterDateDisabled(date: Date) {
  const bounds = counterDateBounds.value
  if (!bounds) return false
  const day = toDayNumber(date)
  if (day < toDayNumber(bounds.start)) return true
  if (bounds.end && day > toDayNumber(bounds.end)) return true
  return false
}

const checkoutFormValid = computed(() => {
  if (!checkoutForm.resolved) {
    return true
  }
  if (activeInstallation.value) {
    return (
      !!checkoutForm.area.trim() &&
      checkoutForm.initialCounterValue !== undefined &&
      checkoutForm.unitsMaintenanceDone !== undefined
    )
  }
  if (activeOrder.value) {
    return checkoutForm.initialCounterValue !== undefined
  }
  return true
})

async function loadAll() {
  loading.value = true
  try {
    const [statusRes, ticketsRes, coverageTicketsRes, ordersRes, coverageOrdersRes, installationsRes, coverageSchedulesRes] =
      await Promise.all([
        selfApi.getMyStatus(),
        ticketsApi.listTickets(),
        selfApi.listCoverageTickets(),
        ordersApi.listMaintenanceOrders(),
        selfApi.listCoverageMaintenanceOrders(),
        selfApi.listPendingInstallations(),
        selfApi.listCoverageSchedules()
      ])
    status.value = statusRes.data
    tickets.value = ticketsRes.data
    coverageTickets.value = coverageTicketsRes.data
    orders.value = ordersRes.data
    coverageOrders.value = coverageOrdersRes.data
    pendingInstallations.value = installationsRes.data
    coverageSchedules.value = coverageSchedulesRes.data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar tu trabajo.')
  } finally {
    loading.value = false
  }
}

async function checkInTicket(ticket: ServiceTicketDto) {
  checkingIn.value = ticket.id
  try {
    await selfApi.checkIn({ serviceTicketId: ticket.id })
    ElMessage.success('Check-in registrado.')
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo hacer check-in.')
  } finally {
    checkingIn.value = null
  }
}

async function checkInOrder(order: MaintenanceOrderDto) {
  checkingIn.value = order.id
  try {
    await selfApi.checkIn({ maintenanceOrderId: order.id })
    ElMessage.success('Check-in registrado.')
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo hacer check-in.')
  } finally {
    checkingIn.value = null
  }
}

async function checkInInstallation(installation: selfApi.PendingInstallationDto) {
  checkingIn.value = installation.assetId
  try {
    await selfApi.checkIn({ assetId: installation.assetId })
    ElMessage.success('Check-in registrado.')
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo hacer check-in.')
  } finally {
    checkingIn.value = null
  }
}

async function claimTicketRow(ticket: ServiceTicketDto) {
  claiming.value = ticket.id
  try {
    await selfApi.claimTicket(ticket.id)
    ElMessage.success('Ticket tomado — ya está asignado a ti.')
    ticketsTab.value = 'mine'
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo tomar el ticket.')
  } finally {
    claiming.value = null
  }
}

async function claimOrderRow(order: MaintenanceOrderDto) {
  claiming.value = order.id
  try {
    await selfApi.claimMaintenanceOrder(order.id)
    ElMessage.success('Orden tomada — ya está asignada a ti.')
    ordersTab.value = 'mine'
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo tomar la orden.')
  } finally {
    claiming.value = null
  }
}

async function doCheckOut() {
  if (!checkoutFormValid.value) {
    ElMessage.warning('Completa los campos obligatorios para cerrar la visita.')
    return
  }

  checkingOut.value = true
  try {
    await selfApi.checkOut({
      resolved: checkoutForm.resolved,
      notes: checkoutForm.notes || null,
      area: activeInstallation.value ? checkoutForm.area || null : null,
      initialCounterValue: checkoutForm.initialCounterValue ?? null,
      initialCounterDate: checkoutForm.initialCounterDate ? new Date(checkoutForm.initialCounterDate).toISOString() : null,
      generalMaintenanceDone: activeInstallation.value ? checkoutForm.generalMaintenanceDone : null,
      unitsMaintenanceDone: activeInstallation.value ? checkoutForm.unitsMaintenanceDone ?? null : null,
      existingConsumablesPrints:
        activeInstallation.value && checkoutForm.unitsMaintenanceDone ? checkoutForm.existingConsumablesPrints ?? null : null,
      externalAssetBrand: isExternalTicket.value ? checkoutForm.externalAssetBrand.trim() || null : null,
      externalAssetModel: isExternalTicket.value ? checkoutForm.externalAssetModel.trim() || null : null,
      externalAssetCounter: isExternalTicket.value ? checkoutForm.externalAssetCounter ?? null : null
    })
    ElMessage.success(
      checkoutForm.resolved ? 'Check-out registrado. Trabajo marcado como resuelto.' : 'Check-out registrado. Puedes retomarlo más tarde.'
    )
    checkoutForm.notes = ''
    checkoutForm.resolved = true
    checkoutForm.area = ''
    checkoutForm.initialCounterValue = undefined
    checkoutForm.initialCounterDate = ''
    checkoutForm.generalMaintenanceDone = false
    checkoutForm.unitsMaintenanceDone = undefined
    checkoutForm.existingConsumablesPrints = undefined
    checkoutForm.externalAssetBrand = ''
    checkoutForm.externalAssetModel = ''
    checkoutForm.externalAssetCounter = undefined
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo hacer check-out.')
  } finally {
    checkingOut.value = false
  }
}

onMounted(loadAll)
</script>

<template>
  <div class="my-work-page" v-loading="loading">
    <h1>Mi trabajo</h1>

    <el-card v-if="isBusy" class="section-card status-card busy">
      <template #header>Visita en curso</template>
      <p v-if="activeTicket">
        <strong>Ticket:</strong> {{ activeTicket.clientName }} — {{ activeTicket.clientLocationName }}<br />
        {{ activeTicket.description }}
      </p>
      <p v-else-if="activeOrder">
        <strong>Mantenimiento:</strong> {{ activeOrder.assetBrandName }} {{ activeOrder.assetModel }} —
        {{ activeOrder.assetSerialNumber }}
      </p>
      <p v-else-if="activeInstallation">
        <strong>Instalación:</strong> {{ activeInstallation.clientName }} — {{ activeInstallation.clientLocationName }}<br />
        {{ activeInstallation.assetBrandName }} {{ activeInstallation.model }} — {{ activeInstallation.serialNumber }}
      </p>
      <p class="checked-in-since" v-if="status?.checkedInAt">
        Desde {{ new Date(status.checkedInAt).toLocaleString() }}
      </p>

      <el-form label-position="top" class="checkout-form">
        <el-form-item label="¿Quedó terminado?">
          <el-switch
            v-model="checkoutForm.resolved"
            active-text="Sí, marcar como resuelto"
            inactive-text="No, solo pausar"
          />
        </el-form-item>

        <template v-if="checkoutForm.resolved">
          <el-form-item v-if="activeInstallation" label="Área donde quedó instalado">
            <el-input v-model="checkoutForm.area" placeholder="Ej: Recepción" />
          </el-form-item>

          <template v-if="activeInstallation || activeOrder || activeTicket">
            <el-form-item :label="activeTicket ? 'Contador (opcional)' : 'Contador'">
              <el-input-number v-model="checkoutForm.initialCounterValue" :min="0" style="width: 100%" />
            </el-form-item>
            <el-form-item label="Fecha de la lectura (opcional, hoy por defecto)">
              <el-date-picker
                v-model="checkoutForm.initialCounterDate"
                type="date"
                style="width: 100%"
                value-format="YYYY-MM-DD"
                :disabled-date="isCounterDateDisabled"
              />
              <div v-if="counterDateBounds" class="hint" style="margin: 0.35rem 0 0">
                Debe estar
                {{
                  counterDateBounds.end
                    ? `entre ${counterDateBounds.start.toLocaleDateString()} y ${counterDateBounds.end.toLocaleDateString()}`
                    : `a partir del ${counterDateBounds.start.toLocaleDateString()}`
                }}
                (vigencia del contrato).
              </div>
            </el-form-item>
          </template>

          <template v-if="activeInstallation">
            <el-form-item label="Mantenimiento general">
              <el-checkbox v-model="checkoutForm.generalMaintenanceDone">Realizado</el-checkbox>
            </el-form-item>

            <el-form-item label="Unidades / insumos">
              <el-radio-group v-model="checkoutForm.unitsMaintenanceDone">
                <el-radio :value="false">Insumos nuevos</el-radio>
                <el-radio :value="true">Mantenimiento de unidades realizado</el-radio>
              </el-radio-group>
            </el-form-item>

            <el-form-item v-if="checkoutForm.unitsMaintenanceDone" label="Impresiones actuales de los insumos instalados">
              <el-input-number v-model="checkoutForm.existingConsumablesPrints" :min="0" style="width: 100%" />
            </el-form-item>
          </template>

          <template v-if="isExternalTicket">
            <p class="hint">
              Cliente sin contrato — el equipo no está catalogado. Puedes dejar constancia de lo que
              identificaste (todo opcional).
            </p>
            <el-form-item label="Marca del equipo (opcional)">
              <el-input v-model="checkoutForm.externalAssetBrand" placeholder="Ej: Epson" />
            </el-form-item>
            <el-form-item label="Modelo del equipo (opcional)">
              <el-input v-model="checkoutForm.externalAssetModel" placeholder="Ej: L3250" />
            </el-form-item>
            <el-form-item label="Contador del equipo (opcional)">
              <el-input-number v-model="checkoutForm.externalAssetCounter" :min="0" style="width: 100%" />
            </el-form-item>
          </template>
        </template>

        <el-form-item label="Notas (opcional)">
          <el-input v-model="checkoutForm.notes" type="textarea" :rows="2" />
        </el-form-item>
        <el-button type="primary" class="tap-btn" :loading="checkingOut" :disabled="!checkoutFormValid" @click="doCheckOut">
          Check-out
        </el-button>
      </el-form>
    </el-card>

    <template v-else>
      <p class="hint">Estás disponible. Elige un ticket, orden o instalación pendiente para hacer check-in.</p>

      <el-card class="section-card">
        <template #header>
          <div class="section-header">
            <el-icon><Tickets /></el-icon>
            <span>Tickets</span>
          </div>
        </template>
        <el-tabs v-model="ticketsTab">
          <el-tab-pane :label="`Mis tickets (${checkInableTickets.length})`" name="mine">
            <el-table v-if="checkInableTickets.length > 0" :data="checkInableTickets">
              <el-table-column label="Cliente / Sede">
                <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
              </el-table-column>
              <el-table-column prop="description" label="Descripción" show-overflow-tooltip />
              <el-table-column label="Estado" width="120">
                <template #default="{ row }">
                  <el-tag size="small">{{ ServiceTicketStatusLabels[row.status] ?? row.status }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="" width="130">
                <template #default="{ row }">
                  <el-button type="primary" class="tap-btn" :loading="checkingIn === row.id" @click="checkInTicket(row)">
                    Check-in
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
            <p v-else class="muted">No tienes tickets asignados pendientes.</p>
          </el-tab-pane>

          <el-tab-pane :label="`En mi zona (${coverageTickets.length})`" name="coverage">
            <p class="hint">De otros técnicos en ciudades que también cubres. Puedes tomar los que aún no arrancó nadie.</p>
            <el-table v-if="coverageTickets.length > 0" :data="coverageTickets">
              <el-table-column label="Cliente / Sede">
                <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
              </el-table-column>
              <el-table-column prop="description" label="Descripción" show-overflow-tooltip />
              <el-table-column label="Técnico asignado">
                <template #default="{ row }">{{ row.technicianName ?? 'Sin asignar' }}</template>
              </el-table-column>
              <el-table-column label="Estado" width="120">
                <template #default="{ row }">
                  <el-tag size="small">{{ ServiceTicketStatusLabels[row.status] ?? row.status }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="" width="130">
                <template #default="{ row }">
                  <el-tag v-if="!isClaimable(row.status)" type="info" size="small">En curso</el-tag>
                  <el-button v-else class="tap-btn" :loading="claiming === row.id" @click="claimTicketRow(row)">
                    Tomar
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
            <p v-else class="muted">No hay tickets de otros técnicos en tu zona.</p>
          </el-tab-pane>
        </el-tabs>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-header">
            <el-icon><Tools /></el-icon>
            <span>Mantenimientos</span>
          </div>
        </template>
        <el-tabs v-model="ordersTab">
          <el-tab-pane :label="`Mis mantenimientos (${checkInableOrders.length})`" name="mine">
            <el-table v-if="checkInableOrders.length > 0" :data="checkInableOrders">
              <el-table-column label="Activo">
                <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
              </el-table-column>
              <el-table-column label="Estado" width="120">
                <template #default="{ row }">
                  <el-tag size="small">{{ MaintenanceOrderStatusLabels[row.status] ?? row.status }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="" width="130">
                <template #default="{ row }">
                  <el-button type="primary" class="tap-btn" :loading="checkingIn === row.id" @click="checkInOrder(row)">
                    Check-in
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
            <p v-else class="muted">No tienes órdenes de mantenimiento pendientes.</p>
          </el-tab-pane>

          <el-tab-pane :label="`En mi zona (${coverageOrders.length})`" name="coverage">
            <p class="hint">De otros técnicos en ciudades que también cubres. Puedes tomar las que aún no arrancó nadie.</p>
            <el-table v-if="coverageOrders.length > 0" :data="coverageOrders">
              <el-table-column label="Activo">
                <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
              </el-table-column>
              <el-table-column label="Ubicación">
                <template #default="{ row }">{{ row.clientLocationName ?? '—' }} · {{ row.cityName ?? '—' }}</template>
              </el-table-column>
              <el-table-column label="Técnico asignado">
                <template #default="{ row }">{{ row.technicianName ?? '—' }}</template>
              </el-table-column>
              <el-table-column label="Estado" width="120">
                <template #default="{ row }">
                  <el-tag size="small">{{ MaintenanceOrderStatusLabels[row.status] ?? row.status }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="" width="130">
                <template #default="{ row }">
                  <el-tag v-if="!isClaimable(row.status)" type="info" size="small">En curso</el-tag>
                  <el-button v-else class="tap-btn" :loading="claiming === row.id" @click="claimOrderRow(row)">
                    Tomar
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
            <p v-else class="muted">No hay mantenimientos de otros técnicos en tu zona.</p>
          </el-tab-pane>
        </el-tabs>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-header installations-header">
            <div class="section-header-title">
              <el-icon><Printer /></el-icon>
              <span>Instalaciones pendientes ({{ pendingInstallations.length }})</span>
            </div>
            <el-select
              v-model="installationFilter.clientId"
              clearable
              filterable
              placeholder="Filtrar por cliente"
              style="width: 220px"
            >
              <el-option v-for="c in installationClients" :key="c.id" :label="c.name" :value="c.id" />
            </el-select>
          </div>
        </template>
        <el-table v-if="filteredInstallations.length > 0" :data="filteredInstallations">
          <el-table-column label="Cliente / Sede">
            <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
          </el-table-column>
          <el-table-column label="Equipo">
            <template #default="{ row }">{{ row.assetBrandName }} {{ row.model }} — {{ row.serialNumber }}</template>
          </el-table-column>
          <el-table-column label="Ciudad" width="140">
            <template #default="{ row }">{{ row.cityName ?? '—' }}</template>
          </el-table-column>
          <el-table-column label="" width="160">
            <template #default="{ row }">
              <el-tag v-if="row.takenByAnotherTechnician" type="info" size="small">Tomada por otro técnico</el-tag>
              <el-button
                v-else
                type="primary"
                class="tap-btn"
                :loading="checkingIn === row.assetId"
                @click="checkInInstallation(row)"
              >
                Check-in
              </el-button>
            </template>
          </el-table-column>
        </el-table>
        <p v-else class="muted">
          {{ installationFilter.clientId ? 'No hay instalaciones pendientes para ese cliente.' : 'No hay instalaciones pendientes.' }}
        </p>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-header">
            <el-icon><Calendar /></el-icon>
            <span>Cronograma en mi zona ({{ coverageSchedules.length }})</span>
          </div>
        </template>
        <el-table v-if="coverageSchedules.length > 0" :data="coverageSchedules">
          <el-table-column label="Activo">
            <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
          </el-table-column>
          <el-table-column label="Ciudad / Cliente">
            <template #default="{ row }">{{ row.cityName ?? '—' }} · {{ row.clientName }}</template>
          </el-table-column>
          <el-table-column label="Último contador" width="130">
            <template #default="{ row }">{{ row.lastKnownCounter ?? '—' }}</template>
          </el-table-column>
          <el-table-column label="Próx. general" width="170">
            <template #default="{ row }">
              {{ new Date(row.nextGeneralDueAt).toLocaleDateString() }} / {{ row.nextGeneralDueCounter }}
            </template>
          </el-table-column>
          <el-table-column label="Próx. unidades" width="170">
            <template #default="{ row }">
              {{ new Date(row.nextUnitsDueAt).toLocaleDateString() }} / {{ row.nextUnitsDueCounter }}
            </template>
          </el-table-column>
          <el-table-column label="Próx. insumos" width="120">
            <template #default="{ row }">{{ row.nextConsumablesDueCounter }}</template>
          </el-table-column>
        </el-table>
        <p v-else class="muted">No hay cronogramas de activos en tu zona.</p>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.my-work-page {
  max-width: 1200px;
}

.my-work-page h1 {
  margin-bottom: 1.25rem;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  padding: 0.5rem 0.25rem;
}

.section-card {
  margin-bottom: 1.75rem;
}

.section-card :deep(.el-card__header) {
  background-color: var(--el-fill-color-light);
  padding: 1rem 1.5rem;
}

.section-card :deep(.el-card__body) {
  padding: 1.5rem;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  font-size: 1.05rem;
}

.installations-header {
  justify-content: space-between;
}

.section-header-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.status-card.busy {
  border-color: var(--el-color-warning);
}

.checked-in-since {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.checkout-form {
  margin-top: 1rem;
}

/* This screen is used almost exclusively on a phone, in the field — every primary
   action needs a real touch target (min 44px), not the compact table-row default. */
.tap-btn {
  min-height: 44px;
  padding: 0 1.1rem;
}
</style>
