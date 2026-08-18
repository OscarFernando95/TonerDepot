<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import { RoleNames } from '../api/types'
import * as dashboardApi from '../api/dashboard'
import type { DashboardSummaryDto } from '../api/dashboard'
import FlapText from '../components/board/FlapText.vue'
import LaneStatus from '../components/board/LaneStatus.vue'

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
  } catch (err) {
    console.error('No se pudo cargar el resumen del dashboard', err)
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

function backlogLane(openCount: number, unassignedCount: number): 'on-time' | 'warning' | 'critical' {
  if (unassignedCount > 0) return 'critical'
  if (openCount > 0) return 'warning'
  return 'on-time'
}

function utilizationLane(percentage: number): 'on-time' | 'warning' | 'critical' {
  if (percentage >= 90) return 'critical'
  if (percentage >= 70) return 'warning'
  return 'on-time'
}

const kpis = computed(() => {
  if (!summary.value) return []
  const s = summary.value
  const backlogTotal = s.ticketsByCity.reduce((sum, c) => sum + c.openCount + c.unassignedCount, 0)
  return [
    {
      label: 'MTTR',
      value: formatHours(s.mttr.averageResolutionHours),
      sub: `${s.mttr.resolvedTicketCount} tickets resueltos en el período`
    },
    {
      label: 'Cumplimiento SLA',
      value: formatPercentage(s.slaCompliance.overallCompliancePercentage),
      sub: 'Sobre tickets resueltos en el período'
    },
    {
      label: 'Mantenimientos a tiempo',
      value: formatPercentage(s.maintenanceCompliance.onTimePercentage),
      sub: `${s.maintenanceCompliance.onTimeCount} / ${s.maintenanceCompliance.completedCount} dentro de ${s.maintenanceCompliance.windowDays} días`
    },
    {
      label: 'Backlog abierto / sin asignar',
      value: String(backlogTotal),
      sub: `En ${s.ticketsByCity.length} ciudad(es) con backlog`
    }
  ]
})

watch(periodDays, loadSummary)
onMounted(() => {
  if (isStaff.value) {
    loadSummary()
  }
})
</script>

<template>
  <div v-if="!isStaff" class="gate-welcome">
    <p class="gate-welcome-eyebrow">
      <span class="gate-welcome-lamp" aria-hidden="true"></span>
      Sesión activa
    </p>
    <h1 class="gate-welcome-title">{{ auth.user?.fullName }}</h1>
    <p class="gate-welcome-role">{{ auth.user?.role }}</p>
  </div>

  <div v-else class="board" v-loading="loading">
    <div class="board-toolbar">
      <h1 class="board-title">Indicadores</h1>
      <el-radio-group v-model="periodDays" size="small" class="board-period">
        <el-radio-button v-for="opt in periodOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </el-radio-button>
      </el-radio-group>
    </div>

    <template v-if="summary">
      <section class="board-panel kpi-panel" aria-label="Indicadores clave">
        <div v-for="k in kpis" :key="k.label" class="kpi-lane">
          <span class="kpi-lane-head">{{ k.label }}</span>
          <div class="kpi-lane-value"><FlapText :value="k.value" /></div>
          <p class="kpi-lane-sub">{{ k.sub }}</p>
        </div>
      </section>

      <div class="board-grid">
        <section class="board-panel">
          <h2 class="board-panel-title">Tickets abiertos / sin asignar por ciudad</h2>
          <el-table :data="summary.ticketsByCity" size="small" empty-text="Sin tickets pendientes">
            <el-table-column prop="cityName" label="Ciudad" />
            <el-table-column label="Estado" width="130">
              <template #default="{ row }">
                <LaneStatus
                  :state="backlogLane(row.openCount, row.unassignedCount)"
                  :label="row.unassignedCount > 0 ? 'Sin asignar' : row.openCount > 0 ? 'En curso' : 'Al día'"
                />
              </template>
            </el-table-column>
            <el-table-column prop="openCount" label="Abiertos" width="100" align="center" />
            <el-table-column prop="unassignedCount" label="Sin asignar" width="110" align="center" />
          </el-table>
        </section>

        <section class="board-panel">
          <h2 class="board-panel-title">Utilización de técnicos (base {{ periodDays }} días x 8h/día)</h2>
          <el-table :data="summary.technicianUtilization" size="small" empty-text="Sin registros de tiempo">
            <el-table-column prop="technicianName" label="Técnico" />
            <el-table-column label="Horas registradas" width="140" align="center">
              <template #default="{ row }">{{ row.hoursLogged }} h</template>
            </el-table-column>
            <el-table-column label="Utilización" width="170">
              <template #default="{ row }">
                <div class="utilization-cell">
                  <LaneStatus :state="utilizationLane(row.utilizationPercentage)" :label="`${row.utilizationPercentage}%`" />
                </div>
              </template>
            </el-table-column>
          </el-table>
        </section>
      </div>

      <section class="board-panel">
        <h2 class="board-panel-title">Cumplimiento de SLA por prioridad</h2>
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
              <span v-else class="board-panel-empty">Sin datos</span>
            </template>
          </el-table-column>
        </el-table>
      </section>
    </template>
  </div>
</template>

<style scoped>
.board-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.25rem;
}

