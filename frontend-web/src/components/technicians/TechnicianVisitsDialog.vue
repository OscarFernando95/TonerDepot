<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import type { TechnicianDto, TimeLogDto } from '../../api/technicians'

const dateTimeFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' })
const formatDateTime = (iso: string) => dateTimeFormatter.format(new Date(iso))

const visible = ref(false)
const loading = ref(false)
const technician = ref<TechnicianDto | null>(null)
const visits = ref<TimeLogDto[]>([])

const STATUS: Record<string, { label: string; type: 'success' | 'danger' | 'warning' | 'info' }> = {
  EnSitio: { label: 'En sitio', type: 'success' },
  FueraDeSitio: { label: 'Fuera de sitio', type: 'danger' },
  SinUbicacion: { label: 'Sin ubicación', type: 'warning' },
  SedeSinCoordenadas: { label: 'Sede sin coordenadas', type: 'info' }
}

function describe(status: string | null, distance: number | null) {
  if (!status) return null
  const meta = STATUS[status] ?? { label: status, type: 'info' as const }
  const km = distance != null ? (distance >= 1000 ? `${(distance / 1000).toFixed(1)} km` : `${Math.round(distance)} m`) : null
  return { ...meta, label: km && status === 'FueraDeSitio' ? `${meta.label} (${km})` : meta.label }
}

async function open(target: TechnicianDto) {
  technician.value = target
  visible.value = true
  loading.value = true
  try {
    const { data } = await techniciansApi.listTechnicianVisits(target.id)
    visits.value = data
  } catch (err: any) {
    console.error('No se pudo cargar el historial de visitas:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el historial de visitas.')
  } finally {
    loading.value = false
  }
}

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" :title="`Visitas — ${technician?.fullName ?? ''}`" width="760px">
    <p class="hint">
      Ubicación del técnico al llegar y al cerrar, comparada con la sede. Solo se registra y se alerta: nunca bloquea el
      check-in.
    </p>
    <el-table :data="visits" v-loading="loading" empty-text="Sin visitas registradas." size="small">
      <el-table-column label="Inicio" width="140">
        <template #default="{ row }">{{ formatDateTime(row.startTime) }}</template>
      </el-table-column>
      <el-table-column label="Fin" width="140">
        <template #default="{ row }">{{ row.endTime ? formatDateTime(row.endTime) : 'En curso' }}</template>
      </el-table-column>
      <el-table-column label="Tipo" width="110">
        <template #default="{ row }">{{ row.serviceTicketId ? 'Ticket' : row.maintenanceOrderId ? 'Mantenimiento' : 'Instalación' }}</template>
      </el-table-column>
      <el-table-column label="Al llegar">
        <template #default="{ row }">
          <el-tag v-if="describe(row.checkInLocationStatus, row.checkInDistanceMeters)" :type="describe(row.checkInLocationStatus, row.checkInDistanceMeters)!.type" size="small">
            {{ describe(row.checkInLocationStatus, row.checkInDistanceMeters)!.label }}
          </el-tag>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column label="Al cerrar">
        <template #default="{ row }">
          <el-tag v-if="describe(row.checkOutLocationStatus, row.checkOutDistanceMeters)" :type="describe(row.checkOutLocationStatus, row.checkOutDistanceMeters)!.type" size="small">
            {{ describe(row.checkOutLocationStatus, row.checkOutDistanceMeters)!.label }}
          </el-tag>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
    </el-table>
    <template #footer>
      <el-button @click="visible = false">Cerrar</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
}
</style>
