import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../state/auth_state.dart';
import '../state/my_work_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';

/// Contenido de "/dashboard" para el rol Tecnico (ver DashboardHomeScreen) —
/// no tiene equivalente directo en el frontend web (ahí el dashboard es de
/// Admin/Coordinador); aquí es un saludo + resumen rápido para el técnico,
/// con el mismo lenguaje visual del tablero.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    final work = context.watch<MyWorkState>();

    // "Pendiente" = asignado pero sin empezar (status Asignado/Asignada, o
    // instalación libre) — en cuanto se hace check-in, el backend pasa el
    // ticket/orden a EnProceso, así que sale solo de esta cuenta. Para
    // instalaciones, el backend marca "takenByAnotherTechnician" también
    // cuando el check-in abierto es del propio técnico (ver AssetService.
    // ListPendingInstallationsAsync) — por eso ya excluye la que uno mismo
    // tiene activa sin lógica extra aquí.
    final pendingTickets = work.tickets.where((t) => t.status == 'Asignado').length;
    final pendingOrders = work.orders.where((o) => o.status == 'Asignada').length;
    final pendingInstallations = work.pendingInstallations.where((i) => !i.takenByAnotherTechnician).length;
    final pendingCount = pendingTickets + pendingOrders + pendingInstallations;
    final hasPending = pendingCount > 0;
    final inProgress = work.isBusy;

    return RefreshIndicator(
      onRefresh: work.loadAll,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text('Hola,', style: TextStyle(color: AppColors.inkSecondary, fontSize: 14)),
          Text(
            auth.currentUser?.fullName ?? '',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 4),
          Text(
            (auth.currentUser?.role ?? '').toUpperCase(),
            style: const TextStyle(color: AppColors.inkSecondary, letterSpacing: 1, fontSize: 12),
          ),
          const SizedBox(height: 24),
          ClaySurface(
            child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      if (hasPending)
                        const _StatusDot(color: AppColors.signalAmber, blink: true)
                      else
                        Container(width: 12, height: 12, color: AppColors.neutral),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          hasPending ? 'Tienes trabajo asignado sin iniciar' : 'No tienes trabajo pendiente por ahora',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                      ),
                    ],
                  ),
                  if (inProgress) ...[
                    const SizedBox(height: 12),
                    const Divider(height: 1, color: AppColors.neutralSoft),
                    const SizedBox(height: 12),
                    const Row(
                      children: [
                        _StatusDot(color: AppColors.signalBlueBright, blink: false),
                        SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            'Tienes una visita en curso sin cerrar',
                            style: TextStyle(fontWeight: FontWeight.w600, color: AppColors.signalBlueBright),
                          ),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _StatTile(label: 'Tickets', value: pendingTickets)),
              const SizedBox(width: 12),
              Expanded(child: _StatTile(label: 'Órdenes', value: pendingOrders)),
              const SizedBox(width: 12),
              Expanded(child: _StatTile(label: 'Instalaciones', value: pendingInstallations)),
            ],
          ),
          const SizedBox(height: 28),
          FilledButton.icon(
            onPressed: () => context.push('/my-work'),
            icon: const Icon(Icons.work_outline),
            label: const Text('Ir a Mi trabajo'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => context.push('/meter-readings'),
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.inkPrimary,
              side: const BorderSide(color: AppColors.neutral),
              shape: const RoundedRectangleBorder(),
              padding: const EdgeInsets.symmetric(vertical: 14),
            ),
            icon: const Icon(Icons.speed_outlined),
            label: const Text('Registrar lectura de contador'),
          ),
        ],
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.label, required this.value});
  final String label;
  final int value;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label.toUpperCase(), style: const TextStyle(color: AppColors.inkSecondary, fontSize: 10, letterSpacing: 0.5)),
          const SizedBox(height: 6),
          Text(
            '$value',
            style: Theme.of(context)
                .textTheme
                .headlineSmall
                ?.copyWith(fontWeight: FontWeight.bold)
                .merge(AppTextStyles.tabularNumber),
          ),
        ],
      ),
    );
  }
}

/// Lámpara de estado del panel "Inicio": cuadrado de color que nunca se
/// apaga del todo (piso alto de opacidad) más un resplandor (glow) que
/// respira al mismo ritmo — se lee como una luz encendida, no como un
/// simple parpadeo débil. `blink: false` la deja fija (para "en curso", que
/// es un estado calmado, no una alerta) y `blink: true` la hace parpadear
/// (para "pendiente sin iniciar", que sí pide atención).
class _StatusDot extends StatefulWidget {
  const _StatusDot({required this.color, required this.blink});

  final Color color;
  final bool blink;

  @override
  State<_StatusDot> createState() => _StatusDotState();
}

class _StatusDotState extends State<_StatusDot> with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 500));
    if (widget.blink) {
      _controller.repeat(reverse: true);
    } else {
      _controller.value = 1;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        // t oscila entre 0.55 y 1.0 (o queda fijo en 1.0 si no parpadea) —
        // nunca se apaga del todo, para que se vea "brillante" y no débil.
        final t = 0.55 + (0.45 * _controller.value);
        return Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: widget.color.withValues(alpha: t),
            boxShadow: [
              BoxShadow(color: widget.color.withValues(alpha: t * 0.85), blurRadius: 10 * t, spreadRadius: 2 * t),
            ],
          ),
        );
      },
    );
  }
}
