import 'package:flutter/material.dart';

import '../models/rental_retirement.dart';
import '../theme/app_theme.dart';

/// Aviso informativo de salida de alquiler: chip ámbar "Próxima a salir de
/// alquiler" / rojo "Superó el límite de alquiler" + cuántas impresiones
/// faltan o se pasó. Mismo lenguaje visual que StatusChip (cuadrado, color +
/// forma + texto). No pinta nada si no hay aviso (sin límite, sin lecturas o
/// rol Cliente, que nunca recibe estos datos).
class RentalRetirementBadge extends StatelessWidget {
  const RentalRetirementBadge({super.key, required this.alert});

  final RentalRetirementAlert? alert;

  @override
  Widget build(BuildContext context) {
    final a = alert;
    if (a == null) return const SizedBox.shrink();
    final color = a.exceeded ? AppColors.signalRed : AppColors.signalAmber;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          decoration: BoxDecoration(
            color: AppColors.claySurfaceRaised,
            border: Border.all(color: color.withValues(alpha: 0.9)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(width: 8, height: 8, color: color),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  a.label.toUpperCase(),
                  style: TextStyle(
                    color: color,
                    fontWeight: FontWeight.w600,
                    fontSize: 11,
                    letterSpacing: 0.4,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (a.detail != null)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(
              a.detail!,
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ).merge(AppTextStyles.tabularNumber),
            ),
          ),
      ],
    );
  }
}
