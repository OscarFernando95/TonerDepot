import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Segmentado con relieve ("Clay elevado"): pista hundida (sombra interior
/// simulada con un borde+fondo canvas) y el segmento activo flotando encima
/// con sombra de color — reemplazo directo de un Row de ChoiceChip/TabBar
/// para grupos de 2-4 opciones (filtros, pestañas). Ver DESIGN.md, dirección
/// visual "Opción A" elegida para toda la app.
class ClaySegmentedControl<T> extends StatelessWidget {
  const ClaySegmentedControl({
    super.key,
    required this.segments,
    required this.selected,
    required this.onChanged,
  });

  final List<ClaySegment<T>> segments;
  final T selected;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.canvasBg,
        borderRadius: BorderRadius.circular(14),
        boxShadow: [
          BoxShadow(
            color: AppColors.inkPrimary.withValues(alpha: 0.08),
            blurRadius: 6,
            offset: const Offset(0, 1),
          ),
        ],
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final segment in segments)
            Expanded(
              child: _Segment<T>(
                segment: segment,
                active: segment.value == selected,
                onTap: () => onChanged(segment.value),
              ),
            ),
        ],
      ),
    );
  }
}

class ClaySegment<T> {
  const ClaySegment({required this.value, required this.label});

  final T value;
  final String label;
}

class _Segment<T> extends StatelessWidget {
  const _Segment({
    required this.segment,
    required this.active,
    required this.onTap,
  });

  final ClaySegment<T> segment;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        curve: Curves.easeOut,
        padding: const EdgeInsets.symmetric(vertical: 9),
        decoration: BoxDecoration(
          color: active ? AppColors.signalBlue : Colors.transparent,
          borderRadius: BorderRadius.circular(11),
          boxShadow: active
              ? [
                  BoxShadow(
                    color: AppColors.signalBlue.withValues(alpha: 0.4),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        alignment: Alignment.center,
        child: Text(
          segment.label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: active ? FontWeight.w700 : FontWeight.w600,
            color: active ? Colors.white : AppColors.inkSecondary,
          ),
        ),
      ),
    );
  }
}
