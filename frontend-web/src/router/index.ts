import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { RoleNames } from '../api/types'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/LoginView.vue'),
      meta: { public: true }
    },
    {
      path: '/',
      component: () => import('../layouts/AppLayout.vue'),
      redirect: { name: 'dashboard' },
      children: [
        {
          path: 'dashboard',
          name: 'dashboard',
          component: () => import('../views/DashboardView.vue')
        },
        {
          path: 'change-password',
          name: 'change-password',
          component: () => import('../views/auth/ChangePasswordView.vue')
        },
        {
          path: 'clients',
          name: 'clients',
          component: () => import('../views/clients/ClientsListView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'clients/:id',
          name: 'client-detail',
          component: () => import('../views/clients/ClientDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'assets',
          name: 'assets',
          component: () => import('../views/assets/AssetsListView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'assets/:id',
          name: 'asset-detail',
          component: () => import('../views/assets/AssetDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'asset-brands',
          name: 'asset-brands',
          component: () => import('../views/assets/AssetBrandsView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'asset-brands/:id',
          name: 'asset-brand-detail',
          component: () => import('../views/assets/AssetBrandDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'contracts',
          name: 'contracts',
          component: () => import('../views/contracts/ContractsListView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'contracts/:id',
          name: 'contract-detail',
          component: () => import('../views/contracts/ContractDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'maintenance-schedules',
          name: 'maintenance-schedules',
          component: () => import('../views/maintenance/MaintenanceSchedulesView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'holidays',
          name: 'holidays',
          component: () => import('../views/maintenance/HolidaysView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'meter-readings',
          name: 'meter-readings',
          component: () => import('../views/assets/MeterReadingsView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador, RoleNames.Tecnico] }
        },
        {
          path: 'maintenance-orders',
          name: 'maintenance-orders',
          component: () => import('../views/maintenance/MaintenanceOrdersView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'maintenance-orders/:id',
          name: 'maintenance-order-detail',
          component: () => import('../views/maintenance/MaintenanceOrderDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'inventory',
          name: 'inventory',
          component: () => import('../views/inventory/InventoryView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'zones',
          name: 'zones',
          component: () => import('../views/technicians/ZonesView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'technicians',
          name: 'technicians',
          component: () => import('../views/technicians/TechniciansView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador] }
        },
        {
          path: 'tickets',
          name: 'tickets',
          component: () => import('../views/tickets/TicketsListView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador, RoleNames.Cliente] }
        },
        {
          path: 'tickets/:id',
          name: 'ticket-detail',
          component: () => import('../views/tickets/TicketDetailView.vue'),
          meta: { roles: [RoleNames.Administrador, RoleNames.Coordinador, RoleNames.Cliente] }
        },
        {
          path: 'my-assets',
          name: 'my-assets',
          component: () => import('../views/portal/MyAssetsView.vue'),
          meta: { roles: [RoleNames.Cliente] }
        },
        {
          path: 'my-contracts',
          name: 'my-contracts',
          component: () => import('../views/portal/MyContractsView.vue'),
          meta: { roles: [RoleNames.Cliente] }
        },
        {
          path: 'my-work',
          name: 'my-work',
          component: () => import('../views/technicians/MyWorkView.vue'),
          meta: { roles: [RoleNames.Tecnico] }
        },
        {
          path: 'users',
          name: 'users',
          component: () => import('../views/users/UsersView.vue'),
          meta: { roles: [RoleNames.Administrador] }
        }
      ]
    }
  ]
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  if (to.meta.public) {
    return true
  }

  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  // Bloquea toda navegación hasta que el usuario cambie la contraseña genérica — el backend refuerza
  // esto mismo con el claim must_change_password (ver MustChangePasswordMiddleware), este guard es solo
  // la parte de UX. "Salir" sigue accesible porque el botón de logout vive en AppLayout, no en una ruta.
  if (auth.user?.mustChangePassword && to.name !== 'change-password') {
    return { name: 'change-password' }
  }

  const allowedRoles = to.meta.roles as string[] | undefined
  if (allowedRoles && !auth.hasRole(...allowedRoles)) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
