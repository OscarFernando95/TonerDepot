import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/role_names.dart';
import '../screens/dashboard_home_screen.dart';
import '../screens/meter_readings_screen.dart';
import '../screens/my_work_screen.dart';
import '../screens/assets/asset_brands_list_screen.dart';
import '../screens/assets/assets_list_screen.dart';
import '../screens/analytics/toner_bi_screen.dart';
import '../screens/clients/clients_list_screen.dart';
import '../screens/contracts/contracts_list_screen.dart';
import '../screens/holidays/holidays_screen.dart';
import '../screens/inventory/inventory_screen.dart';
import '../screens/users/users_list_screen.dart';
import '../screens/zones/zones_screen.dart';
import '../screens/maintenance/maintenance_orders_list_screen.dart';
import '../screens/maintenance/maintenance_schedules_list_screen.dart';
import '../screens/portal/my_assets_screen.dart';
import '../screens/portal/my_contracts_screen.dart';
import '../screens/technicians/technicians_list_screen.dart';
import '../screens/tickets/tickets_list_screen.dart';

/// Un destino de navegación es a la vez: (a) una ruta de go_router, y (b) un
/// item del Drawer si el rol actual puede verla. Una sola lista en vez de
/// los dos mapas paralelos (rutas permitidas por rol + items del menú) que
/// tendría el mirror literal de meta.roles/AppLayout.vue del frontend web —
/// así no hay que mantenerlos sincronizados a mano.
class AppDestination {
  const AppDestination({
    required this.name,
    required this.path,
    required this.label,
    this.shortLabel,
    required this.icon,
    required this.roles,
    required this.builder,
  });

  final String name;
  final String path;
  final String label;

  /// Nombre corto para la barra inferior, donde no cabe el nombre completo del módulo.
  final String? shortLabel;
  final IconData icon;
  final List<String> roles;
  final Widget Function(BuildContext context, GoRouterState state) builder;
}

/// Árbol completo de navegación por rol: el Drawer se arma dinámicamente
/// filtrando esta lista por el rol del usuario.
final List<AppDestination> kAppDestinations = [
  AppDestination(
    name: 'dashboard',
    path: '/dashboard',
    label: 'Inicio',
    icon: Icons.home_outlined,
    roles: RoleNames.all,
    builder: (context, state) => const DashboardHomeScreen(),
  ),
  AppDestination(
    name: 'tickets',
    path: '/tickets',
    label: 'Tickets',
    icon: Icons.confirmation_number_outlined,
    roles: RoleNames.staffAndClientRoles,
    builder: (context, state) => const TicketsListScreen(),
  ),
  AppDestination(
    name: 'my-work',
    path: '/my-work',
    label: 'Mi trabajo',
    icon: Icons.work_outline,
    roles: [RoleNames.tecnico],
    builder: (context, state) => const MyWorkScreen(),
  ),
  AppDestination(
    name: 'meter-readings',
    path: '/meter-readings',
    label: 'Lectura de contadores',
    shortLabel: 'Contadores',
    icon: Icons.speed_outlined,
    roles: RoleNames.staffAndTechnicianRoles,
    builder: (context, state) => const MeterReadingsScreen(),
  ),
  AppDestination(
    name: 'my-assets',
    path: '/my-assets',
    label: 'Mis activos',
    icon: Icons.inventory_2_outlined,
    roles: [RoleNames.cliente],
    builder: (context, state) => const MyAssetsScreen(),
  ),
  AppDestination(
    name: 'my-contracts',
    path: '/my-contracts',
    label: 'Mis contratos',
    icon: Icons.assignment_outlined,
    roles: [RoleNames.cliente],
    builder: (context, state) => const MyContractsScreen(),
  ),
  AppDestination(
    name: 'maintenance-orders',
    path: '/maintenance-orders',
    label: 'Órdenes de mantenimiento',
    shortLabel: 'Órdenes',
    icon: Icons.build_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const MaintenanceOrdersListScreen(),
  ),
  AppDestination(
    name: 'technicians',
    path: '/technicians',
    label: 'Técnicos',
    icon: Icons.engineering_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const TechniciansListScreen(),
  ),
  AppDestination(
    name: 'inventory',
    path: '/inventory',
    label: 'Inventario',
    icon: Icons.warehouse_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const InventoryScreen(),
  ),
  AppDestination(
    name: 'toner-bi',
    path: '/toner-bi',
    label: 'BI de tóner',
    icon: Icons.insights_outlined,
    roles: [RoleNames.administrador],
    builder: (context, state) => const TonerBiScreen(),
  ),
  AppDestination(
    name: 'zones',
    path: '/zones',
    label: 'Zonas',
    icon: Icons.map_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const ZonesScreen(),
  ),
  AppDestination(
    name: 'clients',
    path: '/clients',
    label: 'Clientes',
    icon: Icons.business_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const ClientsListScreen(),
  ),
  AppDestination(
    name: 'assets',
    path: '/assets',
    label: 'Activos',
    icon: Icons.print_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const AssetsListScreen(),
  ),
  AppDestination(
    name: 'asset-brands',
    path: '/asset-brands',
    label: 'Marcas de activo',
    icon: Icons.category_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const AssetBrandsListScreen(),
  ),
  AppDestination(
    name: 'contracts',
    path: '/contracts',
    label: 'Contratos',
    icon: Icons.description_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const ContractsListScreen(),
  ),
  AppDestination(
    name: 'holidays',
    path: '/holidays',
    label: 'Festivos',
    icon: Icons.event_busy_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const HolidaysScreen(),
  ),
  AppDestination(
    name: 'maintenance-schedules',
    path: '/maintenance-schedules',
    label: 'Programaciones',
    icon: Icons.event_repeat_outlined,
    roles: RoleNames.staffRoles,
    builder: (context, state) => const MaintenanceSchedulesListScreen(),
  ),
  AppDestination(
    name: 'users',
    path: '/users',
    label: 'Usuarios',
    icon: Icons.admin_panel_settings_outlined,
    roles: [RoleNames.administrador],
    builder: (context, state) => const UsersListScreen(),
  ),
];

/// Barra inferior: SOLO los módulos más usados de cada rol, en este orden (máximo 5). El resto vive en el menú lateral,
/// que lista todos los módulos del rol. Un rol sin entrada aquí muestra todos sus destinos (son pocos).
const Map<String, List<String>> kBottomNavByRole = {
  RoleNames.administrador: [
    'dashboard',
    'tickets',
    'maintenance-orders',
    'assets',
    'inventory',
  ],
  RoleNames.coordinador: [
    'dashboard',
    'tickets',
    'maintenance-orders',
    'technicians',
    'clients',
  ],
  RoleNames.tecnico: ['dashboard', 'my-work', 'meter-readings'],
  RoleNames.cliente: ['dashboard', 'tickets', 'my-assets', 'my-contracts'],
};
