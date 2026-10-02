<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as selfApi from '../../api/technicianSelf'
import { useAuthStore } from '../../stores/auth'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import type { HomeJobDto, TechnicianHomeDto } from '../../api/technicianSelf'

// Inicio del técnico: su estado y jornada, la visita en curso o el siguiente trabajo, la agenda, el resumen del día y
// el stock bajo de su zona. Mismo contenido que el Inicio de la app, de un solo llamado (GET /technicians/me/home).
const auth = useAuthStore()
const router = useRouter()
const home = ref<TechnicianHomeDto | null>(null)
const loading = ref(false)
const error = ref('')
const now = ref(Date.now())
let ticker: ReturnType<typeof setInterval> | null = null

async function load(silent = false) {
  if (!silent) loading.value = true
  error.value = ''
  try {
    home.value = (await selfApi.getMyHome()).data
  } catch (err: any) {
    console.error('TechnicianHome.load failed', err)
    if (!silent || !home.value) error.value = err.response?.data?.title ?? 'No se pudo cargar el inicio.'
  } finally {
    loading.value = false
  }
}

const nextJob = computed(() => home.value?.agenda.find((j) => j.id !== home.value?.activeVisit?.id) ?? null)
const restOfAgenda = computed(() => (home.value?.agenda ?? []).filter((j) => j.id !== home.value?.activeVisit?.id && j.id !== nextJob.value?.id))

const priorityType = (p: string) => ({ Critica: 'danger', Alta: 'warning', Media: 'primary', Baja: 'info' })[p] as 'danger' | 'warning' | 'primary' | 'info'
const priorityLabel = (p: string) => (p === 'Critica' ? 'Crítica' : p)

function workedLabel(minutes: number) {
  if (minutes < 60) return `${minutes} min`
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return m === 0 ? `${h} h` : `${h} h ${m} min`
}

function ageLabel(since: string) {
  const mins = Math.floor((now.value - new Date(since).getTime()) / 60000)
  if (mins < 1) return 'ahora'
  if (mins < 60) return `hace ${mins} min`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `hace ${hours} h`
  const days = Math.floor(hours / 24)
  return days === 1 ? 'hace 1 día' : `hace ${days} días`
}

const elapsed = computed(() => {
  const started = home.value?.activeVisitStartedAt
  if (!started) return ''
  const total = Math.max(0, Math.floor((now.value - new Date(started).getTime()) / 1000))
  const h = Math.floor(total / 3600)
  const m = String(Math.floor((total % 3600) / 60)).padStart(2, '0')
  const s = String(total % 60).padStart(2, '0')
  return h > 0 ? `${h}:${m}:${s}` : `${m}:${s}`
})

function placeLabel(job: HomeJobDto) {
  const base = [job.clientName, job.locationName].filter(Boolean).join(' — ')
  return job.cityName ? (base ? `${base} · ${job.cityName}` : job.cityName) : base
}

