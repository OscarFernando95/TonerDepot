import 'dart:ui';

import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Superficie "glass": blur + translúcido — solo para chrome no crítico
/// (AppBar, Drawer, FAB, sheets). Nunca para texto crítico sin un fondo
/// sólido detrás (ver ClaySurface para eso). No puede expresarse solo con
/// ThemeData porque `BackdropFilter` no es declarativo — por eso este widget
/// se usa explícito en cada Scaffold que lo necesita.
class GlassPanel extends StatelessWidget {
  const GlassPanel({
    super.key,
    required this.child,
    this.blurSigma = 18,
    this.borderRadius = BorderRadius.zero,
    this.tintOpacity = 0.6,
    this.tintColor,
  });

  final Widget child;
  final double blurSigma;
  final BorderRadius borderRadius;
  final double tintOpacity;
  final Color? tintColor;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: borderRadius,
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: blurSigma, sigmaY: blurSigma),
        child: Container(
          decoration: BoxDecoration(
            color: (tintColor ?? AppColors.claySurface).withValues(alpha: tintOpacity),
            borderRadius: borderRadius,
            border: Border.all(color: Colors.white.withValues(alpha: 0.5)),
          ),
          child: child,
        ),
      ),
    );
  }
}

/// AppBar en vidrio — reemplazo directo de `AppBar(...)` para el Scaffold
/// principal (AppShell). Requiere `Scaffold(extendBodyBehindAppBar: true)`
/// para que el blur tenga contenido real detrás; si no, solo se ve el tinte.
class GlassAppBar extends StatelessWidget implements PreferredSizeWidget {
  const GlassAppBar({super.key, required this.title, this.actions, this.leading});

  final String title;
  final List<Widget>? actions;
  final Widget? leading;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    return GlassPanel(
      child: AppBar(
        title: Text(title),
        actions: actions,
        leading: leading,
        backgroundColor: Colors.transparent,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        foregroundColor: AppColors.inkPrimary,
      ),
    );
  }
}

/// Botón flotante en vidrio — para acciones secundarias tipo "+ Crear" sobre
/// listas (Fases E/F). No usado todavía en Fase A/B, pero es chrome, no
/// contenido, así que sigue la regla glass-para-chrome desde ya.
class GlassFab extends StatelessWidget {
  const GlassFab({super.key, required this.onPressed, required this.icon, this.label});

  final VoidCallback onPressed;
  final Widget icon;
  final Widget? label;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(20);
    return GlassPanel(
      borderRadius: radius,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: radius,
          onTap: onPressed,
          child: Padding(
            padding: label == null ? const EdgeInsets.all(16) : const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
            child: label == null
                ? icon
                : Row(mainAxisSize: MainAxisSize.min, children: [icon, const SizedBox(width: 8), label!]),
          ),
        ),
      ),
    );
  }
}
