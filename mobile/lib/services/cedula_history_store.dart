import 'package:shared_preferences/shared_preferences.dart';

/// Guarda localmente, en el almacenamiento del dispositivo, las cédulas con
/// las que se ha iniciado sesión — para que el técnico solo tenga que
/// tocarla en vez de volver a escribirla. Nunca se guarda la contraseña,
/// solo el número de cédula (dato que ya es visible en la pantalla de login
/// de cualquier forma, no es secreto) — por eso usa SharedPreferences y no
/// el storage cifrado que usa la app para el JWT.
class CedulaHistoryStore {
  CedulaHistoryStore._();
  static final CedulaHistoryStore instance = CedulaHistoryStore._();

  static const _key = 'toner_cedula_history';
  static const _maxEntries = 6;

  Future<List<String>> load() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getStringList(_key) ?? [];
  }

  /// Mueve la cédula al frente de la lista (más reciente primero), sin
  /// duplicados, y recorta a _maxEntries.
  Future<List<String>> remember(String cedula) async {
    final trimmed = cedula.trim();
    if (trimmed.isEmpty) return load();
    final current = await load();
    current.removeWhere((c) => c == trimmed);
    current.insert(0, trimmed);
    final capped = current.take(_maxEntries).toList();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setStringList(_key, capped);
    return capped;
  }
}
