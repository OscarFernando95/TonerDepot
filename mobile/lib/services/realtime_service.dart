import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:signalr_netcore/iretry_policy.dart';
import 'package:signalr_netcore/signalr_client.dart';

import 'api_client.dart';

class EntityChangedEvent {
  const EntityChangedEvent({
    required this.entity,
    required this.id,
    required this.action,
  });

  final String entity;
  final String id;

  /// created | updated | deleted | resync. `resync` es sintético (no viene del servidor): el canal se cortó o la
  /// app estuvo en segundo plano y se pudieron perder avisos; quien lo reciba debe recargar todo.
  final String action;

  /// Evento sintético de "recarga todo".
  static const resync = EntityChangedEvent(
    entity: '*',
    id: '',
    action: 'resync',
  );

  bool get isResync => action == 'resync';

  /// Para handlers de detalle: ¿este evento toca el registro [recordId]? (o es un resync, que toca todo).
  bool affects(String recordId) =>
      isResync || id.toLowerCase() == recordId.toLowerCase();

  factory EntityChangedEvent.fromJson(Map<String, dynamic> json) =>
      EntityChangedEvent(
        entity: json['entity'] as String,
        id: json['id'] as String,
        action: json['action'] as String,
      );
}

typedef EntityChangedHandler = void Function(EntityChangedEvent event);

class _Subscription {
  _Subscription(this.entities, this.handler);
  final List<String> entities;
  final EntityChangedHandler handler;
  final Map<String, Timer> pending = {};
}

/// Despacho de eventos a suscriptores con coalescencia (debounce trailing por suscriptor y `entity:id`). Separado
/// del transporte SignalR para poder probarse sin servidor.
class RealtimeDispatcher {
  RealtimeDispatcher({this.debounce = const Duration(milliseconds: 250)});

  final Duration debounce;
  final _subs = <_Subscription>{};

  int get subscriberCount => _subs.length;

  VoidCallback subscribe(List<String> entities, EntityChangedHandler handler) {
    final sub = _Subscription(entities, handler);
    _subs.add(sub);
    return () {
      _subs.remove(sub);
      for (final t in sub.pending.values) {
        t.cancel();
      }
      sub.pending.clear();
    };
  }

  void dispatch(EntityChangedEvent event) {
    final key = '${event.entity}:${event.id}:${event.isResync ? 'resync' : ''}';
    for (final sub in List.of(_subs)) {
      if (!event.isResync && !sub.entities.contains(event.entity)) continue;
      sub.pending[key]?.cancel();
      sub.pending[key] = Timer(debounce, () {
        sub.pending.remove(key);
        if (!_subs.contains(sub)) return;
        try {
          sub.handler(event);
        } catch (e, st) {
          debugPrint('Error en un handler de tiempo real: $e\n$st');
        }
      });
    }
  }
}

/// Reintenta sin límite: 0, 2, 5, 10 y 30 s, y luego 30 s para siempre (el default de signalr_netcore se rinde
/// tras ~40 s y no vuelve).
class _UnboundedRetryPolicy implements IRetryPolicy {
  static const delaysMs = [0, 2000, 5000, 10000, 30000];
  @override
  int? nextRetryDelayInMilliseconds(RetryContext retryContext) =>
      delaysMs[retryContext.previousRetryCount.clamp(0, delaysMs.length - 1)];
}

/// Un solo canal por sesión de la app, reutilizado por todas las pantallas que se suscriben — mismo patrón que
/// composables/useRealtime.ts en la web. Si el servidor revoca la sesión (logout en otro dispositivo, cierre por
/// administrador), `onSessionRevoked` dispara el mismo camino que un 401 de la API.
class RealtimeService with WidgetsBindingObserver {
  RealtimeService._() {
    WidgetsFlutterBinding.ensureInitialized().addObserver(this);
  }
  static final RealtimeService instance = RealtimeService._();

  static const _resyncAfterBackground = Duration(seconds: 15);

