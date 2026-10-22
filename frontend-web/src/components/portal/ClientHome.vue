<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowRight, ChatDotRound, Download, Printer, Refresh, Tickets, Tools, Warning } from '@element-plus/icons-vue'
import * as clientPortalApi from '../../api/clientPortal'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import type { ClientHomeDto } from '../../api/types'
import LaneStatus from '../board/LaneStatus.vue'
import {
  HOME_MAX_INSTALLATIONS,
  HOME_MAX_MAINTENANCE,
  HOME_MAX_TICKETS,
  ageLabel,
  commentsNotice,
  greeting,
  homeSummary,
  installationsTile,
  machinesTile,
  maintenanceStatusLabel,
  maintenanceStatusLamp,
  maintenanceTile,
  plural,
  priorityLabel,
  priorityLamp,
  splitVisible,
  ticketStatusLabel,
  ticketStatusLamp,
  ticketsTile,
  toneLabel,
  toneLamp,
  visitLabel
} from '../../composables/useClientHome'

// Inicio del Cliente: cuatro tarjetas (tickets, mantenimientos, instalaciones y máquinas) que cuentan el estado de su
// servicio de un vistazo. Cada tarjeta entera es un enlace a su sección (enlace "estirado" en el título) y las filas
// dentro de ella llevan a su propio detalle. Un solo llamado (GET /client-portal/home); se refresca solo.
const router = useRouter()
const home = ref<ClientHomeDto | null>(null)
const loading = ref(false)
const error = ref('')
// Las etiquetas "hace 5 min" se recalculan cada minuto sin volver a pedir datos.
const now = ref(Date.now())
let ticker: ReturnType<typeof setInterval> | null = null

async function load(silent = false) {
  if (!silent) loading.value = true
  if (!silent) error.value = ''
  try {
    home.value = (await clientPortalApi.getClientHome()).data
    error.value = ''
    now.value = Date.now()
  } catch (err) {
    console.error('ClientHome.load failed', err)
    // Un refresco silencioso que falla no tapa lo que ya se ve; solo la primera carga muestra el error.
    if (!silent || !home.value) error.value = 'No pudimos cargar tu inicio. Revisa tu conexión e inténtalo de nuevo.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  load()
  ticker = setInterval(() => (now.value = Date.now()), 60000)
})
onBeforeUnmount(() => {
  if (ticker) clearInterval(ticker)
})

// Se actualiza sola cuando cambia un ticket, un mantenimiento o un equipo de la empresa.
useRealtimeUpdates(['Ticket', 'MaintenanceOrder', 'Asset'], () => load(true))

const greetingText = computed(() => greeting(new Date(now.value).getHours()))
const summary = computed(() => (home.value ? homeSummary(home.value) : ''))

const tickets = computed(() => (home.value ? ticketsTile(home.value) : null))
const maintenance = computed(() => (home.value ? maintenanceTile(home.value) : null))
const installs = computed(() => (home.value ? installationsTile(home.value) : null))
const machines = computed(() => (home.value ? machinesTile(home.value) : null))

const visibleTickets = computed(() => splitVisible(home.value?.recentTickets ?? [], HOME_MAX_TICKETS))
const visibleMaintenance = computed(() => splitVisible(home.value?.upcomingMaintenance ?? [], HOME_MAX_MAINTENANCE))
const visibleInstallations = computed(() => splitVisible(home.value?.installations ?? [], HOME_MAX_INSTALLATIONS))
const notice = computed(() => commentsNotice(home.value?.upcomingMaintenance ?? []))

const machinesBarLabel = computed(() =>
  machines.value
    ? `${machines.value.inOrder} de ${machines.value.installed} ${plural(machines.value.installed, 'máquina', 'máquinas')} en orden`
    : ''
)

// "Reportar un problema" abre el diálogo de nuevo ticket de la lista de tickets (que ya sabe armar el del Cliente).
function reportProblem() {
  router.push({ name: 'tickets', query: { nuevo: '1' } })
}
</script>