.board-title {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: var(--text-xl);
  margin: 0;
  color: var(--flap-ink);
}

.board-panel {
  background: var(--board-panel);
  border: 1px solid var(--board-seam-soft);
  margin-bottom: 1.25rem;
}

.board-panel-title {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-md);
  font-weight: 600;
  color: var(--flap-ink-dim);
  margin: 0;
  padding: 0.9rem 1.1rem;
  border-bottom: 1px solid var(--board-seam-soft);
}

.board-panel :deep(.el-table) {
  --el-table-header-bg-color: transparent;
  --el-table-row-hover-bg-color: var(--board-panel-raised);
}

.board-panel :deep(.el-table th.el-table__cell) {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-2xs);
  color: var(--flap-ink-dim);
}

.board-panel-empty {
  color: var(--flap-ink-dim);
  font-size: var(--text-sm);
}

.kpi-panel {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
}

.kpi-lane {
  padding: 1.1rem 1.25rem;
  border-left: 1px solid var(--board-seam-soft);
}
.kpi-lane:first-child {
  border-left: none;
}

.kpi-lane-head {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-2xs);
  color: var(--flap-ink-dim);
}

.kpi-lane-value {
  font-family: var(--font-display);
  font-weight: 700;
  font-size: var(--text-display);
  color: var(--flap-ink);
  margin: 0.35rem 0 0.2rem;
}

.kpi-lane-sub {
  margin: 0;
  color: var(--flap-ink-dim);
  font-size: var(--text-sm);
}

.board-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.board-grid .board-panel {
  margin-bottom: 0;
  /* A grid item's implicit min-width:auto lets a wide table blow out the
     track instead of scrolling inside it; force it to respect the track. */
  min-width: 0;
}

.utilization-cell {
  display: flex;
  align-items: center;
}

.gate-welcome {
  max-width: 28rem;
  margin: 3rem auto;
  text-align: center;
}

.gate-welcome-eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: var(--text-xs);
  color: var(--signal-blue-bright);
  margin: 0 0 0.75rem;
}

.gate-welcome-lamp {
  width: 0.45rem;
  height: 0.45rem;
  border-radius: 50%;
  background: var(--signal-blue-bright);
}

.gate-welcome-title {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-2xl);
  margin: 0 0 0.35rem;
  color: var(--flap-ink);
}

.gate-welcome-role {
  color: var(--flap-ink-dim);
  margin: 0;
}

@media (max-width: 960px) {
  .kpi-panel {
    grid-template-columns: repeat(2, 1fr);
  }
  .kpi-lane:nth-child(2n + 1) {
    border-left: none;
  }
  .kpi-lane:nth-child(n + 3) {
    border-top: 1px solid var(--board-seam-soft);
  }
  .board-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .kpi-panel {
    grid-template-columns: 1fr;
  }
  .kpi-lane {
    border-left: none;
    border-top: 1px solid var(--board-seam-soft);
  }
  .kpi-lane:first-child {
    border-top: none;
  }
}
</style>
