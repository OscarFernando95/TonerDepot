import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../models/managed_user.dart';
import '../models/role_names.dart';
import '../screens/app_shell.dart';
import '../screens/assets/asset_brand_detail_screen.dart';
import '../screens/assets/asset_create_screen.dart';
import '../screens/assets/asset_detail_screen.dart';
import '../screens/change_password_screen.dart';
import '../screens/clients/client_create_screen.dart';
import '../screens/clients/client_detail_screen.dart';
import '../screens/contracts/contract_create_screen.dart';
import '../screens/contracts/contract_detail_screen.dart';
import '../screens/login_screen.dart';
import '../screens/maintenance/maintenance_order_detail_screen.dart';
import '../screens/maintenance/maintenance_schedule_detail_screen.dart';
import '../screens/technicians/technician_coverage_screen.dart';
import '../screens/technicians/technician_schedule_screen.dart';
import '../screens/technicians/technician_time_off_screen.dart';
import '../screens/technicians/technician_visits_screen.dart';
import '../screens/tickets/ticket_detail_screen.dart';
import '../screens/users/user_create_screen.dart';
import '../screens/users/user_detail_screen.dart';
import '../state/auth_state.dart';
import 'app_destinations.dart';

const _splashPath = '/splash';
const _loginPath = '/login';
const _changePasswordPath = '/change-password';
const _defaultAuthenticatedPath = '/dashboard';

/// Rutas de detalle (push, sin chrome de AppShell) que no son un destino del
/// Drawer y por lo tanto no están en kAppDestinations — necesitan su propia
/// entrada de roles acá para que el guard no las deje abiertas a cualquiera.
const _extraRouteRoles = {
  'ticket-detail': RoleNames.staffAndClientRoles,
  'maintenance-order-detail': RoleNames.staffRoles,
  'maintenance-schedule-detail': RoleNames.staffRoles,
  'technician-coverage': RoleNames.staffRoles,
  'technician-schedule': RoleNames.staffRoles,
  'technician-visits': RoleNames.staffRoles,
  'technician-time-off': RoleNames.staffRoles,
  'client-create': RoleNames.staffRoles,
  'client-detail': RoleNames.staffRoles,
  'asset-create': RoleNames.staffRoles,
  'asset-detail': RoleNames.staffRoles,
  'asset-brand-detail': RoleNames.staffRoles,
  'contract-create': RoleNames.staffRoles,
  'contract-detail': RoleNames.staffRoles,
  'user-create': [RoleNames.administrador],
  'user-detail': [RoleNames.administrador],
};

