import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/role_names.dart';
import '../../models/service_ticket.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/ticket_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/evidence_gallery.dart';
import '../../widgets/status_chip.dart';

/// Detalle de ticket — solo lectura por ahora para todos los roles que
/// llegan aquí (Cliente no puede cambiar estado del suyo: PATCH
/// /tickets/{id}/status es StaffRoles únicamente; las acciones de asignar/
/// cambiar estado para Coordinador llegan en la Fase E junto con el resto de
/// la operación diaria).
class TicketDetailScreen extends StatelessWidget {
  const TicketDetailScreen({super.key, required this.ticketId});

  final String ticketId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TicketDetailState(ApiClient.instance, ticketId)..load(),
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
                    if (ticket.closedAt != null)
                      _InfoRow(
                        label: 'Cerrado',
                        value: _formatDateTime(ticket.closedAt!),
                      ),
                  ],
                ),
              ),
              // Las fotos de evidencia solo las ve el staff (el endpoint no está abierto al rol Cliente).
              if (context.read<AuthState>().hasAnyRole(RoleNames.staffRoles)) ...[
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
              ],
            ],
          ),
        );
      },
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
