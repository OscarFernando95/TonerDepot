import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/technician.dart';
import '../../services/api_client.dart';
import '../../state/maintenance_order_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/evidence_gallery.dart';
import '../../widgets/status_chip.dart';

/// Detalle de orden de mantenimiento — Coordinador/Administrador. Espejo de
/// MaintenanceOrderDetailView.vue: asignar/completar/cancelar solo visibles
/// cuando `order.canManage` (Pendiente/Asignada), igual que la web.
class MaintenanceOrderDetailScreen extends StatelessWidget {
  const MaintenanceOrderDetailScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) =>
          MaintenanceOrderDetailState(ApiClient.instance, orderId)..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Orden de mantenimiento')),
        body: const _OrderDetailBody(),
      ),
    );
  }
}

class _OrderDetailBody extends StatelessWidget {
  const _OrderDetailBody();

  Future<void> _showAssignDialog(
    BuildContext context,
    MaintenanceOrderDetailState state,
  ) async {
    String? technicianId;
    final reasonController = TextEditingController();
    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Asignar técnico'),
          content: SizedBox(
            width: double.maxFinite,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField<String>(
                  initialValue: technicianId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Técnico'),
                  items: [
                    for (final Technician tech in state.technicians)
                      DropdownMenuItem(
                        value: tech.id,
                        child: Text(
                          tech.fullName,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) =>
                      setDialogState(() => technicianId = value),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: reasonController,
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
              onPressed: technicianId == null
                  ? null
                  : () => Navigator.of(dialogContext).pop(true),
              child: const Text('Asignar'),
            ),
          ],
        ),
      ),
    );
    if (result == true && technicianId != null && context.mounted) {
      final error = await state.assign(
        technicianId!,
        reason: reasonController.text.trim().isEmpty
            ? null
            : reasonController.text.trim(),
      );
      if (context.mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error ?? 'Técnico asignado.')));
      }
    }
  }

  Future<void> _showCompleteDialog(
    BuildContext context,
    MaintenanceOrderDetailState state,
  ) async {
    final counterController = TextEditingController();
    DateTime? readingDate = DateTime.now();
    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Completar orden'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              TextField(
                controller: counterController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Lectura de contador',
                ),
              ),
              const SizedBox(height: 12),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(
                  'Fecha: ${readingDate!.day}/${readingDate!.month}/${readingDate!.year}',
                ),
                trailing: const Icon(Icons.calendar_today_outlined, size: 18),
                onTap: () async {
                  final picked = await showDatePicker(
                    context: dialogContext,
                    initialDate: readingDate!,
                    firstDate: DateTime(2020),
                    lastDate: DateTime.now(),
                  );
                  if (picked != null) {
                    setDialogState(() => readingDate = picked);
                  }
                },
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: int.tryParse(counterController.text.trim()) == null
                  ? null
                  : () => Navigator.of(dialogContext).pop(true),
              child: const Text('Completar'),
            ),
          ],
        ),
      ),
    );
    if (result == true && context.mounted) {
      final error = await state.complete(
        int.parse(counterController.text.trim()),
        readingDate,
      );
      if (context.mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error ?? 'Orden completada.')));
      }
    }
  }

  Future<void> _showCancelConfirm(
    BuildContext context,
    MaintenanceOrderDetailState state,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('¿Cancelar esta orden?'),
        content: const Text('Esta acción no se puede deshacer.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('No'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Sí, cancelar'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      final error = await state.cancel();
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error ?? 'Orden cancelada.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<MaintenanceOrderDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.order == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.order == null) {
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
        final order = state.order!;
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
                            '${order.assetBrandName} ${order.assetModel}',
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                        StatusChip(value: order.status),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Serie: ${order.assetSerialNumber}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    if (order.clientLocationName != null)
                      Text(
                        '${order.clientLocationName}${order.cityName != null ? ' — ${order.cityName}' : ''}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    const SizedBox(height: 12),
                    _InfoRow(
                      label: 'Programada',
                      value: order.scheduledDate.split('T').first,
                    ),
                    if (order.technicianName != null)
                      _InfoRow(label: 'Técnico', value: order.technicianName!),
                    if (order.completedAt != null)
                      _InfoRow(
                        label: 'Completada',
                        value: order.completedAt!.split('T').first,
                      ),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      children: [
                        if (order.includesGeneral)
                          const _IncludeChip(label: 'Mantenimiento general'),
                        if (order.includesUnits)
                          const _IncludeChip(label: 'Unidades'),
                        if (order.includesConsumables)
                          const _IncludeChip(label: 'Insumos'),
                      ],
                    ),
                  ],
                ),
              ),
              if (order.canManage) ...[
                const SizedBox(height: 16),
                Row(
                  children: [
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: state.busyWithAction
                            ? null
                            : () => _showAssignDialog(context, state),
                        icon: const Icon(Icons.person_add_alt, size: 18),
                        label: const Text('Asignar'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: state.busyWithAction
                            ? null
                            : () => _showCompleteDialog(context, state),
                        icon: const Icon(Icons.check_circle_outline, size: 18),
                        label: const Text('Completar'),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: state.busyWithAction
                        ? null
                        : () => _showCancelConfirm(context, state),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColors.signalRed,
                      side: const BorderSide(color: AppColors.signalRed),
                    ),
                    icon: const Icon(Icons.cancel_outlined, size: 18),
                    label: const Text('Cancelar orden'),
                  ),
                ),
              ],
              const SizedBox(height: 16),
              ClaySurface(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Evidencia fotográfica', style: Theme.of(context).textTheme.titleSmall),
                    const SizedBox(height: 8),
                    EvidenceGallery(orderId: order.id),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(color: AppColors.inkSecondary),
            ),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}

class _IncludeChip extends StatelessWidget {
  const _IncludeChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.neutralSoft,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(
        label,
        style: const TextStyle(fontSize: 11, color: AppColors.inkSecondary),
      ),
    );
  }
}