<template>
  <div class="client-home">
    <header class="home-hero">
      <div class="home-hero-text">
        <p class="home-eyebrow">
          <span class="home-eyebrow-lamp" aria-hidden="true"></span>
          {{ greetingText }}
        </p>
        <h1 v-if="home" class="home-title">{{ home.clientName }}</h1>
        <span v-else class="sk sk-title" aria-hidden="true"></span>
        <p v-if="home" class="home-summary">{{ summary }}</p>
        <span v-else class="sk sk-line" aria-hidden="true"></span>
      </div>
      <el-button type="primary" size="large" class="home-report" :icon="Warning" @click="reportProblem">
        Reportar un problema
      </el-button>
    </header>

    <div v-if="error && !home" class="home-error" role="alert">
      <p class="home-error-text">{{ error }}</p>
      <el-button :icon="Refresh" :loading="loading" @click="load()">Reintentar</el-button>
    </div>

    <div v-else-if="loading && !home" class="tiles" role="status" aria-label="Cargando tu inicio">
      <div v-for="n in 4" :key="n" class="tile tile-skeleton" aria-hidden="true">
        <span class="sk sk-icon"></span>
        <span class="sk sk-number"></span>
        <span class="sk sk-line"></span>
        <span class="sk sk-line sk-short"></span>
      </div>
    </div>

    <div v-else-if="home && tickets && maintenance && installs && machines" class="tiles">
      <!-- 1. Tickets -->
      <article class="tile" :class="`tone-${tickets.tone}`">
        <div class="tile-head">
          <span class="tile-icon" aria-hidden="true"><el-icon><Tickets /></el-icon></span>
          <h2 class="tile-label">
            <router-link class="tile-link" :to="{ name: 'tickets' }">Tickets</router-link>
          </h2>
          <LaneStatus class="tile-lamp" :state="toneLamp(tickets.tone)" :label="toneLabel(tickets.tone)" />
          <el-icon class="tile-go" aria-hidden="true"><ArrowRight /></el-icon>
        </div>

        <div class="tile-figure">
          <span class="tile-number">{{ tickets.value }}</span>
          <span class="tile-unit">{{ plural(tickets.value, 'abierto', 'abiertos') }}</span>
          <span v-if="tickets.inProgress > 0" class="tile-stat"><b>{{ tickets.inProgress }}</b> en curso</span>
        </div>
        <p class="tile-phrase">{{ tickets.phrase }}</p>

        <ul v-if="visibleTickets.shown.length" class="rows" aria-label="Tickets más recientes">
          <li v-for="t in visibleTickets.shown" :key="t.id">
            <router-link class="row" :to="{ name: 'ticket-detail', params: { id: t.id } }">
              <span class="row-main">
                <span class="row-title">{{ t.equipment }}</span>
                <span v-if="t.locationName" class="row-place">{{ t.locationName }}</span>
              </span>
              <span class="row-meta">
                <LaneStatus :state="ticketStatusLamp(t.status)" :label="ticketStatusLabel(t.status)" />
                <LaneStatus v-if="priorityLamp(t.priority) !== 'idle'" :state="priorityLamp(t.priority)" :label="`Criticidad ${priorityLabel(t.priority)}`" />
                <span v-else class="row-sub">Criticidad {{ priorityLabel(t.priority) }}</span>
              </span>
              <span class="row-sub">
                {{ t.technicianName ? `Técnico: ${t.technicianName}` : 'Aún sin técnico' }} · {{ ageLabel(t.createdAt, now) }}
              </span>
            </router-link>
          </li>
        </ul>
        <p v-if="visibleTickets.hidden > 0" class="tile-more">y {{ visibleTickets.hidden }} más</p>
      </article>

      <!-- 2. Mantenimientos -->
      <article class="tile" :class="`tone-${maintenance.tone}`">
        <div class="tile-head">
          <span class="tile-icon" aria-hidden="true"><el-icon><Tools /></el-icon></span>
          <h2 class="tile-label">
            <router-link class="tile-link" :to="{ name: 'my-maintenance' }">Mantenimientos</router-link>
          </h2>
          <LaneStatus class="tile-lamp" :state="toneLamp(maintenance.tone)" :label="toneLabel(maintenance.tone)" />
          <el-icon class="tile-go" aria-hidden="true"><ArrowRight /></el-icon>
        </div>

        <div class="tile-figure">
          <span class="tile-number">{{ maintenance.scheduled }}</span>
          <span class="tile-unit">{{ plural(maintenance.scheduled, 'programado', 'programados') }}</span>
          <span v-if="maintenance.inProgress > 0" class="tile-stat"><b>{{ maintenance.inProgress }}</b> en curso</span>
          <span v-if="maintenance.withoutDate > 0" class="tile-stat"><b>{{ maintenance.withoutDate }}</b> esperando fecha</span>
        </div>
        <p class="tile-phrase">{{ maintenance.phrase }}</p>

        <ul v-if="visibleMaintenance.shown.length" class="rows" aria-label="Próximos mantenimientos">
          <li v-for="(m, i) in visibleMaintenance.shown" :key="m.id">
            <router-link class="row" :class="{ 'is-featured': i === 0 }" :to="{ name: 'my-maintenance' }">
              <span class="row-main">
                <span class="row-title">{{ m.equipment }}</span>
                <span v-if="m.locationName" class="row-place">{{ m.locationName }}</span>
              </span>
              <span class="row-meta">
                <LaneStatus :state="maintenanceStatusLamp(m.status)" :label="maintenanceStatusLabel(m.status)" />
                <span class="row-visit">{{ visitLabel(m, new Date(now)) }}</span>
              </span>
              <span v-if="m.technicianName" class="row-sub">Técnico: {{ m.technicianName }}</span>
            </router-link>
          </li>
        </ul>
        <p v-if="visibleMaintenance.hidden > 0" class="tile-more">y {{ visibleMaintenance.hidden }} más</p>
        <p v-if="notice" class="tile-note">
          <el-icon aria-hidden="true"><ChatDotRound /></el-icon>
          {{ notice }}
        </p>
      </article>

      <!-- 3. Instalaciones -->
      <article class="tile" :class="[`tone-${installs.tone}`, { 'is-compact': installs.empty }]">
        <div class="tile-head">
          <span class="tile-icon" aria-hidden="true"><el-icon><Download /></el-icon></span>
          <h2 class="tile-label">
            <router-link class="tile-link" :to="{ name: 'my-assets' }">Instalaciones</router-link>
          </h2>
          <LaneStatus class="tile-lamp" :state="toneLamp(installs.tone)" :label="toneLabel(installs.tone)" />
          <el-icon class="tile-go" aria-hidden="true"><ArrowRight /></el-icon>
        </div>

        <template v-if="installs.empty">
          <p class="tile-phrase">{{ installs.phrase }}</p>
        </template>
        <template v-else>
          <div class="tile-figure">
            <span class="tile-number">{{ installs.pending }}</span>
            <span class="tile-unit">{{ plural(installs.pending, 'pendiente', 'pendientes') }}</span>
            <span v-if="installs.inProgress > 0" class="tile-stat"><b>{{ installs.inProgress }}</b> instalando ahora</span>
          </div>
          <p class="tile-phrase">{{ installs.phrase }}</p>

          <ul v-if="visibleInstallations.shown.length" class="rows" aria-label="Equipos por instalar">
            <li v-for="i in visibleInstallations.shown" :key="i.assetId">
              <router-link class="row" :to="{ name: 'my-assets' }">
                <span class="row-main">
                  <span class="row-title">{{ i.equipment }}</span>
                  <span v-if="i.locationName" class="row-place">{{ i.locationName }}</span>
                </span>
                <span class="row-meta">
                  <LaneStatus v-if="i.inProgress" state="on-time" label="Instalando ahora" />
                  <LaneStatus v-else state="idle" label="Por instalar" />
                </span>
              </router-link>
            </li>
          </ul>
          <p v-if="visibleInstallations.hidden > 0" class="tile-more">y {{ visibleInstallations.hidden }} más</p>
        </template>
      </article>

      <!-- 4. Mis máquinas -->
      <article class="tile" :class="`tone-${machines.tone}`">
        <div class="tile-head">
          <span class="tile-icon" aria-hidden="true"><el-icon><Printer /></el-icon></span>
          <h2 class="tile-label">
            <router-link class="tile-link" :to="{ name: 'my-assets' }">Mis máquinas</router-link>
          </h2>
          <LaneStatus class="tile-lamp" :state="toneLamp(machines.tone)" :label="toneLabel(machines.tone)" />
          <el-icon class="tile-go" aria-hidden="true"><ArrowRight /></el-icon>
        </div>

        <div class="tile-figure">
          <span class="tile-number">{{ machines.installed }}</span>
          <span class="tile-unit">{{ plural(machines.installed, 'instalada', 'instaladas') }}</span>
        </div>
        <p class="tile-phrase">{{ machines.phrase }}</p>

        <template v-if="!machines.empty">
          <div class="fleet-bar" role="img" :aria-label="machinesBarLabel">
            <span class="fleet-ok" :style="{ width: `${machines.inOrderPct}%` }"></span>
            <span class="fleet-attention" :style="{ width: `${machines.attentionPct}%` }"></span>
          </div>
          <div class="fleet-legend">
            <LaneStatus state="on-time" :label="`${machines.inOrder} en orden`" />
            <LaneStatus v-if="machines.attention > 0" state="warning" :label="`${machines.attention} con ticket abierto`" />
            <LaneStatus v-if="machines.inMaintenance > 0" state="idle" :label="`${machines.inMaintenance} en mantenimiento`" />
          </div>
        </template>
      </article>
    </div>
  </div>
