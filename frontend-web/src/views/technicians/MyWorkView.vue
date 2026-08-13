<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as selfApi from '../../api/technicianSelf'
import * as ticketsApi from '../../api/tickets'
import * as ordersApi from '../../api/maintenanceOrders'
import { ServiceTicketStatusLabels, type ServiceTicketDto } from '../../api/types'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto } from '../../api/types'

const status = ref<selfApi.TechnicianSelfStatusDto | null>(null)
const tickets = ref<ServiceTicketDto[]>([])
const orders = ref<MaintenanceOrderDto[]>([])
const loading = ref(false)
const checkingIn = ref<string | null>(null)
const checkingOut = ref(false)

const checkoutForm = reactive({ resolved: true, notes: '' })

const isBusy = computed(() => status.value?.status === 'Ocupado')

const activeTicket = computed(() =>
  status.value?.activeServiceTicketId ? tickets.value.find((t) => t.id === status.value!.activeServiceTicketId) : null
)
const activeOrder = computed(() =>
  status.value?.activeMaintenanceOrderId
    ? orders.value.find((o) => o.id === status.value!.activeMaintenanceOrderId)
    : null
)

const checkInableTickets = computed(() =>
  tickets.value.filter((t) => t.status === 'Asignado' || t.status === 'EnProceso')
)
const checkInableOrders = computed(() =>
  orders.value.filter((o) => o.status === 'Asignada' || o.status === 'EnProceso')
)

async function loadAll() {
  loading.value = true
  try {
    const [statusRes, ticketsRes, ordersRes] = await Promise.all([
      selfApi.getMyStatus(),
      ticketsApi.listTickets(),
      ordersApi.listMaintenanceOrders()
    ])
    status.value = statusRes.data
    tickets.value = ticketsRes.data
    orders.value = ordersRes.data
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

async function doCheckOut() {
  checkingOut.value = true
  try {
    await selfApi.checkOut({ resolved: checkoutForm.resolved, notes: checkoutForm.notes || null })
    ElMessage.success(
      checkoutForm.resolved ? 'Check-out registrado. Trabajo marcado como resuelto.' : 'Check-out registrado. Puedes retomarlo más tarde.'
    )
    checkoutForm.notes = ''
    checkoutForm.resolved = true
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
  <div v-loading="loading">
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
        <el-form-item label="Notas (opcional)">
          <el-input v-model="checkoutForm.notes" type="textarea" :rows="2" />
        </el-form-item>
        <el-button type="primary" :loading="checkingOut" @click="doCheckOut">Check-out</el-button>
      </el-form>
    </el-card>

    <template v-else>
      <p class="hint">Estás disponible. Elige un ticket u orden asignada para hacer check-in.</p>

      <el-card class="section-card">
        <template #header>Mis tickets</template>
        <el-table :data="checkInableTickets">
          <el-table-column label="Cliente / Sede">
            <template #default="{ row }">{{ row.clientName }} — {{ row.clientLocationName }}</template>
          </el-table-column>
          <el-table-column prop="description" label="Descripción" show-overflow-tooltip />
          <el-table-column label="Estado" width="120">
            <template #default="{ row }">
              <el-tag size="small">{{ ServiceTicketStatusLabels[row.status] ?? row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="" width="120">
            <template #default="{ row }">
              <el-button type="primary" size="small" :loading="checkingIn === row.id" @click="checkInTicket(row)">
                Check-in
              </el-button>
            </template>
          </el-table-column>
        </el-table>
        <p v-if="checkInableTickets.length === 0" class="muted">No tienes tickets asignados pendientes.</p>
      </el-card>

      <el-card class="section-card">
        <template #header>Mis órdenes de mantenimiento</template>
        <el-table :data="checkInableOrders">
          <el-table-column label="Activo">
            <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
          </el-table-column>
          <el-table-column label="Estado" width="120">
            <template #default="{ row }">
              <el-tag size="small">{{ MaintenanceOrderStatusLabels[row.status] ?? row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="" width="120">
            <template #default="{ row }">
              <el-button type="primary" size="small" :loading="checkingIn === row.id" @click="checkInOrder(row)">
                Check-in
              </el-button>
            </template>
          </el-table-column>
        </el-table>
        <p v-if="checkInableOrders.length === 0" class="muted">No tienes órdenes de mantenimiento pendientes.</p>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.hint {
  color: #6b7280;
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  padding: 0.5rem 0;
}

.section-card {
  margin-bottom: 1.5rem;
}

.status-card.busy {
  border-color: var(--el-color-warning);
}

.checked-in-since {
  color: #6b7280;
  font-size: 0.85rem;
}

.checkout-form {
  margin-top: 1rem;
}
</style>
