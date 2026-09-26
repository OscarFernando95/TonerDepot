import 'package:flutter/foundation.dart';

/// Configuración del servidor. En dev, el emulador de Android no puede llamar a
/// "localhost" (eso apuntaría al propio emulador) — hay que usar 10.0.2.2, que
/// el emulador traduce al localhost de la máquina host. En un dispositivo físico
/// conectado a la misma red, hay que usar la IP LAN de tu PC (ej. 192.168.1.50).
/// Por eso el usuario puede cambiar esta URL desde la pantalla de login
/// (ver ServerSettingsScreen) sin recompilar la app.
class AppConfig {
  AppConfig._();

  static const String defaultAndroidEmulatorUrl = 'http://10.0.2.2:5250/api';
  static const String defaultIosSimulatorUrl = 'http://localhost:5250/api';

  static String get defaultBaseUrl {
    if (defaultTargetPlatform == TargetPlatform.android) {
      return defaultAndroidEmulatorUrl;
    }
    return defaultIosSimulatorUrl;
  }
}