</template>

<style scoped>
.client-home {
  max-width: 68rem;
  margin: 0 auto;
  min-width: 0;
}

/* --- Saludo ----------------------------------------------------------------------------------------------- */
.home-hero {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem 1.5rem;
  margin-bottom: 1.5rem;
}

.home-hero-text {
  min-width: 0;
}

.home-eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: var(--text-xs);
  color: var(--signal-blue-bright);
  margin: 0 0 0.5rem;
}

.home-eyebrow-lamp {
  width: 0.45rem;
  height: 0.45rem;
  border-radius: 50%;
  background: var(--signal-blue-bright);
}

.home-title {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-2xl);
  line-height: 1.1;
  margin: 0 0 0.4rem;
  color: var(--flap-ink);
  overflow-wrap: anywhere;
}

.home-summary {
  margin: 0;
  color: var(--flap-ink-dim);
  font-size: var(--text-md);
}

.home-report {
  flex: none;
  min-height: 44px;
  font-weight: 600;
}

/* --- Cuadrícula de tarjetas ------------------------------------------------------------------------------- */
.tiles {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1rem;
}

@media (min-width: 1024px) {
  .tiles {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 1.25rem;
  }
}

/* Acento por tono: calmado en azul (a tiempo), ámbar/rojo suaves solo cuando hay algo que atender. */
.tone-calm,
.tone-info {
  --accent: var(--signal-blue-bright);
  --accent-wash: var(--signal-blue-wash);
}
.tone-attention {
  --accent: var(--signal-amber);
  --accent-wash: var(--signal-amber-wash);
}
.tone-alert {
  --accent: var(--signal-red);
  --accent-wash: var(--signal-red-wash);
}

