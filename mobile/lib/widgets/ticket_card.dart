import 'package:flutter/material.dart';

import '../models/service_ticket.dart';
import '../models/status_labels.dart';
import '../theme/app_theme.dart';
import 'clay_icon_badge.dart';
import 'clay_surface.dart';
import 'status_chip.dart';
import 'animated_gradient_border.dart';

class TicketCard extends StatelessWidget {
  const TicketCard({
    super.key,
    required this.ticket,
    required this.canCheckIn,
    required this.isActive,
    required this.checkingIn,
    required this.onCheckIn,
  });

  final ServiceTicket ticket;
  final bool canCheckIn;
  final bool isActive;
  final bool checkingIn;
  final VoidCallback onCheckIn;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      color: isActive ? AppColors.claySurfaceRaised : null,
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              // Mismo ícono/color que _TicketListItem en tickets_list_screen.dart
              // (icono del dominio + color por prioridad).
              ClayIconBadge(
                icon: Icons.confirmation_number_outlined,
                color: StatusLabels.priorityColor(ticket.priority),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  ticket.clientName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip(value: ticket.priority, isPriority: true),
              const SizedBox(width: 6),
              StatusChip(value: ticket.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            ticket.clientLocationName,
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          if (ticket.cityName != null)
            Text(
              ticket.cityName!,
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
          const SizedBox(height: 6),
          Text(ticket.description),
          if (ticket.isExternal) ...[
            const SizedBox(height: 4),
            const Text(
              'Equipo no catalogado (cliente externo)',
              style: TextStyle(fontSize: 12, fontStyle: FontStyle.italic),
            ),
          ] else if (ticket.assetBrandName != null) ...[
            const SizedBox(height: 4),
            Text(
              '${ticket.assetBrandName} ${ticket.assetModel ?? ''} — ${ticket.assetSerialNumber ?? ''}',
              style: const TextStyle(fontSize: 12),
            ),
          ],
          if (canCheckIn && !isActive) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: AnimatedGradientBorder(
                backgroundColor: isActive
                    ? AppColors.claySurfaceRaised
                    : AppColors.claySurface,
                child: FilledButton.icon(
                  onPressed: checkingIn ? null : onCheckIn,
                  icon: checkingIn
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.login, size: 18),
                  label: const Text('Check-in'),
                ),
              ),
            ),
          ],
          if (isActive) ...[
            const SizedBox(height: 8),
            const Align(
              alignment: Alignment.centerRight,
              child: Text(
                'Visita en curso',
                style: TextStyle(
                  color: AppColors.signalBlueBright,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
