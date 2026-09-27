import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Paleta "Clay/Glass Field Kit" — reemplaza la anterior "Split-Flap
/// Concourse Board" (mirror 1:1 del tablero oscuro de frontend-web). Cambio
/// deliberado y pedido explícitamente para esta app: superficies opacas tipo
/// clay (sombra dual, alto contraste) para todo contenido crítico, chrome
/// (AppBar/Drawer/FAB/sheets) en glass — ver widgets/clay_surface.dart y
/// widgets/glass_panel.dart. Fondo claro a propósito: uso de campo con sol
/// directo hace que el tablero oscuro original sea más difícil de leer que
/// una superficie clara de alto contraste.
class AppColors {
  AppColors._();

  static const canvasBg = Color(0xFFEEF2F6);
  static const claySurface = Color(0xFFF7F9FC);
  static const claySurfaceRaised = Color(0xFFFFFFFF);

  static const neutral = Color(0xFF94A3B8);
  static const neutralSoft = Color(0xFFE2E8F0);

  static const inkPrimary = Color(0xFF1A2026);
  static const inkSecondary = Color(0xFF475569);

  // Marca y señal — sin cambios respecto al tema anterior, para no perder
  // identidad de marca en el cambio de fondo claro/oscuro.
  static const signalBlue = Color(0xFF2760D1);
  static const signalBlueBright = Color(0xFF5B8DFF);
  static const signalBlueWash = Color(0x292F6FED); // rgba(47,111,237,.16)

  static const signalAmber = Color(0xFFD9A441);
  static const signalAmberWash = Color(0x24D9A441); // rgba(217,164,65,.14)

  static const signalRed = Color(0xFFD64545);
  static const signalRedWash = Color(0x24D64545); // rgba(214,69,69,.14)
}

/// Estilos de texto que no calzan en un TextTheme estándar.
class AppTextStyles {
  AppTextStyles._();

  /// Cifras de ancho fijo (lecturas de contador, contadores en vivo) — sin
  /// esto, los dígitos saltan de ancho en cada refresh y el texto "tiembla".
  static const tabularNumber = TextStyle(fontFeatures: [FontFeature.tabularFigures()]);
}

/// Tema único (claro, sin alternativa oscura) — ver nota en app_theme sobre
/// por qué esta app diverge del tablero oscuro del resto del proyecto.
class AppTheme {
  AppTheme._();

  static const _radius = BorderRadius.all(Radius.circular(16));

  static ThemeData get light {
    final base = ThemeData(useMaterial3: true, brightness: Brightness.light);
    final textTheme = GoogleFonts.interTextTheme(base.textTheme).apply(
      bodyColor: AppColors.inkPrimary,
      displayColor: AppColors.inkPrimary,
    );

    final fieldBorder = OutlineInputBorder(
      borderRadius: _radius,
      borderSide: const BorderSide(color: AppColors.neutralSoft),
    );

    return base.copyWith(
      scaffoldBackgroundColor: AppColors.canvasBg,
      textTheme: textTheme,
      colorScheme: base.colorScheme.copyWith(
        primary: AppColors.signalBlue,
        onPrimary: Colors.white,
        secondary: AppColors.signalBlueBright,
        onSecondary: Colors.white,
        surface: AppColors.claySurface,
        onSurface: AppColors.inkPrimary,
        error: AppColors.signalRed,
        onError: Colors.white,
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.claySurface,
        foregroundColor: AppColors.inkPrimary,
        elevation: 0,
        centerTitle: false,
      ),
      // Red de seguridad para cualquier `Card(` que no se haya migrado a
      // ClayCard todavía — no reemplaza el retrofit real (CardThemeData no
      // admite la sombra dual de ClaySurface), solo evita que se vea oscuro.
      cardTheme: CardThemeData(
        color: AppColors.claySurface,
        elevation: 0,
        margin: const EdgeInsets.symmetric(vertical: 6),
        shape: const RoundedRectangleBorder(
          borderRadius: _radius,
          side: BorderSide(color: AppColors.neutralSoft),
        ),
      ),
      dividerTheme: const DividerThemeData(color: AppColors.neutralSoft, thickness: 1),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.claySurface,
        border: fieldBorder,
        enabledBorder: fieldBorder,
        focusedBorder: fieldBorder.copyWith(
          borderSide: const BorderSide(color: AppColors.signalBlueBright, width: 2),
        ),
        labelStyle: const TextStyle(color: AppColors.inkSecondary),
        hintStyle: const TextStyle(color: AppColors.inkSecondary),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.signalBlue,
          foregroundColor: Colors.white,
          elevation: 2,
          shadowColor: AppColors.inkPrimary.withValues(alpha: 0.25),
          shape: const RoundedRectangleBorder(borderRadius: _radius),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 16),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.inkPrimary,
          side: const BorderSide(color: AppColors.neutral),
          shape: const RoundedRectangleBorder(borderRadius: _radius),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 16),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.inkSecondary,
          shape: const RoundedRectangleBorder(borderRadius: _radius),
        ),
      ),
      iconTheme: const IconThemeData(color: AppColors.inkSecondary),
      // Sin esto, el FAB usa el "secondaryContainer" morado por defecto de
      // Material 3 — no existe ningún morado en esta paleta.
      floatingActionButtonTheme: const FloatingActionButtonThemeData(
        backgroundColor: AppColors.signalBlue,
        foregroundColor: Colors.white,
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.inkPrimary,
        contentTextStyle: const TextStyle(color: Colors.white),
        actionTextColor: AppColors.signalBlueBright,
        shape: const RoundedRectangleBorder(borderRadius: _radius),
        behavior: SnackBarBehavior.floating,
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.signalBlueBright),
      dialogTheme: DialogThemeData(
        backgroundColor: AppColors.claySurface,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(22))),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
        ),
      ),
      dividerColor: AppColors.neutralSoft,
    );
  }
}
