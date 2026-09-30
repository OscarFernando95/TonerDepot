import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/technician_schedule.dart';
import '../../services/api_client.dart';
import '../../state/technician_schedule_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

// Lunes primero; el backend usa 0 = domingo.
const _days = [
  (1, 'Lunes'),
  (2, 'Martes'),
  (3, 'Miércoles'),
  (4, 'Jueves'),
  (5, 'Viernes'),
  (6, 'Sábado'),
  (0, 'Domingo'),
];

/// Horario laboral semanal de un técnico (Staff). Espejo de TechnicianScheduleDialog.vue.
class TechnicianScheduleScreen extends StatelessWidget {
  const TechnicianScheduleScreen({
    super.key,
    required this.technicianId,
    this.technicianName,
  });

  final String technicianId;
  final String? technicianName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) =>
          TechnicianScheduleState(ApiClient.instance, technicianId)..load(),
      child: Scaffold(
        appBar: AppBar(
          title: Text(
            'Horario${technicianName == null ? '' : ' — $technicianName'}',
          ),
        ),
        body: const _Body(),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  Future<void> _pickTime(
    BuildContext context,
    TechnicianScheduleState state,
    WorkInterval interval, {
    required bool isStart,
  }) async {
    final current = isStart ? interval.start : interval.end;
    final parts = current.split(':');
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(
        hour: int.parse(parts[0]),
        minute: int.parse(parts[1]),
      ),
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(alwaysUse24HourFormat: true),
        child: child!,
      ),
    );
    if (picked == null) return;
    final value =
        '${picked.hour.toString().padLeft(2, '0')}:${picked.minute.toString().padLeft(2, '0')}';
    state.setTime(
      interval,
      start: isStart ? value : null,
      end: isStart ? null : value,
    );
  }

  Future<void> _run(
    BuildContext context,
    Future<String?> Function() action,
    String successMessage,
  ) async {
    final error = await action();
    if (!context.mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(error ?? successMessage)));
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<TechnicianScheduleState>();
    if (state.loading && state.timeZoneId.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.error != null && state.timeZoneId.isEmpty) {
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

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          'Hora local (${state.timeZoneId}). Un día sin tramos no se trabaja; los festivos de Colombia se descuentan solos.'
          '${state.isDefault ? ' Hoy usa el horario general de la empresa.' : ''}',
          style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
        ),
        const SizedBox(height: 12),
        for (final (day, label) in _days)
          ClaySurface(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    // Azul si el día tiene tramos de trabajo, gris si está
                    // libre — mismo lenguaje "insignia" del resto de la app,
                    // aplicado aquí por fila de día en vez de por fila de lista.
                    ClayIconBadge(
                      icon: Icons.schedule_outlined,
                      color: state.days[day]!.isEmpty
                          ? AppColors.neutral
                          : AppColors.signalBlue,
                      size: 28,
                      iconSize: 14,
                    ),
                    const SizedBox(width: 10),
                    Text(
                      label,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                if (state.days[day]!.isEmpty)
                  const Text(
                    'No trabaja',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                for (final (index, interval) in state.days[day]!.indexed)
                  Row(
                    children: [
                      TextButton(
                        onPressed: () =>
                            _pickTime(context, state, interval, isStart: true),
                        child: Text(interval.start),
                      ),
                      const Text('a'),
                      TextButton(
                        onPressed: () =>
                            _pickTime(context, state, interval, isStart: false),
                        child: Text(interval.end),
                      ),
                      const Spacer(),
                      IconButton(
                        tooltip: 'Quitar tramo',
                        onPressed: () => state.removeInterval(day, index),
                        icon: const Icon(
                          Icons.delete_outline,
                          color: AppColors.signalRed,
                        ),
                      ),
                    ],
                  ),
                TextButton.icon(
                  onPressed: () => state.addInterval(day),
                  icon: const Icon(Icons.add, size: 18),
                  label: const Text('Agregar tramo'),
                ),
              ],
            ),
          ),
        const SizedBox(height: 12),
        FilledButton(
          onPressed: state.saving || !state.hasAnyInterval
              ? null
              : () => _run(context, state.save, 'Horario guardado.'),
          child: const Text('Guardar horario'),
        ),
        const SizedBox(height: 8),
        OutlinedButton(
          onPressed: state.saving || state.isDefault
              ? null
              : () => _run(
                  context,
                  state.resetToDefault,
                  'Se restableció el horario de la empresa.',
                ),
          child: const Text('Usar horario de la empresa'),
        ),
      ],
    );
  }
}