.tile {
  position: relative; /* ancla del enlace estirado */
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-width: 0;
  padding: 1.1rem 1.25rem 1.2rem;
  background: var(--board-panel);
  border: 1px solid var(--board-seam-soft);
}

.tile:hover {
  background: var(--board-panel-raised);
  border-color: var(--board-seam);
}

.tile.is-compact {
  padding-bottom: 1rem;
}

.tile-head {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  margin-bottom: 0.4rem;
}

.tile-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  flex: none;
  background: var(--accent-wash);
  color: var(--accent);
  font-size: var(--text-lg);
}

.tile-label {
  flex: 1;
  min-width: 0;
  margin: 0;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-md);
  font-weight: 600;
}

/* Enlace estirado: todo el recuadro es clicable sin anidar enlaces dentro de enlaces. */
.tile-link {
  color: var(--flap-ink);
  text-decoration: none;
}
.tile-link::after {
  content: '';
  position: absolute;
  inset: 0;
}
html.dark .tile-link:focus-visible {
  outline: none;
}
html.dark .tile-link:focus-visible::after {
  outline: 2px solid var(--signal-blue-bright);
  outline-offset: -2px;
}

.tile-lamp {
  flex: none;
}

.tile-go {
  flex: none;
  color: var(--flap-ink-dim);
}
.tile:hover .tile-go,
.tile:focus-within .tile-go {
  color: var(--flap-ink);
}

.tile-figure {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.25rem 0.6rem;
}

