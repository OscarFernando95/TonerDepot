<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as ticketsApi from '../../api/tickets'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import EvidenceGallery from '../../components/EvidenceGallery.vue'
import * as techniciansApi from '../../api/technicians'
import { useAuthStore } from '../../stores/auth'
import {
  RoleNames,
  ServiceTicketAllowedTransitions,
  ServiceTicketStatusLabels,
  type AssignmentHistoryDto,
  type ServiceTicketDto
} from '../../api/types'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const isStaff = auth.hasRole(RoleNames.Administrador, RoleNames.Coordinador)
const ticketId = route.params.id as string

const ticket = ref<ServiceTicketDto | null>(null)
const history = ref<AssignmentHistoryDto[]>([])
const technicians = ref<techniciansApi.TechnicianDto[]>([])
const loading = ref(false)

const assignDialogVisible = ref(false)
const savingAssign = ref(false)
const assignForm = reactive({ technicianId: '', reason: '' })

const changingStatus = ref(false)

const availableTransitions = computed(() => (ticket.value ? ServiceTicketAllowedTransitions[ticket.value.status] : []))
const canAssign = computed(
  () => ticket.value && ['Abierto', 'SinAsignar', 'Asignado'].includes(ticket.value.status)
)

async function loadAll() {
  loading.value = true
  try {
    const promises: Promise<any>[] = [ticketsApi.getTicket(ticketId)]
    if (isStaff) {
      promises.push(ticketsApi.getTicketAssignmentHistory(ticketId), techniciansApi.listTechnicians())
    }
    const results = await Promise.all(promises)
    ticket.value = results[0].data
    if (isStaff) {
      history.value = results[1].data
      technicians.value = results[2].data
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el ticket.')
  } finally {
    loading.value = false
  }
}

function openAssignDialog() {
  assignForm.technicianId = ticket.value?.technicianId ?? ''
  assignForm.reason = ''
  assignDialogVisible.value = true
}

async function saveAssign() {
  savingAssign.value = true
  try {
    const { data } = await ticketsApi.assignTicket(ticketId, {
      technicianId: assignForm.technicianId,
      reason: assignForm.reason || null
    })
    ticket.value = data
    ElMessage.success('Ticket asignado.')
    assignDialogVisible.value = false
    const { data: historyData } = await ticketsApi.getTicketAssignmentHistory(ticketId)
    history.value = historyData
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo asignar el ticket.')
  } finally {
    savingAssign.value = false
  }
}

async function changeStatus(status: string) {
  changingStatus.value = true
  try {
    const { data } = await ticketsApi.setTicketStatus(ticketId, status)
    ticket.value = data
    ElMessage.success('Estado actualizado.')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cambiar el estado.')
  } finally {
    changingStatus.value = false
  }
}

// Minutos → "2 h 15 min" / "45 min" / "Menos de 1 min".
function formatDuration(totalMinutes: number) {
  if (totalMinutes < 1) return 'Menos de 1 min'
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  if (hours === 0) return `${minutes} min`
  return minutes === 0 ? `${hours} h` : `${hours} h ${minutes} min`
}

function statusLabel(status: string) {
  return ServiceTicketStatusLabels[status] ?? status
}

onMounted(loadAll)

// Reasignación, check-in/out o cierre desde otra pantalla actualizan este detalle solos.
useRealtimeUpdates(['Ticket'], (event) => {
  if (event.id === ticketId) loadAll()
})
</script>

<template>
  <div v-loading="loading">
    <template v-if="ticket">
      <div class="page-header">
        <el-button :icon="ArrowLeft" circle title="Volver a Tickets" @click="router.push({ name: 'tickets' })" />
        <h1>{{ ticket.clientName }} — {{ ticket.clientLocationName }}</h1>
        <el-tag size="large">{{ statusLabel(ticket.status) }}</el-tag>
      </div>

      <el-card class="section-card">
        <template #header>Detalle del ticket</template>
        <dl class="detail-grid">
          <dt>Descripción</dt>
          <dd>{{ ticket.description }}</dd>
          <dt>Prioridad</dt>
          <dd>{{ ticket.priority }}</dd>
          <dt>Activo reportado</dt>
          <dd>
            <span v-if="ticket.assetSerialNumber">
              {{ ticket.assetBrandName }} {{ ticket.assetModel }} — {{ ticket.assetSerialNumber }}
            </span>
            <span v-else-if="ticket.externalAssetBrand || ticket.externalAssetModel || ticket.externalAssetCounter != null">
              {{ ticket.externalAssetBrand ?? '—' }} {{ ticket.externalAssetModel ?? '' }}
              <span v-if="ticket.externalAssetCounter != null"> — Contador: {{ ticket.externalAssetCounter }}</span>
              <span class="muted"> (equipo del cliente, sin catalogar)</span>
            </span>
            <span v-else>—</span>
          </dd>
          <dt>Reportado por</dt>
          <dd>{{ ticket.reportedByUserName }}</dd>
          <dt>Técnico asignado</dt>
          <dd>{{ ticket.technicianName ?? '—' }}</dd>
          <dt>Creado</dt>
          <dd>{{ new Date(ticket.createdAt).toLocaleString() }}</dd>
          <template v-if="ticket.resolvedAt">
            <dt>Resuelto</dt>
            <dd>{{ new Date(ticket.resolvedAt).toLocaleString() }}</dd>
            <template v-if="ticket.resolutionDurationMinutes != null">
              <dt>Tiempo de resolución</dt>
              <dd>{{ formatDuration(ticket.resolutionDurationMinutes) }}</dd>
            </template>
          </template>
          <template v-if="ticket.closedAt">
            <dt>Cerrado</dt>
            <dd>{{ new Date(ticket.closedAt).toLocaleString() }}</dd>
          </template>
        </dl>

        <div v-if="isStaff" class="section-actions">
          <el-button v-if="canAssign" @click="openAssignDialog">
            {{ ticket.technicianId ? 'Reasignar técnico' : 'Asignar técnico' }}
          </el-button>
          <el-select
            v-if="availableTransitions.length > 0"
            placeholder="Cambiar estado a..."
            style="width: 220px"
            :disabled="changingStatus"
            @change="changeStatus"
          >
            <el-option v-for="s in availableTransitions" :key="s" :label="statusLabel(s)" :value="s" />
          </el-select>
        </div>
      </el-card>

      <el-card v-if="isStaff" class="section-card">
        <template #header>Evidencia y resolución</template>
        <div class="evidence-resolution">
          <section>
            <h3 class="column-title">Evidencia fotográfica</h3>
            <EvidenceGallery :ticket-id="ticketId" />
          </section>
          <section>
            <h3 class="column-title">Descripción de la resolución</h3>
            <p v-if="ticket.resolutionNotes" class="resolution-notes">{{ ticket.resolutionNotes }}</p>
            <p v-else class="muted">
              {{ ticket.resolvedAt ? 'El técnico no dejó descripción.' : 'Aún no se ha resuelto el ticket.' }}
            </p>
          </section>
        </div>
      </el-card>

      <el-card v-if="isStaff" class="section-card">
        <template #header>Historial de asignación</template>
        <el-table :data="history" stripe>
          <el-table-column prop="technicianName" label="Técnico" />
          <el-table-column label="Tipo" width="120">
            <template #default="{ row }">
              <el-tag size="small" :type="row.assignmentType === 'Manual' ? 'info' : 'primary'">
                {{ row.assignmentType }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="assignedByUserName" label="Asignado por" width="180">
            <template #default="{ row }">{{ row.assignedByUserName ?? 'Sistema (automático)' }}</template>
          </el-table-column>
          <el-table-column label="Fecha" width="180">
            <template #default="{ row }">{{ new Date(row.assignedAt).toLocaleString() }}</template>
          </el-table-column>
          <el-table-column prop="reason" label="Motivo" />
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="assignDialogVisible" title="Asignar técnico" width="420px">
      <el-form :model="assignForm" label-position="top">
        <el-form-item label="Técnico">
          <el-select v-model="assignForm.technicianId" style="width: 100%" filterable placeholder="Selecciona un técnico">
            <el-option v-for="t in technicians" :key="t.id" :label="t.fullName" :value="t.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Motivo (opcional)">
          <el-input v-model="assignForm.reason" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="assignDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingAssign" @click="saveAssign">Confirmar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.section-card {
  margin-bottom: 1.5rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: 160px 1fr;
  row-gap: 0.5rem;
  margin: 0;
}

.detail-grid dt {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.detail-grid dd {
  margin: 0;
}

.muted {
  color: #9ca3af;
  font-size: 0.85rem;
}

/* Evidencia a la izquierda, descripción de la resolución a la derecha; en pantallas angostas se apilan. */
.evidence-resolution {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

@media (max-width: 900px) {
  .evidence-resolution {
    grid-template-columns: 1fr;
  }
}

.column-title {
  margin: 0 0 0.75rem;
  font-size: 0.95rem;
  color: var(--el-text-color-secondary);
}

.resolution-notes {
  margin: 0;
  white-space: pre-wrap;
}

.section-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 1.25rem;
}
</style>
