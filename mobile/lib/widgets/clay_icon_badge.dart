import 'package:flutter/material.dart';

/// Insignia de ícono cuadrada redondeada con fondo de color "wash" (10-16%
/// de opacidad) — el mismo lenguaje del prototipo "Clay elevado": nunca
/// color solo (el ícono en sí ya distingue el tipo), el wash es refuerzo
/// visual, no el único portador de significado.
class ClayIconBadge extends StatelessWidget {
  const ClayIconBadge({
    super.key,
    required this.icon,
    required this.color,
    this.size = 34,
    this.iconSize = 17,
  });

  final IconData icon;
  final Color color;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(size * 0.32),
      ),
      alignment: Alignment.center,
      child: Icon(icon, size: iconSize, color: color),
    );
  }
}
