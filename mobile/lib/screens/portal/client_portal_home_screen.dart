import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../state/auth_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';

/// Inicio del portal Cliente — accesos directos a lo que ya existe (Fase C).
/// Equivalente Cliente del gate-welcome de DashboardView.vue, pero con
/// atajos reales en vez de solo un saludo.
class ClientPortalHomeScreen extends StatelessWidget {
  const ClientPortalHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text('Hola,', style: const TextStyle(color: AppColors.inkSecondary, fontSize: 14)),
        Text(
          auth.currentUser?.fullName ?? '',
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 24),
        _ShortcutCard(
          icon: Icons.confirmation_number_outlined,
          title: 'Tickets',
          subtitle: 'Reporta un problema o revisa el estado de tus tickets',
          onTap: () => context.push('/tickets'),
        ),
        const SizedBox(height: 12),
        _ShortcutCard(
          icon: Icons.inventory_2_outlined,
          title: 'Mis activos',
          subtitle: 'Equipos instalados en tus sedes',
          onTap: () => context.push('/my-assets'),
        ),
        const SizedBox(height: 12),
        _ShortcutCard(
          icon: Icons.assignment_outlined,
          title: 'Mis contratos',
          subtitle: 'Vigencia y equipos asociados',
          onTap: () => context.push('/my-contracts'),
        ),
      ],
    );
  }
}

class _ShortcutCard extends StatelessWidget {
  const _ShortcutCard({required this.icon, required this.title, required this.subtitle, required this.onTap});

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: const BoxDecoration(shape: BoxShape.circle, color: AppColors.signalBlueWash),
            child: Icon(icon, color: AppColors.signalBlue),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                const SizedBox(height: 2),
                Text(subtitle, style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12)),
              ],
            ),
          ),
          const Icon(Icons.chevron_right, color: AppColors.inkSecondary),
        ],
      ),
    );
  }
}