  final RealtimeDispatcher _dispatcher = RealtimeDispatcher();
  HubConnection? _connection;
  Future<void>? _starting;
  VoidCallback? onSessionRevoked;

  /// true entre connect() y disconnect(): si la conexión se cae del todo, se reinicia sola.
  bool _wanted = false;
  bool _everConnected = false;
  Timer? _restartTimer;
  DateTime? _pausedAt;

  Future<void> connect() {
    _wanted = true;
    return _starting ??= _doConnect().whenComplete(() => _starting = null);
  }

  Future<void> _doConnect() async {
    if (_connection != null) return;
    if (ApiClient.instance.currentToken == null) return;

    // baseUrl termina en "/api" (ver AppConfig) — el hub vive en la raíz del sitio, no bajo /api.
    final apiBaseUrl = await ApiClient.instance.baseUrl;
    final siteBaseUrl = apiBaseUrl.endsWith('/api')
        ? apiBaseUrl.substring(0, apiBaseUrl.length - 4)
        : apiBaseUrl;
    final connection = HubConnectionBuilder()
        .withUrl(
          '$siteBaseUrl/hubs/updates',
          options: HttpConnectionOptions(
            // Se lee en cada llamada: el token cambia al renovar sesión / volver a iniciar sesión.
            accessTokenFactory: () async =>
                ApiClient.instance.currentToken ?? '',
            logMessageContent: false,
          ),
        )
        .withAutomaticReconnect(reconnectPolicy: _UnboundedRetryPolicy())
        .build();

    connection.on('entityChanged', (arguments) {
      final data = arguments?.first as Map<String, dynamic>?;
      if (data == null) return;
      _dispatcher.dispatch(EntityChangedEvent.fromJson(data));
    });
    connection.on('sessionRevoked', (_) => onSessionRevoked?.call());
    connection.onreconnected(({connectionId}) => resyncAll());
    connection.onclose(({error}) {
      if (!identical(_connection, connection)) return;
      _connection = null;
      _scheduleRestart();
    });

    try {
      await connection.start();
      if (!_wanted) {
        // disconnect() llegó mientras arrancaba.
        await connection.stop();
        return;
      }
      _connection = connection;
      if (_everConnected) resyncAll();
      _everConnected = true;
    } catch (e, st) {
      debugPrint('RealtimeService.connect failed: $e\n$st');
      _scheduleRestart();
    }
  }

  void _scheduleRestart() {
    if (!_wanted || _restartTimer != null) return;
    _restartTimer = Timer(const Duration(seconds: 10), () {
      _restartTimer = null;
      if (_wanted && _connection == null) connect();
    });
  }

  Future<void> disconnect() async {
    _wanted = false;
    _everConnected = false;
    _restartTimer?.cancel();
    _restartTimer = null;
    final connection = _connection;
    _connection = null;
    // Los handlers NO se limpian: cada suscriptor se da de baja con su propio callback.
    await connection?.stop();
  }

  /// Avisa a TODOS los suscriptores que recarguen (se pudieron perder eventos).
  void resyncAll() => _dispatcher.dispatch(EntityChangedEvent.resync);

  /// Se suscribe a "algo cambió" para las entidades indicadas (los `resync` llegan siempre). Devuelve una función
  /// para cancelar la suscripción — quien la use debe llamarla en dispose().
  VoidCallback subscribe(List<String> entities, EntityChangedHandler handler) {
    final unsubscribe = _dispatcher.subscribe(entities, handler);
    if (_connection == null) connect();
    return unsubscribe;
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.paused ||
        state == AppLifecycleState.hidden) {
      _pausedAt ??= DateTime.now();
    } else if (state == AppLifecycleState.resumed) {
      final since = _pausedAt;
      _pausedAt = null;
      if (_wanted && _connection == null) connect();
      if (since != null &&
          DateTime.now().difference(since) > _resyncAfterBackground) {
        resyncAll();
      }
    }
  }
}
