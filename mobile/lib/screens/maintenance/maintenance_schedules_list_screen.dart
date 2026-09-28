import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/maintenance_schedule.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/maintenance_schedules_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de MaintenanceSchedulesView.vue. Las acciones globales
/// (regenerar faltantes / evaluar ahora) van en una barra de botones bajo el
/// AppBar del shell; "Evaluar ahora" solo para Administrador, como en la web.
class MaintenanceSchedulesListScreen extends StatelessWidget {
  const MaintenanceSchedulesListScreen({super.key});

  static const _urgencies = {
    'overdue': 'Vencido',
    'urgent': 'Muy próximo',
    'soon': 'Próximo',
    'far': 'Lejano',
  };

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MaintenanceSchedulesState(ApiClient.instance)..load(),
      child: Builder(
        builder: (context) => Scaffold(
          backgroundColor: Colors.transparent,
          body: Consumer<MaintenanceSchedulesState>(
            builder: (context, state, _) {
              final isAdmin = context.read<AuthState>().hasRole(
                RoleNames.administrador,
              );
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                    child: Row(
                      children: [
                        Expanded(
                          child: OutlinedButton(
                            onPressed: state.busyWithAction
                                ? null
                                : () => _run(context, state.backfill),
                            child: const Text('Regenerar faltantes'),
                          ),
                        ),
                        if (isAdmin) ...[
                          const SizedBox(width: 8),
                          Expanded(
                            child: OutlinedButton(
                              onPressed: state.busyWithAction
                                  ? null
                                  : () => _run(context, state.evaluateNow),
                              child: const Text('Evaluar ahora'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: Row(
                        children: [
                          ChoiceChip(
                            label: const Text('Todos'),
                            selected: state.urgencyFilter == null,
                            onSelected: (_) => state.setUrgencyFilter(null),
                          ),
                          for (final entry in _urgencies.entries)
                            Padding(
                              padding: const EdgeInsets.only(left: 6),
                              child: ChoiceChip(
                                label: Text(entry.value),
                                selected: state.urgencyFilter == entry.key,
                                onSelected: (_) =>
                                    state.setUrgencyFilter(entry.key),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ),
                  Expanded(child: _Body(state: state)),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  static Future<void> _run(
    BuildContext context,
    Future<String> Function() action,
  ) async {
    final message = await action();
    if (context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
    }
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.state});

  final MaintenanceSchedulesState state;

  @override
  Widget build(BuildContext context) {
    if (state.loading && state.schedules.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.error != null && state.schedules.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            state.error!,
            style: const TextStyle(color: AppColors.signalRed),
            textAlign: TextAlign.center,
          ),
        ),
      );
    }
    final schedules = state.filtered;
    if (schedules.isEmpty) {
      return const PlaceholderScreen(
        title: 'Sin cronogramas',
        message: 'No hay cronogramas que coincidan con el filtro.',
      );
    }
    return RefreshIndicator(
      onRefresh: state.load,
      child: NotificationListener<ScrollNotification>(
        onNotification: (notification) {
          if (notification.metrics.pixels >=
              notification.metrics.maxScrollExtent - 200) {
            state.loadMore();
          }
          return false;
        },
        child: ListView.builder(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
          itemCount: schedules.length + (state.hasMore ? 1 : 0),
          itemBuilder: (context, index) {
            if (index >= schedules.length) {
              return const Padding(
                padding: EdgeInsets.symmetric(vertical: 16),
                child: Center(child: CircularProgressIndicator()),
              );
            }
            return _ScheduleItem(schedule: schedules[index]);
          },
        ),
      ),
    );
  }
}

class _ScheduleItem extends StatelessWidget {
  const _ScheduleItem({required this.schedule});

  final MaintenanceSchedule schedule;

  @override
  Widget build(BuildContext context) {
    final prints = schedule.printsRemaining;
    final days = schedule.daysRemaining;
    return ClayCard(
      onTap: () => context.push('/maintenance-schedules/${schedule.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '${schedule.assetBrandName} ${schedule.assetModel}',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.urgency(schedule.urgency),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Serie: ${schedule.assetSerialNumber}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          Text(
            [
              schedule.clientName,
              if (schedule.cityName != null) schedule.cityName!,
            ].join(' — '),
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(height: 6),
          Text(
            [
              if (prints != null) '$prints impr.',
              if (days != null) '$days días',
              if (!schedule.isActive) 'Pausado',
            ].join(' · '),
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}
