import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Guarda localmente, en el almacenamiento del dispositivo, las cédulas con
/// las que se ha iniciado sesión — para que el técnico solo tenga que
/// tocarla en vez de volver a escribirla. Nunca se guarda la contraseña,
/// solo el número de cédula (dato que ya es visible en la pantalla de login
/// de cualquier forma, no es secreto). Usa el mismo storage seguro que ya
/// usa la app para el JWT solo porque ya está disponible como dependencia
/// — no hace falta que la cédula esté cifrada para este propósito.
class CedulaHistoryStore {
  CedulaHistoryStore._();
  static final CedulaHistoryStore instance = CedulaHistoryStore._();

  static const _key = 'toner_cedula_history';
  static const _maxEntries = 6;
  final _storage = const FlutterSecureStorage();

  Future<List<String>> load() async {
    final raw = await _storage.read(key: _key);
    if (raw == null || raw.isEmpty) return [];
    try {
      return (jsonDecode(raw) as List).cast<String>();
    } catch (_) {
      return [];
    }
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
    await _storage.write(key: _key, value: jsonEncode(capped));
    return capped;
  }
}
