<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch, type Component } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  HomeFilled,
  OfficeBuilding,
  Printer,
  PriceTag,
  Document,
  Calendar,
  Tools,
  Tickets,
  UserFilled,
  Suitcase,
  User,
  Monitor,
  Files,
  Fold,
  Expand,
  Odometer,
  Location,
  Box,
  DataAnalysis,
  SwitchButton,
  Menu as MenuIcon,
  Close
} from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'
import * as authApi from '../api/auth'
import { RoleNames } from '../api/types'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const activeMenu = computed(() => route.name as string)

const canSeeClients = computed(() => auth.hasRole(RoleNames.Administrador, RoleNames.Coordinador))
const canSeeUsers = computed(() => auth.hasRole(RoleNames.Administrador))
const canSeeTickets = computed(() =>
  auth.hasRole(RoleNames.Administrador, RoleNames.Coordinador, RoleNames.Cliente)
)
const canSeeMyWork = computed(() => auth.hasRole(RoleNames.Tecnico))
const canSeeClientPortal = computed(() => auth.hasRole(RoleNames.Cliente))
const canSeeMeterReadings = computed(() =>
  auth.hasRole(RoleNames.Administrador, RoleNames.Coordinador, RoleNames.Tecnico)
)

interface MenuItem {
  index: string
  route: string
  icon: Component
  label: string
  description: string
  visible: () => boolean
}

const menuItems: MenuItem[] = [
  { index: 'dashboard', route: 'dashboard', icon: HomeFilled, label: 'Inicio', description: 'Resumen general de la operación', visible: () => true },
  { index: 'clients', route: 'clients', icon: OfficeBuilding, label: 'Clientes', description: 'Catálogo de clientes y sus sedes', visible: () => canSeeClients.value },
  { index: 'assets', route: 'assets', icon: Printer, label: 'Activos', description: 'Inventario y ciclo de vida de impresoras', visible: () => canSeeClients.value },
  { index: 'asset-brands', route: 'asset-brands', icon: PriceTag, label: 'Marcas', description: 'Marcas de equipos disponibles', visible: () => canSeeClients.value },
  { index: 'contracts', route: 'contracts', icon: Document, label: 'Contratos', description: 'Alquileres y condiciones por cliente', visible: () => canSeeClients.value },
  { index: 'maintenance-schedules', route: 'maintenance-schedules', icon: Calendar, label: 'Cronogramas', description: 'Programación de mantenimiento preventivo', visible: () => canSeeClients.value },
  { index: 'holidays', route: 'holidays', icon: Calendar, label: 'Festivos', description: 'Festivos de Colombia y días no laborables', visible: () => canSeeClients.value },
  { index: 'maintenance-orders', route: 'maintenance-orders', icon: Tools, label: 'Órdenes de mantenimiento', description: 'Visitas de mantenimiento generadas y su estado', visible: () => canSeeClients.value },
  { index: 'meter-readings', route: 'meter-readings', icon: Odometer, label: 'Lectura de contadores', description: 'Registra el contador de cualquier activo instalado', visible: () => canSeeMeterReadings.value },
  { index: 'my-assets', route: 'my-assets', icon: Monitor, label: 'Mis activos', description: 'Equipos instalados en tus sedes', visible: () => canSeeClientPortal.value },
  { index: 'my-contracts', route: 'my-contracts', icon: Files, label: 'Mis contratos', description: 'Tus contratos vigentes', visible: () => canSeeClientPortal.value },
  { index: 'tickets', route: 'tickets', icon: Tickets, label: 'Tickets', description: 'Soporte correctivo y su seguimiento', visible: () => canSeeTickets.value },
  { index: 'technicians', route: 'technicians', icon: UserFilled, label: 'Técnicos', description: 'Directorio y cobertura de técnicos de campo', visible: () => canSeeClients.value },
  { index: 'toner-bi', route: 'toner-bi', icon: DataAnalysis, label: 'BI de tóner', description: 'Consumo y duración del tóner por máquina', visible: () => canSeeUsers.value },
  { index: 'inventory', route: 'inventory', icon: Box, label: 'Inventario', description: 'Bodega principal e inventario por zona', visible: () => canSeeClients.value },
  { index: 'zones', route: 'zones', icon: Location, label: 'Zonas', description: 'Zonas de cobertura y sus municipios', visible: () => canSeeClients.value },
  { index: 'my-work', route: 'my-work', icon: Suitcase, label: 'Mi trabajo', description: 'Tus tickets, mantenimientos e instalaciones', visible: () => canSeeMyWork.value },
  { index: 'users', route: 'users', icon: User, label: 'Usuarios', description: 'Cuentas y roles del sistema', visible: () => canSeeUsers.value }
]

