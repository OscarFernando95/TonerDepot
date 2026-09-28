import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import 'api_client.dart';

class EntityChangedEvent {
  const EntityChangedEvent({required this.entity, required this.id, required this.action});

  final String entity;
  final String id;
  final String action; // created | updated | deleted

  factory EntityChangedEvent.fromJson(Map<String, dynamic> json) => EntityChangedEvent(
        entity: json['entity'] as String,
        id: json['id'] as String,
        action: json['action'] as String,
      );
}

/// Un solo canal por sesión de la app, reutilizado por todas las pantallas que se suscriben — mismo patrón que
/// composables/useRealtime.ts en la web. Si el servidor revoca la sesión (logout en otro dispositivo, cierre por
/// administrador), `onSessionRevoked` dispara el mismo camino que un 401 de la API.
class RealtimeService {
  RealtimeService._();
  static final RealtimeService instance = RealtimeService._();

  HubConnection? _connection;
  VoidCallback? onSessionRevoked;
  final _handlers = <void Function(EntityChangedEvent)>[];

  Future<void> connect() async {
    if (_connection != null) return;

    final token = ApiClient.instance.currentToken;
    if (token == null) return;

    // baseUrl termina en "/api" (ver AppConfig) — el hub vive en la raíz del sitio, no bajo /api.
    final apiBaseUrl = await ApiClient.instance.baseUrl;
    final siteBaseUrl = apiBaseUrl.endsWith('/api') ? apiBaseUrl.substring(0, apiBaseUrl.length - 4) : apiBaseUrl;
    final connection = HubConnectionBuilder()
        .withUrl(
          '$siteBaseUrl/hubs/updates',
          options: HttpConnectionOptions(accessTokenFactory: () async => token, logMessageContent: false),
        )
        .withAutomaticReconnect()
        .build();

    connection.on('entityChanged', (arguments) {
      final data = arguments?.first as Map<String, dynamic>?;
      if (data == null) return;
      final event = EntityChangedEvent.fromJson(data);
      for (final handler in _handlers) {
        handler(event);
      }
    });

    connection.on('sessionRevoked', (_) => onSessionRevoked?.call());

    try {
      await connection.start();
      _connection = connection;
    } catch (e, st) {
      debugPrint('RealtimeService.connect failed: $e\n$st');
    }
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
    _handlers.clear();
  }

  /// Se suscribe a "algo cambió" para las entidades indicadas. Devuelve una función para cancelar la
  /// suscripción — quien la use debe llamarla en dispose()/deactivate(), igual que cualquier listener.
  VoidCallback subscribe(List<String> entities, void Function(EntityChangedEvent) handler) {
    void wrapped(EntityChangedEvent event) {
      if (entities.contains(event.entity)) handler(event);
    }

    _handlers.add(wrapped);
    if (_connection == null) connect();
    return () => _handlers.remove(wrapped);
  }
}
