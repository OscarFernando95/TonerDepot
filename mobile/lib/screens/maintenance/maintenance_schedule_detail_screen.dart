import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/maintenance_schedule.dart';
import '../../services/api_client.dart';
import '../../state/maintenance_schedule_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../../utils/date_only.dart';

String _date(String? iso) =>
    iso == null ? '—' : formatDateOnly(DateTime.parse(iso).toLocal());

class MaintenanceScheduleDetailScreen extends StatelessWidget {
  const MaintenanceScheduleDetailScreen({super.key, required this.scheduleId});

  final String scheduleId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) =>
          MaintenanceScheduleDetailState(ApiClient.instance, scheduleId)
            ..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Cronograma')),
        body: const _Body(),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  Future<void> _toggle(
    BuildContext context,
    MaintenanceScheduleDetailState state,
    MaintenanceSchedule schedule,
  ) async {
    final next = !schedule.isActive;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(next ? '¿Activar cronograma?' : '¿Pausar cronograma?'),
        content: Text('${schedule.assetBrandName} ${schedule.assetModel}'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(next ? 'Activar' : 'Pausar'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    final error = await state.setStatus(next);
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? 'Cronograma actualizado.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<MaintenanceScheduleDetailState>();
    final schedule = state.schedule;
    if (state.loading && schedule == null) {
      return const Center(child: CircularProgressIndicator());
    }
    if (schedule == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            state.error ?? 'No se pudo cargar el cronograma.',
            style: const TextStyle(color: AppColors.signalRed),
            textAlign: TextAlign.center,
          ),
        ),
      );
    }
    final prints = schedule.printsRemaining;
    final days = schedule.daysRemaining;

    return RefreshIndicator(
      onRefresh: state.load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ClaySurface(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        '${schedule.assetBrandName} ${schedule.assetModel}',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ),
                    StatusChip.urgency(schedule.urgency),
                  ],
                ),
                Text(
                  'Serie: ${schedule.assetSerialNumber}',
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
                Text(
                  schedule.clientName,
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
                if (schedule.clientLocationName != null ||
                    schedule.cityName != null)
                  Text(
                    [
                      ?schedule.clientLocationName,
                      ?schedule.cityName,
                    ].join(' — '),
                    style: const TextStyle(color: AppColors.inkSecondary),
                  ),
                if (schedule.area != null)
                  Text(
                    'Área: ${schedule.area}',
                    style: const TextStyle(color: AppColors.inkSecondary),
                  ),
                const SizedBox(height: 12),
                Text('Último contador: ${schedule.lastKnownCounter ?? '—'}'),
                Text(
                  'Último mantenimiento: ${_date(schedule.lastMaintenanceAt)}'
                  '${schedule.lastMaintenanceCodes.isEmpty ? '' : ' (${schedule.lastMaintenanceCodes.join(', ')})'}',
                ),
                Text(
                  'Próximo: ${schedule.nextMaintenanceAt == null ? 'por contador' : _date(schedule.nextMaintenanceAt)} / ${schedule.nextMaintenanceCounter}'
                  '${schedule.nextMaintenanceCodes.isEmpty ? '' : ' (${schedule.nextMaintenanceCodes.join(', ')})'}',
                ),
                Text(
                  'Faltante: ${[if (prints != null) '$prints impr.', if (days != null) '$days días'].join(' · ')}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: schedule.isActive
                      ? OutlinedButton(
                          onPressed: state.busyWithAction
                              ? null
                              : () => _toggle(context, state, schedule),
                          child: const Text('Pausar cronograma'),
                        )
                      : FilledButton(
                          onPressed: state.busyWithAction
                              ? null
                              : () => _toggle(context, state, schedule),
                          child: const Text('Activar cronograma'),
                        ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Órdenes generadas',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 8),
          if (state.orders.isEmpty)
            const Text(
              'Este cronograma todavía no generó órdenes.',
              style: TextStyle(color: AppColors.inkSecondary),
            )
          else
            for (final order in state.orders)
              ClayCard(
                onTap: () => context.push('/maintenance-orders/${order.id}'),
                child: Row(
                  children: [
                    Expanded(
                      child: Text('Programada: ${_date(order.scheduledDate)}'),
                    ),
                    StatusChip(value: order.status),
                  ],
                ),
              ),
        ],
      ),
    );
  }
}
