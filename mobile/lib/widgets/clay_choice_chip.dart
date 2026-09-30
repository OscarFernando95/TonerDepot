import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Reemplazo directo de `ChoiceChip` (mismo constructor: `label`,
/// `selected`, `onSelected`) con el lenguaje "Clay elevado" — el
/// `ChoiceChip` de Material 3 sin tema propio sale lila/rosado pastel
/// (tonal palette derivada del seed color), que no tiene nada que ver con
/// la paleta de la app. Seleccionado: azul señal sólido + sombra, blanco +
/// check. Sin seleccionar: superficie clay con borde sutil.
class ClayChoiceChip extends StatelessWidget {
  const ClayChoiceChip({
    super.key,
    required this.label,
    required this.selected,
    required this.onSelected,
  });

  final Widget label;
  final bool selected;
  final ValueChanged<bool> onSelected;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () => onSelected(!selected),
      behavior: HitTestBehavior.opaque,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        curve: Curves.easeOut,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
        decoration: BoxDecoration(
          color: selected ? AppColors.signalBlue : AppColors.claySurfaceRaised,
          borderRadius: BorderRadius.circular(999),
          border: selected
              ? null
              : Border.all(color: AppColors.neutralSoft),
          boxShadow: selected
              ? [
                  BoxShadow(
                    color: AppColors.signalBlue.withValues(alpha: 0.35),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: DefaultTextStyle(
          style: TextStyle(
            fontSize: 13,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
            color: selected ? Colors.white : AppColors.inkPrimary,
          ),
          child: IconTheme(
            data: IconThemeData(
              color: selected ? Colors.white : AppColors.inkPrimary,
              size: 16,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (selected) ...[
                  const Icon(Icons.check, size: 16),
                  const SizedBox(width: 6),
                ],
                label,
              ],
            ),
          ),
        ),
      ),
    );
  }
}