.tile-number {
  font-family: var(--font-display);
  font-weight: 700;
  font-size: var(--text-display-lg);
  line-height: 1;
  font-variant-numeric: tabular-nums;
  color: var(--flap-ink);
}

.tile-unit {
  font-size: var(--text-md);
  color: var(--flap-ink-dim);
}

.tile-stat {
  font-size: var(--text-md);
  color: var(--flap-ink-dim);
}
.tile-stat b {
  color: var(--flap-ink);
  font-variant-numeric: tabular-nums;
}

.tile-phrase {
  margin: 0 0 0.35rem;
  color: var(--flap-ink-dim);
  font-size: var(--text-md);
  line-height: 1.45;
}

.tile-more {
  margin: 0.4rem 0 0;
  color: var(--flap-ink-dim);
  font-size: var(--text-sm);
}

.tile-note {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin: 0.5rem 0 0;
  color: var(--signal-blue-bright);
  font-size: var(--text-sm);
}

/* --- Filas compactas -------------------------------------------------------------------------------------- */
.rows {
  list-style: none;
  margin: 0.35rem 0 0;
  padding: 0;
}

.rows li + li {
  border-top: 1px solid var(--board-seam-soft);
}

/* z-index sobre el enlace estirado de la tarjeta: la fila abre su propio detalle. */
.row {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.6rem 0.5rem;
  margin: 0 -0.5rem;
  color: var(--flap-ink);
  text-decoration: none;
}
.row:hover {
  background: var(--signal-blue-wash);
}
.row.is-featured {
  background: var(--accent-wash);
}

.row-main {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0 0.5rem;
  min-width: 0;
}

.row-title {
  font-weight: 600;
  font-size: var(--text-md);
  overflow-wrap: anywhere;
}

.row-place,
.row-sub {
  color: var(--flap-ink-dim);
  font-size: var(--text-sm);
}

.row-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem 0.9rem;
}

.row-visit {
  font-size: var(--text-sm);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  color: var(--flap-ink);
}

/* --- Barra de máquinas ------------------------------------------------------------------------------------ */
.fleet-bar {
  display: flex;
  height: 0.6rem;
  margin-top: 0.4rem;
  background: var(--board-seam-soft);
  overflow: hidden;
}
.fleet-ok {
  background: var(--signal-blue-bright);
}
.fleet-attention {
  background: var(--signal-amber);
}

.fleet-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem 1.1rem;
  margin-top: 0.6rem;
}

/* --- Error ------------------------------------------------------------------------------------------------ */
.home-error {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1.25rem;
  background: var(--board-panel);
  border: 1px solid var(--board-seam-soft);
}
.home-error-text {
  margin: 0;
  color: var(--flap-ink);
}

/* --- Esqueleto de carga ----------------------------------------------------------------------------------- */
.sk {
  display: block;
  background: var(--board-seam-soft);
}
.sk-icon {
  width: 2.25rem;
  height: 2.25rem;
  margin-bottom: 0.4rem;
}
.sk-number {
  width: 4rem;
  height: 2.25rem;
  margin-bottom: 0.4rem;
}
.sk-line {
  width: 85%;
  height: 0.8rem;
  margin-bottom: 0.4rem;
}
.sk-short {
  width: 55%;
}
.sk-title {
  width: 14rem;
  max-width: 100%;
  height: 1.8rem;
  margin-bottom: 0.5rem;
}
.tile-skeleton {
  min-height: 9.5rem;
}
.tile-skeleton:hover {
  background: var(--board-panel);
  border-color: var(--board-seam-soft);
}

@media (prefers-reduced-motion: no-preference) {
  .sk {
    animation: home-pulse 1.4s ease-in-out infinite;
  }
  .tile,
  .row {
    transition: background-color 0.15s ease, border-color 0.15s ease;
  }
}

@keyframes home-pulse {
  50% {
    opacity: 0.45;
  }
}

@media (max-width: 640px) {
  .home-hero {
    flex-direction: column;
    align-items: stretch;
  }
  .home-report {
    width: 100%;
  }
  .tile-head {
    flex-wrap: wrap;
  }
  .tile-lamp {
    order: 3;
    flex-basis: 100%;
    margin-left: calc(2.25rem + 0.65rem);
  }
}
</style>
