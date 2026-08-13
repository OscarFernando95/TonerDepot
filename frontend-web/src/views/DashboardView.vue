<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import { RoleNames } from '../api/types'
import * as dashboardApi from '../api/dashboard'
import type { DashboardSummaryDto } from '../api/dashboard'

const auth = useAuthStore()
const isStaff = computed(() => auth.hasRole(RoleNames.Administrador, RoleNames.Coordinador))

const loading = ref(false)
const periodDays = ref(30)
const summary = ref<DashboardSummaryDto | null>(null)

const periodOptions = [
  { label: '7 días', value: 7 },
  { label: '30 días', value: 30 },
  { label: '90 días', value: 90 }
]

async function loadSummary() {
  loading.value = true
  try {
    const res = await dashboardApi.getDashboardSummary(periodDays.value)
    summary.value = res.data
  } catch {
    ElMessage.error('No se pudieron cargar los indicadores')
  } finally {
    loading.value = false
  }
}

function formatHours(value: number | null) {
  return value === null ? 'Sin datos' : `${value} h`
}

function formatPercentage(value: number | null) {
  return value === null ? 'Sin datos' : `${value}%`
}

function percentageStatus(value: number | null) {
  if (value === null) return undefined
  if (value >= 90) return 'success'
  if (value >= 70) return 'warning'
  return 'exception'
}

watch(periodDays, loadSummary)
onMounted(() => {
  if (isStaff.value) {
    loadSummary()
  }
})
</script>

<template>
  <div v-if="!isStaff">
    <h1>Hola, {{ auth.user?.fullName }}</h1>
    <p>
      Estás conectado como <strong>{{ auth.user?.role }}</strong>.
    </p>
  </div>

  <div v-else class="dashboard" v-loading="loading">
    <div class="dashboard-header">
      <h1>Indicadores</h1>
      <el-radio-group v-model="periodDays" size="small">
        <el-radio-button v-for="opt in periodOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </el-radio-button>
      </el-radio-group>
    </div>

    <template v-if="summary">
      <div class="stat-grid">
        <el-card shadow="never" class="stat-card">
          <div class="stat-label">MTTR (tiempo medio de resolución)</div>
          <div class="stat-value">{{ formatHours(summary.mttr.averageResolutionHours) }}</div>
          <div class="stat-sub">{{ summary.mttr.resolvedTicketCount }} tickets resueltos en el período</div>
        </el-card>

        <el-card shadow="never" class="stat-card">
          <div class="stat-label">Cumplimiento de SLA</div>
          <div class="stat-value">{{ formatPercentage(summary.slaCompliance.overallCompliancePercentage) }}</div>
          <div class="stat-sub">Sobre tickets resueltos en el período</div>
        </el-card>

        <el-card shadow="never" class="stat-card">
          <div class="stat-label">Mantenimientos a tiempo</div>
          <div class="stat-value">{{ formatPercentage(summary.maintenanceCompliance.onTimePercentage) }}</div>
          <div class="stat-sub">
            {{ summary.maintenanceCompliance.onTimeCount }} / {{ summary.maintenanceCompliance.completedCount }}
            completados dentro de {{ summary.maintenanceCompliance.windowDays }} días de la fecha programada
          </div>
        </el-card>

        <el-card shadow="never" class="stat-card">
          <div class="stat-label">Tickets abiertos / sin asignar</div>
          <div class="stat-value">
            {{ summary.ticketsByCity.reduce((sum, c) => sum + c.openCount + c.unassignedCount, 0) }}
          </div>
          <div class="stat-sub">En {{ summary.ticketsByCity.length }} ciudad(es) con backlog</div>
        </el-card>
      </div>

      <el-row :gutter="16" class="dashboard-tables">
        <el-col :span="12">
          <el-card shadow="never">
            <template #header>Tickets abiertos / sin asignar por ciudad</template>
            <el-table :data="summary.ticketsByCity" size="small" empty-text="Sin tickets pendientes">
              <el-table-column prop="cityName" label="Ciudad" />
              <el-table-column prop="openCount" label="Abiertos" width="100" align="center" />
              <el-table-column prop="unassignedCount" label="Sin asignar" width="110" align="center" />
            </el-table>
          </el-card>
        </el-col>

        <el-col :span="12">
          <el-card shadow="never">
            <template #header>Utilización de técnicos (base {{ periodDays }} días x 8h/día)</template>
            <el-table :data="summary.technicianUtilization" size="small" empty-text="Sin registros de tiempo">
              <el-table-column prop="technicianName" label="Técnico" />
              <el-table-column label="Horas registradas" width="130" align="center">
                <template #default="{ row }">{{ row.hoursLogged }} h</template>
              </el-table-column>
              <el-table-column label="Utilización" width="180">
                <template #default="{ row }">
                  <el-progress :percentage="row.utilizationPercentage" />
                </template>
              </el-table-column>
            </el-table>
          </el-card>
        </el-col>
      </el-row>

      <el-card shadow="never" class="dashboard-tables">
        <template #header>Cumplimiento de SLA por prioridad</template>
        <el-table :data="summary.slaCompliance.byPriority" size="small">
          <el-table-column prop="priority" label="Prioridad" width="120" />
          <el-table-column prop="targetHours" label="Meta" width="90" align="center">
            <template #default="{ row }">{{ row.targetHours }} h</template>
          </el-table-column>
          <el-table-column prop="resolvedCount" label="Resueltos" width="100" align="center" />
          <el-table-column prop="withinSlaCount" label="Dentro de SLA" width="120" align="center" />
          <el-table-column label="% cumplimiento">
            <template #default="{ row }">
              <el-progress
                v-if="row.compliancePercentage !== null"
                :percentage="row.compliancePercentage"
                :status="percentageStatus(row.compliancePercentage)"
              />
              <span v-else class="stat-sub">Sin datos</span>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.dashboard-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1rem;
  margin-bottom: 1rem;
}

.stat-card {
  min-height: 100px;
}

.stat-label {
  color: #6b7280;
  font-size: 0.85rem;
  margin-bottom: 0.5rem;
}

.stat-value {
  font-size: 1.75rem;
  font-weight: 600;
}

.stat-sub {
  color: #9ca3af;
  font-size: 0.8rem;
  margin-top: 0.25rem;
}

.dashboard-tables {
  margin-bottom: 1rem;
}
</style>
