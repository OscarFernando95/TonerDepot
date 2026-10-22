import 'package:flutter/material.dart';

import '../models/maintenance_order.dart';
import '../models/maintenance_thread_text.dart';
import '../models/status_labels.dart';
import '../theme/app_theme.dart';
import 'clay_icon_badge.dart';
import 'clay_surface.dart';
import 'status_chip.dart';
import 'animated_gradient_border.dart';

class OrderCard extends StatelessWidget {
  const OrderCard({
    super.key,
    required this.order,
    required this.canCheckIn,
    required this.isActive,
    required this.checkingIn,
    required this.onCheckIn,
    this.onCancel,
    this.onSchedule,
    this.onOpenThread,
  });

  final MaintenanceOrder order;
  final bool canCheckIn;
  final bool isActive;
  final bool checkingIn;
  final VoidCallback onCheckIn;

  /// Solo para el mantenimiento que inició el propio técnico y aún no empezó (ver MaintenanceOrder.canCancelByTechnician).
  final VoidCallback? onCancel;

  /// Programar/Reprogramar la fecha (solo mientras la orden sigue Asignada, ver MaintenanceOrder.canScheduleByTechnician).
  final VoidCallback? onSchedule;

  /// Abre el hilo de comentarios con el cliente y el staff (solo lectura si la visita ya está en curso).
  final VoidCallback? onOpenThread;

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
              // Mismo ícono/color que _OrderListItem en
              // maintenance_orders_list_screen.dart.
              ClayIconBadge(
                icon: Icons.build_outlined,
                color: StatusLabels.colorFor(order.status),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${order.assetBrandName} ${order.assetModel}',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
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
              order.clientLocationName!,
              style: const TextStyle(color: AppColors.inkSecondary),
            ),
          if (order.cityName != null)
            Text(
              order.cityName!,
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
          const SizedBox(height: 4),
          Text(
            'Programada: ${order.scheduledDate.split('T').first}',
            style: const TextStyle(fontSize: 12),
          ),
          Text(
            'Fecha de visita: ${plannedLabelForStaff(order)}',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: order.plannedAt == null
                  ? AppColors.inkSecondary
                  : AppColors.signalBlue,
            ),
          ),
          if (onSchedule != null || onOpenThread != null)
            Wrap(
              alignment: WrapAlignment.end,
              children: [
                if (onSchedule != null && order.canScheduleByTechnician)
                  TextButton.icon(
                    onPressed: checkingIn ? null : onSchedule,
                    icon: const Icon(Icons.event_outlined, size: 18),
                    label: Text(scheduleButtonLabel(order)),
                  ),
                if (onOpenThread != null)
                  TextButton.icon(
                    onPressed: onOpenThread,
                    icon: const Icon(Icons.chat_bubble_outline, size: 18),
                    label: Text(commentsButtonLabel(order.commentCount)),
                  ),
              ],
            ),
          if (canCheckIn && !isActive) ...[
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                if (onCancel != null && order.canCancelByTechnician) ...[
                  TextButton(
                    onPressed: checkingIn ? null : onCancel,
                    child: const Text('Cancelar'),
                  ),
                  const SizedBox(width: 8),
                ],
                AnimatedGradientBorder(
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
              ],
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