// Para el técnico, "Lectura de contadores" es su lista de máquinas: se llama como él la busca.
const visibleMenuItems = computed(() =>
  menuItems
    .filter((item) => item.visible())
    .map((item) =>
      item.index === 'meter-readings' && auth.hasRole(RoleNames.Tecnico)
        ? { ...item, icon: Printer, label: 'Mis máquinas', description: 'Tus máquinas con su contador y su tóner' }
        : item
    )
)

const collapsed = ref(localStorage.getItem('sidebar-collapsed') === 'true')
watch(collapsed, (value) => {
  localStorage.setItem('sidebar-collapsed', String(value))
})

// Below this width the rail can't coexist with content: it becomes an
// off-canvas drawer (slide-in via transform, per the detector's own fix
// for the layout-thrash it flagged on animating width) instead of an
// ever-present column that squeezes the board's tables.
const isNarrow = ref(false)
const drawerOpen = ref(false)
let narrowQuery: MediaQueryList | null = null
function syncNarrow() {
  isNarrow.value = narrowQuery?.matches ?? false
  if (!isNarrow.value) drawerOpen.value = false
}
onMounted(() => {
  narrowQuery = window.matchMedia('(max-width: 768px)')
  syncNarrow()
  narrowQuery.addEventListener('change', syncNarrow)
})
onUnmounted(() => {
  narrowQuery?.removeEventListener('change', syncNarrow)
})
watch(() => route.fullPath, () => {
  drawerOpen.value = false
})

const railWidth = computed(() => {
  if (isNarrow.value) return '248px'
  return collapsed.value ? '68px' : '248px'
})
const showLabels = computed(() => isNarrow.value || !collapsed.value)

