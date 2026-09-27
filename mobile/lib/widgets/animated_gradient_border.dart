import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Borde con degradado que gira sin parar — se usa alrededor del botón
/// "Check-in" cuando hay trabajo pendiente, para que llame la atención sin
/// depender solo de un color fijo (mismo espíritu que "nunca solo color" de
/// las lámparas de estado, aplicado aquí como acento de atención). Radio de
/// esquina igual al de FilledButtonTheme (ver app_theme.dart) para que el
/// anillo no quede cuadrado alrededor de un botón redondeado.
class AnimatedGradientBorder extends StatefulWidget {
  const AnimatedGradientBorder({
    super.key,
    required this.child,
    this.borderWidth = 2,
    this.radius = 16,
    this.backgroundColor = AppColors.claySurface,
    this.colors = const [
      AppColors.signalBlue,
      AppColors.signalBlueBright,
      AppColors.signalAmber,
      AppColors.signalBlue,
    ],
  });

  final Widget child;
  final double borderWidth;
  final double radius;
  final Color backgroundColor;
  final List<Color> colors;

  @override
  State<AnimatedGradientBorder> createState() => _AnimatedGradientBorderState();
}

class _AnimatedGradientBorderState extends State<AnimatedGradientBorder> with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(seconds: 3))..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return Container(
          padding: EdgeInsets.all(widget.borderWidth),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(widget.radius),
            gradient: SweepGradient(
              colors: widget.colors,
              transform: GradientRotation(_controller.value * 2 * math.pi),
            ),
          ),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(widget.radius - widget.borderWidth),
            child: Container(color: widget.backgroundColor, child: child),
          ),
        );
      },
      child: widget.child,
    );
  }
}
