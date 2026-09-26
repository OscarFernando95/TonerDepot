import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Paleta "Split-Flap Concourse Board" — copiada 1:1 de
/// frontend-web/DESIGN.md y board-theme.css, para que la app del técnico
/// se vea como una extensión del mismo tablero, no como una app aparte.
/// Ver DESIGN.md en la raíz del repo para la razón de ser de cada color.
class AppColors {
  AppColors._();

  static const boardBg = Color(0xFF101317);
  static const boardPanel = Color(0xFF1A2026);
  static const boardPanelRaised = Color(0xFF21282F);
  static const boardSeam = Color(0xFF5B6672);
  static const boardSeamSoft = Color(0xFF262E35);
  static const flapInk = Color(0xFFEEF0EC);
  static const flapInkDim = Color(0xFF99A1AB);

  static const signalBlue = Color(0xFF2760D1);
  static const signalBlueBright = Color(0xFF5B8DFF);
  static const signalBlueWash = Color(0x292F6FED); // rgba(47,111,237,.16)

  static const signalAmber = Color(0xFFD9A441);
  static const signalAmberWash = Color(0x24D9A441); // rgba(217,164,65,.14)

  static const signalRed = Color(0xFFD64545);
  static const signalRedWash = Color(0x24D64545); // rgba(214,69,69,.14)
}

/// Tema oscuro fijo (no hay modo claro — igual que la web, el tablero es
/// oscuro a propósito, no un "dark mode" alternable). Cero radio de esquina
/// en todo, siguiendo la regla del sistema ("Zero corner radius anywhere").
class AppTheme {
  AppTheme._();

  static const _zeroRadius = BorderRadius.zero;

  static ThemeData get dark {
    final base = ThemeData(useMaterial3: true, brightness: Brightness.dark);
    final textTheme = GoogleFonts.interTextTheme(base.textTheme).apply(
      bodyColor: AppColors.flapInk,
      displayColor: AppColors.flapInk,
    );

    final seamBorder = OutlineInputBorder(
      borderRadius: _zeroRadius,
      borderSide: const BorderSide(color: AppColors.boardSeam),
    );

    return base.copyWith(
      scaffoldBackgroundColor: AppColors.boardBg,
      textTheme: textTheme,
      colorScheme: base.colorScheme.copyWith(
        primary: AppColors.signalBlue,
        onPrimary: Colors.white,
        secondary: AppColors.signalBlueBright,
        onSecondary: Colors.white,
        surface: AppColors.boardPanel,
        onSurface: AppColors.flapInk,
        error: AppColors.signalRed,
        onError: Colors.white,
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.boardPanel,
        foregroundColor: AppColors.flapInk,
        elevation: 0,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: AppColors.boardPanel,
        elevation: 0,
        margin: const EdgeInsets.symmetric(vertical: 6),
        shape: RoundedRectangleBorder(
          borderRadius: _zeroRadius,
          side: const BorderSide(color: AppColors.boardSeamSoft),
        ),
      ),
      dividerTheme: const DividerThemeData(color: AppColors.boardSeamSoft, thickness: 1),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.boardPanel,
        border: seamBorder,
        enabledBorder: seamBorder,
        focusedBorder: seamBorder.copyWith(
          borderSide: const BorderSide(color: AppColors.signalBlueBright, width: 2),
        ),
        labelStyle: const TextStyle(color: AppColors.flapInkDim),
        hintStyle: const TextStyle(color: AppColors.flapInkDim),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.signalBlue,
          foregroundColor: Colors.white,
          shape: const RoundedRectangleBorder(borderRadius: _zeroRadius),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 16),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.flapInkDim,
          shape: const RoundedRectangleBorder(borderRadius: _zeroRadius),
        ),
      ),
      iconTheme: const IconThemeData(color: AppColors.flapInkDim),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.boardPanelRaised,
        contentTextStyle: const TextStyle(color: AppColors.flapInk),
        shape: const RoundedRectangleBorder(borderRadius: _zeroRadius),
        behavior: SnackBarBehavior.floating,
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.signalBlueBright),
      dialogTheme: DialogThemeData(
        backgroundColor: AppColors.boardPanel,
        shape: const RoundedRectangleBorder(borderRadius: _zeroRadius),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: AppColors.boardPanel,
        shape: RoundedRectangleBorder(borderRadius: _zeroRadius),
      ),
      dividerColor: AppColors.boardSeamSoft,
    );
  }
}