/// Reemplaza a AuthGate (antes en main.dart): un único `redirect`, igual al
/// guard de 4 pasos de frontend-web/src/router/index.ts (líneas 148-172) —
/// splash mientras se resuelve la sesión, login si no hay sesión,
/// cambio de contraseña obligatorio, y por último el rol de la ruta.
GoRouter buildAppRouter(AuthState auth) {
  final rolesByRouteName = {
    for (final d in kAppDestinations) d.name: d.roles,
    ..._extraRouteRoles,
  };

  return GoRouter(
    refreshListenable: auth,
    initialLocation: _splashPath,
    redirect: (context, state) {
      final loc = state.matchedLocation;

      if (auth.status == AuthStatus.unknown) {
        return loc == _splashPath ? null : _splashPath;
      }
      if (auth.status == AuthStatus.unauthenticated) {
        return loc == _loginPath ? null : _loginPath;
      }
      if (auth.currentUser?.mustChangePassword ?? false) {
        return loc == _changePasswordPath ? null : _changePasswordPath;
      }
      if (loc == _splashPath ||
          loc == _loginPath ||
          loc == _changePasswordPath) {
        return _defaultAuthenticatedPath;
      }
      final allowedRoles = rolesByRouteName[state.name];
      if (allowedRoles != null && !auth.hasAnyRole(allowedRoles)) {
        return _defaultAuthenticatedPath;
      }
      return null;
    },
    routes: [
      GoRoute(
        path: _splashPath,
        name: 'splash',
        builder: (context, state) => const _SplashScreen(),
      ),
      GoRoute(
        path: _loginPath,
        name: 'login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: _changePasswordPath,
        name: 'change-password',
        builder: (context, state) => const ChangePasswordScreen(),
      ),
      _pushedRoute(
        path: '/tickets/:id',
        name: 'ticket-detail',
        builder: (context, state) =>
            TicketDetailScreen(ticketId: state.pathParameters['id']!),
      ),
      _pushedRoute(
        path: '/maintenance-orders/:id',
        name: 'maintenance-order-detail',
        builder: (context, state) =>
            MaintenanceOrderDetailScreen(orderId: state.pathParameters['id']!),
      ),
      _pushedRoute(
        path: '/maintenance-schedules/:id',
        name: 'maintenance-schedule-detail',
        builder: (context, state) => MaintenanceScheduleDetailScreen(
          scheduleId: state.pathParameters['id']!,
        ),
      ),
      _pushedRoute(
        path: '/technicians/:id/schedule',
        name: 'technician-schedule',
        builder: (context, state) => TechnicianScheduleScreen(
          technicianId: state.pathParameters['id']!,
          technicianName: state.extra as String?,
        ),
      ),
      _pushedRoute(
        path: '/technicians/:id/visits',
        name: 'technician-visits',
        builder: (context, state) => TechnicianVisitsScreen(
          technicianId: state.pathParameters['id']!,
          technicianName: state.extra as String?,
        ),
      ),
      _pushedRoute(
        path: '/technicians/:id/time-off',
        name: 'technician-time-off',
        builder: (context, state) => TechnicianTimeOffScreen(
          technicianId: state.pathParameters['id']!,
          technicianName: state.extra as String?,
        ),
      ),
      _pushedRoute(
        path: '/technicians/:id',
        name: 'technician-coverage',
        builder: (context, state) => TechnicianCoverageScreen(
          technicianId: state.pathParameters['id']!,
          technicianName: state.extra as String?,
        ),
      ),
      _pushedRoute(
        path: '/clients/new',
        name: 'client-create',
        builder: (context, state) => const ClientCreateScreen(),
      ),
      _pushedRoute(
        path: '/clients/:id',
        name: 'client-detail',
        builder: (context, state) =>
            ClientDetailScreen(clientId: state.pathParameters['id']!),
      ),
      _pushedRoute(
        path: '/assets/new',
        name: 'asset-create',
        builder: (context, state) => const AssetCreateScreen(),
      ),
      _pushedRoute(
        path: '/assets/:id',
        name: 'asset-detail',
        builder: (context, state) =>
            AssetDetailScreen(assetId: state.pathParameters['id']!),
      ),
      _pushedRoute(
        path: '/contracts/new',
        name: 'contract-create',
        builder: (context, state) => const ContractCreateScreen(),
      ),
      _pushedRoute(
        path: '/contracts/:id',
        name: 'contract-detail',
        builder: (context, state) =>
            ContractDetailScreen(contractId: state.pathParameters['id']!),
      ),
      _pushedRoute(
        path: '/users/new',
        name: 'user-create',
        builder: (context, state) => const UserCreateScreen(),
      ),
      _pushedRoute(
        path: '/users/:id',
        name: 'user-detail',
        builder: (context, state) =>
            UserDetailScreen(user: state.extra as ManagedUser),
      ),
      _pushedRoute(
        path: '/asset-brands/:id',
        name: 'asset-brand-detail',
        builder: (context, state) => AssetBrandDetailScreen(
          brandId: state.pathParameters['id']!,
          brandName: state.extra as String?,
        ),
      ),
      ShellRoute(
        builder: (context, state, child) => AppShell(child: child),
        routes: [
          for (final destination in kAppDestinations)
            GoRoute(
              path: destination.path,
              name: destination.name,
              builder: destination.builder,
            ),
        ],
      ),
    ],
  );
}

/// GoRoute de una pantalla de detalle/creación (push, sin chrome de AppShell).
/// Estas pantallas no tienen la barra inferior del shell, así que sin esto sus
/// botones y listas quedan bajo la barra de navegación/gestos del teléfono.
GoRoute _pushedRoute({
  required String path,
  required String name,
  required GoRouterWidgetBuilder builder,
}) {
  return GoRoute(
    path: path,
    name: name,
    builder: (context, state) => _BottomSafeArea(child: builder(context, state)),
  );
}

/// Reserva el inset inferior del sistema. El ColoredBox pinta el fondo de la
/// app en esa franja, para que no se vea el color por defecto del Navigator.
class _BottomSafeArea extends StatelessWidget {
  const _BottomSafeArea({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Theme.of(context).scaffoldBackgroundColor,
      child: SafeArea(top: false, child: child),
    );
  }
}

class _SplashScreen extends StatelessWidget {
  const _SplashScreen();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}
