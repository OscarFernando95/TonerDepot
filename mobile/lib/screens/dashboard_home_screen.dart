import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/role_names.dart';
import '../state/auth_state.dart';
import 'common/placeholder_screen.dart';
import 'dashboard/dashboard_screen.dart';
import 'home_screen.dart';
import 'portal/client_portal_home_screen.dart';

/// Contenido de la ruta "/dashboard" — el único punto de la app que decide
/// qué ve cada rol al entrar. Administrador/Coordinador ven los KPIs reales
/// (Fase D); Tecnico ve HomeScreen (self-service); Cliente ve accesos
/// directos a su portal (Fase C); Ventas todavía no tiene nada portado en
/// ningún lado (gap ya existente en frontend-web, no solo en móvil).
class DashboardHomeScreen extends StatelessWidget {
  const DashboardHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    final role = auth.currentUser?.role;

    if (role == RoleNames.tecnico) {
      return const HomeScreen();
    }
    if (role == RoleNames.administrador || role == RoleNames.coordinador) {
      return const DashboardScreen();
    }
    if (role == RoleNames.cliente) {
      return const ClientPortalHomeScreen();
    }

    return PlaceholderScreen(
      title: 'Hola, ${auth.currentUser?.fullName ?? ''}',
      message:
          'Tu rol (${role ?? ''}) todavía no tiene funciones propias en esta app.',
    );
  }
}
