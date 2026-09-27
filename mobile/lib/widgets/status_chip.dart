import 'package:flutter/material.dart';

import '../models/status_labels.dart';
import '../theme/app_theme.dart';

enum _StatusDomain { ticketOrOrder, priority, assetLifecycle, contract, technicianStatus, activeState }

/// "Lámpara" de estado — versión móvil de LaneStatus.vue: color + forma +
/// texto en mayúsculas, nunca color solo (regla "Never-Color-Alone" del
/// sistema anterior, que sigue aplicando aunque el resto del lenguaje visual
/// haya cambiado a clay/glass). Cuadrado, para que se distinga de las
/// ClayCard/ClaySurface (esas sí redondeadas) que lo contienen.
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.value, this.isPriority = false}) : _domain = _StatusDomain.ticketOrOrder;

  const StatusChip.priority(this.value, {super.key})
      : isPriority = true,
        _domain = _StatusDomain.priority;

  const StatusChip.assetLifecycle(this.value, {super.key})
      : isPriority = false,
        _domain = _StatusDomain.assetLifecycle;

  const StatusChip.contract(this.value, {super.key})
      : isPriority = false,
        _domain = _StatusDomain.contract;

  const StatusChip.technicianStatus(this.value, {super.key})
      : isPriority = false,
        _domain = _StatusDomain.technicianStatus;

  /// Para el par activo/inactivo que se repite en Clientes, Sedes y Usuarios
  /// — no viene de un enum del backend, así que no hay mapa en StatusLabels,
  /// se resuelve directo en el switch de abajo.
  StatusChip.activeState(bool isActive, {super.key})
      : value = isActive.toString(),
        isPriority = false,
        _domain = _StatusDomain.activeState;

  final String value;
  final bool isPriority;
  final _StatusDomain _domain;

  @override
  Widget build(BuildContext context) {
    final String label;
    final Color color;
    switch (_domain) {
      case _StatusDomain.priority:
        label = StatusLabels.priority[value] ?? value;
        color = StatusLabels.priorityColor(value);
      case _StatusDomain.assetLifecycle:
        label = StatusLabels.assetLifecycle[value] ?? value;
        color = StatusLabels.assetLifecycleColor(value);
      case _StatusDomain.contract:
        label = StatusLabels.contract[value] ?? value;
        color = StatusLabels.contractColor(value);
      case _StatusDomain.technicianStatus:
        label = StatusLabels.technicianStatus[value] ?? value;
        color = StatusLabels.technicianStatusColor(value);
      case _StatusDomain.ticketOrOrder:
        label = StatusLabels.ticket[value] ?? StatusLabels.order[value] ?? value;
        color = StatusLabels.colorFor(value);
      case _StatusDomain.activeState:
        label = value == 'true' ? 'Activo' : 'Inactivo';
        color = value == 'true' ? AppColors.signalBlue : AppColors.neutral;
    }

    return Container(
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
