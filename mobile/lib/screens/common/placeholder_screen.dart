import 'package:flutter/material.dart';

import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';

/// Contenido genérico para una ruta ya registrada (aparece en el Drawer,
/// tiene guard de rol) pero sin pantalla real todavía — evita que un rol
/// nuevo (Cliente, Coordinador, Administrador, Ventas) vea la app rota
/// mientras se van portando sus funciones en las fases siguientes. Usa el
/// mismo lenguaje clay que el resto de la app (insignia + tarjeta) en vez de
/// un texto suelto sobre fondo vacío, para que no se sienta como un error.
class PlaceholderScreen extends StatelessWidget {
  const PlaceholderScreen({
    super.key,
    required this.title,
    this.message = 'Esta función todavía no está disponible en la app móvil.',
  });

  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 360),
          child: ClaySurface(
            radius: 24,
            padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 64,
                  height: 64,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: AppColors.signalAmberWash,
                  ),
                  child: const Icon(
                    Icons.hourglass_top_outlined,
                    size: 30,
                    color: AppColors.signalAmber,
                  ),
                ),
                const SizedBox(height: 20),
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w700),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 8),
                Text(
                  message,
                  style: const TextStyle(color: AppColors.inkSecondary),
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
