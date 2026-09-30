import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Campo de fecha/hora de solo-lectura que abre `showDatePicker`/
/// `showTimePicker` al tocarlo — reemplazo de un `ListTile` con
/// `contentPadding: EdgeInsets.zero` + `shape: RoundedRectangleBorder(...)`,
/// patrón repetido en varias pantallas que Flutter señala en debug con
/// "ListTile background color or ink splashes may be invisible": un
/// `ListTile` con `shape` propio necesita su propio `Material` ancestro
/// para recortar el splash a esa forma — sin él, el ink se pinta (o no) por
/// fuera del borde redondeado. Acá el `InkWell` vive en su propio
/// `Material(color: transparent)`, así que el problema no puede ocurrir.
class ClayDateField extends StatelessWidget {
  const ClayDateField({
    super.key,
    required this.label,
    required this.value,
    required this.onTap,
    this.trailing,
  });

  final String label;
  final String value;
  final VoidCallback onTap;

  /// Por defecto el ícono de calendario; algunas pantallas lo cambian por
  /// un botón de "limpiar" cuando el campo ya tiene valor (ej. fecha de fin
  /// opcional de un contrato).
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          decoration: BoxDecoration(
            border: Border.all(color: AppColors.neutralSoft),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      label,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.inkSecondary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      value,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.inkPrimary,
                      ),
                    ),
                  ],
                ),
              ),
              trailing ??
                  const Icon(
                    Icons.calendar_today_outlined,
                    size: 18,
                    color: AppColors.inkSecondary,
                  ),
            ],
          ),
        ),
      ),
    );
  }
}
