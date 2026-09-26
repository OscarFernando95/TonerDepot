import 'package:flutter/material.dart';

import '../models/pending_installation.dart';
import '../theme/app_theme.dart';
import 'animated_gradient_border.dart';

class InstallationCard extends StatelessWidget {
  const InstallationCard({
    super.key,
    required this.installation,
    required this.canCheckIn,
    required this.isActive,
    required this.checkingIn,
    required this.onCheckIn,
  });

  final PendingInstallation installation;
  final bool canCheckIn;
  final bool isActive;
  final bool checkingIn;
  final VoidCallback onCheckIn;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 6),
      color: isActive ? AppColors.boardPanelRaised : null,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text('${installation.assetBrandName} ${installation.model}',
                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                ),
                if (installation.takenByAnotherTechnician)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.boardPanelRaised,
                      border: Border.all(color: AppColors.signalAmber.withValues(alpha: 0.6)),
                    ),
                    child: const Text(
                      'TOMADA',
                      style: TextStyle(color: AppColors.signalAmber, fontWeight: FontWeight.w600, fontSize: 11, letterSpacing: 0.4),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text('Serie: ${installation.serialNumber}', style: const TextStyle(color: AppColors.flapInkDim)),
            Text('${installation.clientName} — ${installation.clientLocationName}',
                style: const TextStyle(color: AppColors.flapInkDim)),
            if (installation.cityName != null)
              Text(installation.cityName!, style: const TextStyle(color: AppColors.flapInkDim, fontSize: 12)),
            if (canCheckIn && !isActive && !installation.takenByAnotherTechnician) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: AnimatedGradientBorder(
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
            if (installation.takenByAnotherTechnician && !isActive) ...[
              const SizedBox(height: 6),
              const Text('Otro técnico ya la está atendiendo.',
                  style: TextStyle(color: AppColors.flapInkDim, fontSize: 12, fontStyle: FontStyle.italic)),
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
      ),
    );
  }
}
