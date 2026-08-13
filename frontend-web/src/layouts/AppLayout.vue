<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
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

function handleLogout() {
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <el-container class="app-shell">
    <el-aside width="220px" class="app-sidebar">
      <div class="app-brand">Toner</div>
      <el-menu :default-active="activeMenu" router class="app-menu" background-color="transparent">
        <el-menu-item index="dashboard" :route="{ name: 'dashboard' }">
          <span>Inicio</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="clients" :route="{ name: 'clients' }">
          <span>Clientes</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="cities" :route="{ name: 'cities' }">
          <span>Ciudades</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="assets" :route="{ name: 'assets' }">
          <span>Activos</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="asset-brands" :route="{ name: 'asset-brands' }">
          <span>Marcas</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="contracts" :route="{ name: 'contracts' }">
          <span>Contratos</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="maintenance-schedules" :route="{ name: 'maintenance-schedules' }">
          <span>Cronogramas</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="maintenance-orders" :route="{ name: 'maintenance-orders' }">
          <span>Órdenes de mantenimiento</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClientPortal" index="my-assets" :route="{ name: 'my-assets' }">
          <span>Mis activos</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClientPortal" index="my-contracts" :route="{ name: 'my-contracts' }">
          <span>Mis contratos</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeTickets" index="tickets" :route="{ name: 'tickets' }">
          <span>Tickets</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeClients" index="technicians" :route="{ name: 'technicians' }">
          <span>Técnicos</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeMyWork" index="my-work" :route="{ name: 'my-work' }">
          <span>Mi trabajo</span>
        </el-menu-item>
        <el-menu-item v-if="canSeeUsers" index="users" :route="{ name: 'users' }">
          <span>Usuarios</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="app-header">
        <div class="app-header-user">
          <span class="app-header-name">{{ auth.user?.fullName }}</span>
          <el-tag size="small" type="info">{{ auth.user?.role }}</el-tag>
          <el-button link @click="handleLogout">Salir</el-button>
        </div>
      </el-header>
      <el-main>
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
}

.app-sidebar {
  background-color: #1f2937;
  color: white;
  display: flex;
  flex-direction: column;
  --el-menu-text-color: #d1d5db;
  --el-menu-hover-text-color: white;
  --el-menu-hover-bg-color: #374151;
  --el-menu-active-color: white;
  --el-menu-bg-color: transparent;
}

.app-brand {
  padding: 1.25rem 1rem;
  font-size: 1.25rem;
  font-weight: 600;
  color: white;
}

.app-menu {
  border-right: none;
}

.app-header {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  border-bottom: 1px solid #e5e7eb;
  background: white;
}

.app-header-user {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.app-header-name {
  font-weight: 500;
}
</style>