function directionsUrl(job: HomeJobDto) {
  if (job.latitude != null && job.longitude != null) {
    return `https://www.google.com/maps/dir/?api=1&destination=${job.latitude},${job.longitude}`
  }
  const query = [job.address, job.cityName].filter(Boolean).join(', ')
  return query ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(query)}` : null
}

function openDirections(job: HomeJobDto) {
  const url = directionsUrl(job)
  if (!url) {
    ElMessage.info('Esta sede no tiene dirección ni ubicación.')
    return
  }
  window.open(url, '_blank', 'noopener')
}

const dayTime = new Intl.DateTimeFormat('es-CO', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
const goToWork = () => router.push({ name: 'my-work' })

onMounted(() => {
  void load()
  ticker = setInterval(() => (now.value = Date.now()), 1000)
})
onBeforeUnmount(() => {
  if (ticker) clearInterval(ticker)
})
useRealtimeUpdates(['Ticket', 'MaintenanceOrder', 'Visit', 'Technician', 'Zone', 'Inventory'], () => void load(true))
</script>

<template>
  <div v-loading="loading" class="home">
    <div class="hello">
      <span class="eyebrow">Hola,</span>
      <h1>{{ auth.user?.fullName }}</h1>
    </div>

    <el-alert v-if="error" :title="error" type="error" show-icon :closable="false">
      <el-button size="small" @click="load()">Reintentar</el-button>
    </el-alert>

    <template v-if="home">
      <section class="panel status">
        <div class="status-line">
          <span class="lamp" :class="home.timeOffUntil ? 'amber' : home.isWorkingNow ? 'blue' : 'grey'" aria-hidden="true" />
          <strong v-if="home.timeOffUntil">En permiso hasta {{ dayTime.format(new Date(home.timeOffUntil)) }}</strong>
          <strong v-else-if="home.isWorkingNow">En horario</strong>
          <strong v-else>Fuera de horario</strong>
        </div>
        <p class="muted">{{ home.todayShift ? `Jornada de hoy: ${home.todayShift}` : 'Hoy no es día laboral para ti' }}</p>
        <p v-if="home.zoneNames.length" class="muted">Zona: {{ home.zoneNames.join(', ') }}</p>
        <p v-else class="warn">Aún no tienes una zona asignada.</p>
      </section>

      <div class="main-grid">
        <section v-if="home.activeVisit" class="panel focus">
          <div class="panel-title"><span class="lamp blue" aria-hidden="true" /> Visita en curso</div>
          <h2>{{ home.activeVisit.title }}</h2>
          <p class="muted">{{ placeLabel(home.activeVisit) }}</p>
          <div class="timer" role="timer" aria-label="Tiempo de visita">{{ elapsed }}</div>
          <div class="actions">
            <el-button type="primary" @click="goToWork">Cerrar visita</el-button>
            <el-button @click="openDirections(home.activeVisit)">Mapa</el-button>
          </div>
        </section>

        <section v-else-if="nextJob" class="panel focus">
          <div class="panel-title">
            Siguiente trabajo
            <el-tag :type="priorityType(nextJob.priority)" size="small" effect="plain">{{ priorityLabel(nextJob.priority) }}</el-tag>
          </div>
          <h2>{{ nextJob.title }}</h2>
          <p v-if="nextJob.summary">{{ nextJob.summary }}</p>
          <p class="muted">{{ placeLabel(nextJob) }}</p>
          <p v-if="nextJob.address" class="muted small">{{ nextJob.address }}</p>
          <p class="muted small">{{ nextJob.kind }} · {{ ageLabel(nextJob.since) }}</p>
          <div class="actions">
            <el-button type="primary" @click="goToWork">Iniciar</el-button>
            <el-button @click="openDirections(nextJob)">Cómo llegar</el-button>
          </div>
        </section>

        <section v-else class="panel focus">
          <div class="status-line"><span class="lamp grey" aria-hidden="true" /> <strong>No tienes trabajo pendiente por ahora</strong></div>
        </section>

        <section class="tiles">
          <div class="tile"><span class="tile-label">Visitas hoy</span><span class="tile-value">{{ home.visitsClosedToday }}</span></div>
          <div class="tile"><span class="tile-label">Trabajado</span><span class="tile-value">{{ workedLabel(home.minutesWorkedToday) }}</span></div>
        </section>
      </div>

      <section v-if="restOfAgenda.length" class="panel">
        <div class="panel-title">Agenda</div>
        <ul class="agenda">
          <li v-for="job in restOfAgenda" :key="job.id" @click="goToWork">
            <span class="bar" :class="job.priority" aria-hidden="true" />
            <span class="agenda-main">
              <strong>{{ job.title }}</strong>
              <span class="muted small">{{ placeLabel(job) }}</span>
            </span>
            <el-tag :type="priorityType(job.priority)" size="small" effect="plain">{{ priorityLabel(job.priority) }}</el-tag>
            <span class="muted small">{{ ageLabel(job.since) }}</span>
          </li>
        </ul>
      </section>

      <section v-if="home.lowStock.length" class="panel">
        <div class="panel-title amber-text">Stock bajo en tu zona</div>
        <ul class="stock">
          <li v-for="item in home.lowStock" :key="item.itemName + item.locationName">
            <span>{{ item.itemName }}</span>
            <strong :class="item.quantity < 0 ? 'neg' : 'low'">{{ item.quantity < 0 ? `${item.quantity} (negativo)` : `${item.quantity} / mín. ${item.minimumStock}` }}</strong>
          </li>
        </ul>
        <p class="muted small">Avisa para que repongan antes de tu próxima visita.</p>
      </section>
    </template>
  </div>
</template>

<style scoped>
.home { max-width: 62rem; margin: 0 auto; display: flex; flex-direction: column; gap: 1rem; }
.hello h1 { margin: 0; }
.eyebrow { font-size: 0.85rem; color: var(--flap-ink-dim); }
.panel { background: var(--board-panel); border: 1px solid var(--board-seam-soft); padding: 1rem 1.1rem; }
.panel p { margin: 0.25rem 0; }
.panel h2 { margin: 0.5rem 0 0.25rem; font-size: 1.15rem; }
.panel-title { display: flex; align-items: center; gap: 0.5rem; font-size: 0.72rem; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: var(--flap-ink-dim); }
.amber-text { color: var(--signal-amber); }
.status-line { display: flex; align-items: center; gap: 0.6rem; }
.lamp { width: 0.6rem; height: 0.6rem; display: inline-block; }
.lamp.blue { background: var(--signal-blue-bright); }
.lamp.amber { background: var(--signal-amber); }
.lamp.grey { background: var(--board-seam); }
.muted { color: var(--flap-ink-dim); }
.small { font-size: 0.8rem; }
.warn { color: var(--signal-amber); font-size: 0.85rem; }
.main-grid { display: grid; grid-template-columns: 1fr; gap: 1rem; }
@media (min-width: 900px) { .main-grid { grid-template-columns: 2fr 1fr; align-items: start; } }
.timer { font-size: 2.4rem; font-weight: 700; font-variant-numeric: tabular-nums; margin: 0.75rem 0; color: var(--flap-ink); }
.actions { display: flex; gap: 0.5rem; margin-top: 0.75rem; flex-wrap: wrap; }
.tiles { display: grid; grid-template-columns: 1fr 1fr; gap: 1px; background: var(--board-seam-soft); border: 1px solid var(--board-seam-soft); }
@media (min-width: 900px) { .tiles { grid-template-columns: 1fr; } }
.tile { display: flex; flex-direction: column; gap: 0.3rem; padding: 1rem; background: var(--board-panel); }
.tile-label { font-size: 0.72rem; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: var(--flap-ink-dim); }
.tile-value { font-size: 1.7rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.agenda, .stock { list-style: none; margin: 0.5rem 0 0; padding: 0; }
.agenda li { display: flex; align-items: center; gap: 0.75rem; padding: 0.55rem 0; border-top: 1px solid var(--board-seam-soft); cursor: pointer; }
.agenda li:first-child { border-top: none; }
.agenda-main { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.bar { width: 4px; align-self: stretch; background: var(--board-seam); }
.bar.Critica { background: var(--signal-red); }
.bar.Alta { background: var(--signal-amber); }
.bar.Media { background: var(--signal-blue-bright); }
.stock li { display: flex; justify-content: space-between; padding: 0.3rem 0; }
.stock .neg { color: var(--signal-red); }
.stock .low { color: var(--signal-amber); }
</style>