async function handleLogout() {
  // El logout revoca la sesión en el servidor (clave para la sesión única del técnico); si falla la
  // red igual se cierra localmente — la sesión vence sola o la cierra un administrador.
  try {
    await authApi.logout()
  } catch (err) {
    console.error('No se pudo revocar la sesión en el servidor:', err)
  }
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <el-container class="board-shell">
    <div
      v-if="isNarrow && drawerOpen"
      class="rail-backdrop"
      @click="drawerOpen = false"
    ></div>

    <el-aside
      :width="railWidth"
      class="board-rail"
      :class="{ 'is-drawer': isNarrow, 'is-open': !isNarrow || drawerOpen }"
      :inert="isNarrow && !drawerOpen"
    >
      <div class="rail-brand">
        <span v-if="showLabels" class="rail-brand-mark">
          <span class="rail-brand-lamp" aria-hidden="true"></span>
          TONER
        </span>
        <el-button
          v-if="isNarrow"
          link
          class="rail-collapse"
          title="Cerrar menú"
          @click="drawerOpen = false"
        >
          <el-icon size="16"><Close /></el-icon>
        </el-button>
        <el-button
          v-else
          link
          class="rail-collapse"
          :title="collapsed ? 'Expandir menú' : 'Colapsar menú'"
          @click="collapsed = !collapsed"
        >
          <el-icon size="16"><component :is="collapsed ? Expand : Fold" /></el-icon>
        </el-button>
      </div>

      <nav class="rail-lanes" aria-label="Navegación principal">
        <el-tooltip
          v-for="item in visibleMenuItems"
          :key="item.index"
          :content="`${item.label} — ${item.description}`"
          placement="right"
          :disabled="showLabels"
        >
          <router-link
            :to="{ name: item.route }"
            class="rail-lane"
            :class="{ 'is-active': activeMenu === item.index }"
          >
            <el-icon class="rail-lane-icon"><component :is="item.icon" /></el-icon>
            <span v-if="showLabels" class="rail-lane-label">{{ item.label }}</span>
          </router-link>
        </el-tooltip>
      </nav>
    </el-aside>

    <el-container class="board-main">
      <el-header class="board-strip">
        <el-button v-if="isNarrow" link class="board-strip-menu" @click="drawerOpen = true">
          <el-icon size="18"><MenuIcon /></el-icon>
        </el-button>
        <span class="board-strip-user">{{ auth.user?.fullName }}</span>
        <span class="board-strip-role">{{ auth.user?.role }}</span>
        <el-button link class="board-strip-exit" @click="handleLogout">
          <el-icon size="14"><SwitchButton /></el-icon>
          <span>Salir</span>
        </el-button>
      </el-header>
      <el-main class="board-content">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.board-shell {
  /* A fixed viewport height (not min-height) keeps the rail and header
     in place; only .board-content scrolls internally when a page's
     content runs long, instead of the whole document scrolling and
     carrying the rail away with it. */
  height: 100vh;
  overflow: hidden;
  background: var(--board-bg);
}

.board-rail {
  background: var(--board-panel);
  border-right: 1px solid var(--board-seam-soft);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.board-rail.is-drawer {
  position: fixed;
  inset: 0 auto 0 0;
  z-index: 20;
  transform: translateX(-100%);
  transition: transform 0.2s ease-out;
  box-shadow: 0 0 32px rgba(0, 0, 0, 0.5);
}

.board-rail.is-drawer.is-open {
  transform: translateX(0);
}

.rail-backdrop {
  position: fixed;
  inset: 0;
  background: var(--scrim);
  z-index: 15;
}

.rail-brand {
  padding: 1.1rem 1rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  white-space: nowrap;
  border-bottom: 1px solid var(--board-seam-soft);
}

.rail-brand-mark {
  font-family: var(--font-display);
  font-weight: 700;
  font-size: var(--text-lg);
  letter-spacing: 0.06em;
  color: var(--flap-ink);
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.rail-brand-lamp {
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: var(--signal-blue-bright);
  flex: none;
}

.rail-collapse {
  color: var(--flap-ink-dim);
}
.rail-collapse:hover {
  color: var(--flap-ink);
}

.rail-lanes {
  display: flex;
  flex-direction: column;
  padding: 0.5rem 0;
  overflow-y: auto;
}

.rail-lane {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.55rem 1rem;
  text-decoration: none;
  color: var(--flap-ink-dim);
  white-space: nowrap;
}

.rail-lane:hover {
  background: var(--board-panel-raised);
  color: var(--flap-ink);
}

.rail-lane.is-active {
  background: var(--signal-blue-wash);
  color: var(--flap-ink);
}

.rail-lane.is-active .rail-lane-icon {
  color: var(--signal-blue-bright);
}

.rail-lane-icon {
  flex: none;
}

.rail-lane-label {
  font-size: var(--text-md);
  overflow: hidden;
  text-overflow: ellipsis;
}

.board-main {
  background: var(--board-bg);
  /* Flex item's implicit min-width:auto would let any page's wide table
     push the whole shell into horizontal scroll instead of scrolling
     inside its own panel. */
  min-width: 0;
}

.board-strip {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  border-bottom: 1px solid var(--board-seam-soft);
  background: var(--board-panel);
  padding: 0 1.25rem;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-xs);
  color: var(--flap-ink-dim);
}

.board-strip-menu {
  color: var(--flap-ink);
  margin-right: 0.25rem;
}

.board-strip-user {
  color: var(--flap-ink);
  font-weight: 600;
}

.board-strip-role {
  color: var(--flap-ink-dim);
  border: 1px solid var(--board-seam);
  padding: 0.15rem 0.4rem;
}

.board-strip-exit {
  margin-left: auto;
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--flap-ink-dim);
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-xs);
}
.board-strip-exit:hover {
  color: var(--signal-red);
}

.board-content {
  padding: 1.5rem;
}

@media (max-width: 768px) {
  .board-strip {
    padding: 0 0.85rem;
    gap: 0.5rem;
  }
  .board-content {
    padding: 1rem;
  }
}
</style>
