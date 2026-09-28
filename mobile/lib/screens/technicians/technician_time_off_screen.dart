import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/technician_schedule.dart';
import '../../services/api_client.dart';
import '../../state/technician_time_off_state.dart';
import '../../theme/app_theme.dart';
import '../../utils/date_only.dart';
import '../../widgets/clay_surface.dart';

/// "Fuera de la oficina" de un técnico: permisos, vacaciones, incapacidades (no es un retiro). Espejo de
/// TechnicianTimeOffDialog.vue.
class TechnicianTimeOffScreen extends StatelessWidget {
  const TechnicianTimeOffScreen({
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
          TechnicianTimeOffState(ApiClient.instance, technicianId)..load(),
      child: Scaffold(
        appBar: AppBar(
          title: Text(
            'Fuera de la oficina${technicianName == null ? '' : ' — $technicianName'}',
          ),
        ),
        floatingActionButton: Builder(
          builder: (context) => FloatingActionButton.extended(
            onPressed: () =>
                _showAddDialog(context, context.read<TechnicianTimeOffState>()),
            icon: const Icon(Icons.add),
            label: const Text('Marcar fuera'),
          ),
        ),
        body: const _Body(),
      ),
    );
  }
}

Future<DateTime?> _pickDateTime(BuildContext context, DateTime initial) async {
  final date = await showDatePicker(
    context: context,
    initialDate: initial,
    firstDate: DateTime.now().subtract(const Duration(days: 1)),
    lastDate: DateTime.now().add(const Duration(days: 366)),
  );
  if (date == null || !context.mounted) return null;
  final time = await showTimePicker(
    context: context,
    initialTime: TimeOfDay.fromDateTime(initial),
    builder: (context, child) => MediaQuery(
      data: MediaQuery.of(context).copyWith(alwaysUse24HourFormat: true),
      child: child!,
    ),
  );
  if (time == null) return null;
  return DateTime(date.year, date.month, date.day, time.hour, time.minute);
}

Future<void> _showAddDialog(
  BuildContext context,
  TechnicianTimeOffState state,
) async {
  var startsAt = DateTime.now();
  var endsAt = DateTime.now().add(const Duration(hours: 8));
  final reasonController = TextEditingController();

  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (dialogContext, setDialogState) => AlertDialog(
        title: const Text('Marcar fuera de la oficina'),
        content: SizedBox(
          width: double.maxFinite,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Inicio'),
                subtitle: Text(formatDateTimeShort(startsAt)),
                onTap: () async {
                  final picked = await _pickDateTime(dialogContext, startsAt);
                  if (picked != null) setDialogState(() => startsAt = picked);
                },
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Fin'),
                subtitle: Text(formatDateTimeShort(endsAt)),
                onTap: () async {
                  final picked = await _pickDateTime(dialogContext, endsAt);
                  if (picked != null) setDialogState(() => endsAt = picked);
                },
              ),
              TextField(
                controller: reasonController,
                maxLength: 300,
                decoration: const InputDecoration(
                  labelText: 'Motivo (opcional)',
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: endsAt.isAfter(startsAt)
                ? () => Navigator.of(dialogContext).pop(true)
                : null,
            child: const Text('Guardar'),
          ),
        ],
      ),
    ),
  );
  if (confirmed != true || !context.mounted) return;

  final reason = reasonController.text.trim();
  final error = await state.add(
    startsAt: startsAt,
    endsAt: endsAt,
    reason: reason.isEmpty ? null : reason,
  );
  if (context.mounted) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(error ?? 'Técnico marcado fuera de la oficina.')),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  Future<void> _cancel(
    BuildContext context,
    TechnicianTimeOffState state,
    TimeOff item,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(item.isActive ? '¿Terminar ahora?' : '¿Anular período?'),
        content: Text(
          item.isActive
              ? 'El técnico vuelve a estar disponible desde ahora.'
              : 'Se anula este período fuera de la oficina.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Confirmar'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    final error = await state.cancel(item.id);
    if (context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error ?? 'Listo.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<TechnicianTimeOffState>();
    if (state.loading && state.items.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.error != null && state.items.isEmpty) {
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
    if (state.items.isEmpty) {
      return const Center(
        child: Text(
          'Sin períodos registrados.',
          style: TextStyle(color: AppColors.inkSecondary),
        ),
      );
    }
    return RefreshIndicator(
      onRefresh: state.load,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
        itemCount: state.items.length,
        itemBuilder: (context, index) {
          final item = state.items[index];
          return ClayCard(
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${formatDateTimeShort(item.startsAt)}  →  ${formatDateTimeShort(item.effectiveEndsAt)}',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      Text(
                        item.statusLabel,
                        style: const TextStyle(
                          color: AppColors.inkSecondary,
                          fontSize: 12,
                        ),
                      ),
                      if (item.reason != null)
                        Text(
                          item.reason!,
                          style: const TextStyle(color: AppColors.inkSecondary),
                        ),
                    ],
                  ),
                ),
                if (item.canCancel)
                  TextButton(
                    onPressed: state.busy
                        ? null
                        : () => _cancel(context, state, item),
                    child: Text(item.isActive ? 'Terminar' : 'Anular'),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
