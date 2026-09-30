import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/assignment_history.dart';
import '../../models/role_names.dart';
import '../../models/service_ticket.dart';
import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/ticket_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/evidence_gallery.dart';
import '../../widgets/status_chip.dart';

/// Detalle de ticket — espejo de TicketDetailView.vue. Para
/// Administrador/Coordinador (RoleNames.staffRoles) agrega asignar/reasignar
/// técnico, cambiar estado e historial de asignación; Técnico y Cliente lo
/// ven en modo solo lectura (igual que en la web, donde esas acciones exigen
/// StaffRoles en el backend).
class TicketDetailScreen extends StatelessWidget {
  const TicketDetailScreen({super.key, required this.ticketId});

  final String ticketId;

  @override
  Widget build(BuildContext context) {
    final isStaff = context.read<AuthState>().hasAnyRole(RoleNames.staffRoles);
    return ChangeNotifierProvider(
      create: (_) => TicketDetailState(ApiClient.instance, ticketId, isStaff: isStaff)..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Ticket')),
        body: const _TicketDetailBody(),
      ),
    );
  }
}

class _TicketDetailBody extends StatelessWidget {
  const _TicketDetailBody();

  String _formatDateTime(String iso) {
    final dt = DateTime.tryParse(iso)?.toLocal();
    if (dt == null) return iso;
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(dt.day)}/${two(dt.month)}/${dt.year} ${two(dt.hour)}:${two(dt.minute)}';
  }

  /// Minutos → "2 h 15 min" / "45 min" / "Menos de 1 min" (espejo de TicketDetailView.vue).
  String _formatDuration(int totalMinutes) {
    if (totalMinutes < 1) return 'Menos de 1 min';
    final hours = totalMinutes ~/ 60;
    final minutes = totalMinutes % 60;
    if (hours == 0) return '$minutes min';
    return minutes == 0 ? '$hours h' : '$hours h $minutes min';
  }

  Future<void> _openAssignSheet(BuildContext context, TicketDetailState state) async {
    String? technicianId = state.ticket?.technicianId;
    final reasonController = TextEditingController();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (sheetContext, setSheetState) {
            return Padding(
              padding: EdgeInsets.fromLTRB(
                20,
                20,
                20,
                MediaQuery.of(sheetContext).viewInsets.bottom +
                    MediaQuery.of(sheetContext).padding.bottom +
                    20,
              ),
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      state.ticket?.technicianId != null ? 'Reasignar técnico' : 'Asignar técnico',
                      style: Theme.of(sheetContext).textTheme.titleLarge,
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<String>(
                      initialValue: technicianId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Técnico *'),
                      items: [
                        for (final t in state.technicians)
                          DropdownMenuItem(value: t.id, child: Text(t.fullName, overflow: TextOverflow.ellipsis)),
                      ],
                      onChanged: (value) => setSheetState(() => technicianId = value),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: reasonController,
                      minLines: 2,
                      maxLines: 4,
                      decoration: const InputDecoration(labelText: 'Motivo (opcional)'),
                    ),
                    const SizedBox(height: 20),
                    SizedBox(
                      width: double.infinity,
                      height: 52,
                      child: FilledButton(
                        onPressed: technicianId == null
                            ? null
                            : () => Navigator.of(sheetContext).pop(true),
                        child: const Text('Confirmar', style: TextStyle(fontWeight: FontWeight.w700)),
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );

    if (confirmed == true && technicianId != null && context.mounted) {
      final error = await state.assign(
        technicianId: technicianId!,
        reason: reasonController.text.trim().isEmpty ? null : reasonController.text.trim(),
      );
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? 'Ticket asignado.')),
      );
    }
  }

  Future<void> _changeStatus(BuildContext context, TicketDetailState state, String status) async {
    final error = await state.changeStatus(status);
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(error ?? 'Estado actualizado.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TicketDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.ticket == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.ticket == null) {
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
        final ticket = state.ticket!;
        final canAssign = state.isStaff && ['Abierto', 'SinAsignar', 'Asignado'].contains(ticket.status);
        final transitions = state.isStaff ? (StatusLabels.ticketAllowedTransitions[ticket.status] ?? const []) : const <String>[];
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
                            ticket.clientName,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                        StatusChip(value: ticket.status),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${ticket.clientLocationName}${ticket.cityName != null ? ' — ${ticket.cityName}' : ''}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        const Text(
                          'Prioridad: ',
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                        StatusChip.priority(ticket.priority),
                      ],
                    ),
                    const SizedBox(height: 16),
                    const Divider(height: 1, color: AppColors.neutralSoft),
                    const SizedBox(height: 16),
                    Text(
                      'Descripción',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 6),
                    Text(ticket.description),
                    if (canAssign || transitions.isNotEmpty) ...[
                      const SizedBox(height: 16),
                      const Divider(height: 1, color: AppColors.neutralSoft),
                      const SizedBox(height: 16),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          if (canAssign)
                            OutlinedButton.icon(
                              onPressed: state.assigning ? null : () => _openAssignSheet(context, state),
                              icon: const Icon(Icons.engineering_outlined, size: 18),
                              label: Text(ticket.technicianId != null ? 'Reasignar técnico' : 'Asignar técnico'),
                            ),
                          for (final s in transitions)
                            OutlinedButton.icon(
                              onPressed: state.changingStatus ? null : () => _changeStatus(context, state, s),
                              icon: const Icon(Icons.sync_alt, size: 18),
                              label: Text('Pasar a ${StatusLabels.ticket[s] ?? s}'),
                            ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 16),
              ClaySurface(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Equipo',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 8),
                    _EquipmentInfo(ticket: ticket),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              ClaySurface(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Seguimiento',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 8),
                    _InfoRow(
                      label: 'Reportado por',
                      value: ticket.reportedByUserName,
                    ),
                    _InfoRow(
                      label: 'Creado',
                      value: _formatDateTime(ticket.createdAt),
                    ),
                    if (ticket.technicianName != null)
                      _InfoRow(
                        label: 'Técnico asignado',
                        value: ticket.technicianName!,
                      ),
                    if (ticket.resolvedAt != null)
                      _InfoRow(
                        label: 'Resuelto',
                        value: _formatDateTime(ticket.resolvedAt!),
                      ),
                    if (ticket.resolvedAt != null &&
                        ticket.resolutionDurationMinutes != null)
                      _InfoRow(
                        label: 'Tiempo de resolución',
                        value: _formatDuration(ticket.resolutionDurationMinutes!),
                      ),
                    if (ticket.closedAt != null)
                      _InfoRow(
                        label: 'Cerrado',
                        value: _formatDateTime(ticket.closedAt!),
                      ),
                  ],
                ),
              ),
              if (ticket.resolutionNotes != null) ...[
                const SizedBox(height: 16),
                ClaySurface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Descripción de la resolución',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 8),
                      Text(ticket.resolutionNotes!),
                    ],
                  ),
                ),
              ],
              // Las fotos de evidencia solo las ve el staff (el endpoint no está abierto al rol Cliente).
              if (state.isStaff) ...[
                const SizedBox(height: 16),
                ClaySurface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Evidencia fotográfica', style: Theme.of(context).textTheme.titleSmall),
                      const SizedBox(height: 8),
                      EvidenceGallery(ticketId: ticket.id),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                ClaySurface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Historial de asignación', style: Theme.of(context).textTheme.titleSmall),
                      const SizedBox(height: 8),
                      if (state.history.isEmpty)
                        const Text('Sin asignaciones todavía.', style: TextStyle(color: AppColors.inkSecondary))
                      else
                        for (final h in state.history) _AssignmentHistoryRow(entry: h, formatDateTime: _formatDateTime),
                    ],
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _AssignmentHistoryRow extends StatelessWidget {
  const _AssignmentHistoryRow({required this.entry, required this.formatDateTime});

  final AssignmentHistory entry;
  final String Function(String) formatDateTime;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            margin: const EdgeInsets.only(top: 5),
            width: 8,
            height: 8,
            decoration: const BoxDecoration(
              color: AppColors.signalBlue,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  entry.technicianName ?? 'Sin técnico',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(
                  '${entry.assignmentType == 'Manual' ? 'Manual' : 'Automático'} · '
                  '${entry.assignedByUserName ?? 'Sistema (automático)'} · '
                  '${formatDateTime(entry.assignedAt)}',
                  style: const TextStyle(fontSize: 12, color: AppColors.inkSecondary),
                ),
                if (entry.reason != null && entry.reason!.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(entry.reason!, style: const TextStyle(fontSize: 12)),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _EquipmentInfo extends StatelessWidget {
  const _EquipmentInfo({required this.ticket});

  final ServiceTicket ticket;

  @override
  Widget build(BuildContext context) {
    if (!ticket.isExternal) {
      return Text(
        '${ticket.assetBrandName} ${ticket.assetModel} — Serie: ${ticket.assetSerialNumber}',
      );
    }
    if (ticket.hasExternalAssetInfo) {
      return Text(
        '${ticket.externalAssetBrand ?? ''} ${ticket.externalAssetModel ?? ''}'
        '${ticket.externalAssetCounter != null ? ' — Contador: ${ticket.externalAssetCounter}' : ''}\n'
        '(Equipo no catalogado, capturado por el técnico)',
        style: const TextStyle(color: AppColors.inkSecondary),
      );
    }
    return const Text(
      'Equipo no catalogado — todavía sin datos (el técnico los captura al cerrar la visita).',
      style: TextStyle(
        color: AppColors.inkSecondary,
        fontStyle: FontStyle.italic,
      ),
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
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 140,
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
