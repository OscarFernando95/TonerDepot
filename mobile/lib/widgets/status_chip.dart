import 'package:flutter/material.dart';

import '../models/status_labels.dart';
import '../theme/app_theme.dart';

/// "Lámpara" de estado — versión móvil de LaneStatus.vue: color + forma +
/// texto en mayúsculas, nunca color solo (regla del sistema en DESIGN.md:
/// "The Never-Color-Alone Rule"). Cuadrado, no chip redondeado — el sistema
/// no usa esquinas curvas en ningún lado.
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.value, this.isPriority = false});

  final String value;
  final bool isPriority;

  @override
  Widget build(BuildContext context) {
    final label = isPriority
        ? (StatusLabels.priority[value] ?? value)
        : (StatusLabels.ticket[value] ?? StatusLabels.order[value] ?? value);
    final color = isPriority ? StatusLabels.priorityColor(value) : StatusLabels.colorFor(value);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.boardPanelRaised,
        border: Border.all(color: color.withValues(alpha: 0.6)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 8, height: 8, color: color),
          const SizedBox(width: 6),
          Text(
            label.toUpperCase(),
            style: TextStyle(
              color: color,
              fontWeight: FontWeight.w600,
              fontSize: 11,
              letterSpacing: 0.4,
            ),
          ),
        ],
      ),
    );
  }
}
