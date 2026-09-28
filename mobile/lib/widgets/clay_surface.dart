import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Superficie "clay": opaca, sombra dual (highlight claro + sombra oscura),
/// alto contraste — para todo contenido crítico (cards de datos, paneles de
/// estado). Nunca glass: el contenido que el técnico necesita leer con sol
/// directo no puede depender de translucidez.
class ClaySurface extends StatelessWidget {
  const ClaySurface({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.radius = 22,
    this.color,
    this.borderColor,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final double radius;
  final Color? color;

  /// Acento de color para banners de estado (error, visita en curso) — la
  /// sombra dual ya distingue la superficie del fondo, esto es solo la señal
  /// de color adicional (nunca la única, ver "Never-Color-Alone" en
  /// widgets/status_chip.dart).
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: color ?? AppColors.claySurface,
        borderRadius: BorderRadius.circular(radius),
        border: borderColor == null
            ? null
            : Border.all(color: borderColor!, width: 1.5),
        boxShadow: [
          BoxShadow(
            color: AppColors.inkPrimary.withValues(alpha: 0.10),
            offset: const Offset(6, 6),
            blurRadius: 16,
            spreadRadius: -6,
          ),
          BoxShadow(
            color: Colors.white.withValues(alpha: 0.9),
            offset: const Offset(-6, -6),
            blurRadius: 16,
            spreadRadius: -6,
          ),
        ],
      ),
      child: child,
    );
  }
}

/// ClaySurface con feedback de tap (escala al presionar) — reemplazo directo
/// de `Card(child: ...)` en listas. No usa InkWell/Material: el ripple no
/// calza en una superficie sin borde duro que lo contenga.
class ClayCard extends StatefulWidget {
  const ClayCard({
    super.key,
    required this.child,
    this.onTap,
    this.padding = const EdgeInsets.all(16),
    this.radius = 22,
    this.color,
    this.borderColor,
    this.margin = const EdgeInsets.symmetric(vertical: 6),
  });

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final double radius;
  final Color? color;
  final Color? borderColor;
  final EdgeInsetsGeometry margin;

  @override
  State<ClayCard> createState() => _ClayCardState();
}

class _ClayCardState extends State<ClayCard> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final surface = ClaySurface(
      padding: widget.padding,
      radius: widget.radius,
      color: widget.color,
      borderColor: widget.borderColor,
      child: widget.child,
    );
    final content = AnimatedScale(
      scale: _pressed ? 0.97 : 1.0,
      duration: const Duration(milliseconds: 100),
      child: surface,
    );
    final wrapped = Padding(padding: widget.margin, child: content);
    if (widget.onTap == null) return wrapped;
    return GestureDetector(
      onTapDown: (_) => setState(() => _pressed = true),
      onTapCancel: () => setState(() => _pressed = false),
      onTapUp: (_) => setState(() => _pressed = false),
      onTap: widget.onTap,
      child: wrapped,
    );
  }
}
