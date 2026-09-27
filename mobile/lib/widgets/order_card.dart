import 'package:flutter/material.dart';

import '../models/maintenance_order.dart';
import '../theme/app_theme.dart';
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
  });

  final MaintenanceOrder order;
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
                Expanded(
                  child: Text('${order.assetBrandName} ${order.assetModel}',
                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                ),
                StatusChip(value: order.status),
              ],
            ),
            const SizedBox(height: 4),
            Text('Serie: ${order.assetSerialNumber}', style: const TextStyle(color: AppColors.inkSecondary)),
            if (order.clientLocationName != null) Text(order.clientLocationName!, style: const TextStyle(color: AppColors.inkSecondary)),
            if (order.cityName != null) Text(order.cityName!, style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12)),
            const SizedBox(height: 4),
            Text('Programada: ${order.scheduledDate.split('T').first}', style: const TextStyle(fontSize: 12)),
            if (canCheckIn && !isActive) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: AnimatedGradientBorder(
                  backgroundColor: isActive ? AppColors.claySurfaceRaised : AppColors.claySurface,
                  child: FilledButton.icon(
                    onPressed: checkingIn ? null : onCheckIn,
                    icon: checkingIn
                        ? const SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 2))
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
                child: Text('Visita en curso', style: TextStyle(color: AppColors.signalBlueBright, fontWeight: FontWeight.bold)),
              ),
            ],
          ],
        ),
    );
  }
}
