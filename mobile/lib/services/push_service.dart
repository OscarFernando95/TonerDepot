import 'dart:convert';
import 'dart:io' show Platform;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import 'api_client.dart';

/// Id del canal de Android; el backend lo manda en cada notificación (FirebasePushTransport.AndroidChannelId).
const kPushChannelId = 'toner_assignments';

final _uuidPattern = RegExp(
  r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
);

/// A qué pantalla lleva tocar una notificación. El payload trae solo identificadores (type: ticket | order |
/// installation, id, event: assigned | removed | cancelled | unassigned | available); el detalle se pide a la API con
/// la sesión del usuario. Devuelve null si el payload no es reconocible (no se navega a nada).
String? pushTargetPath(Map<String, dynamic> data) {
  final type = data['type']?.toString();
  final id = data['id']?.toString();
  final event = data['event']?.toString();
  if (type != 'ticket' && type != 'order' && type != 'installation') return null;
  // Las instalaciones pendientes no tienen pantalla de detalle propia: viven en la lista de Mi trabajo.
  if (type == 'installation') return '/my-work';
  // Un servicio que ya no es mío (reasignado o cancelado): la pantalla útil es mi trabajo, no su detalle.
  if (event == 'removed' || event == 'cancelled') return '/my-work';
  if (id == null || !_uuidPattern.hasMatch(id)) return null;
  return type == 'ticket' ? '/tickets/$id' : '/maintenance-orders/$id';
}

/// Notificaciones push por FCM (solo Android por ahora; iOS necesita la clave APNs en Firebase). El token del
/// dispositivo se asocia al usuario en el backend y NO se borra al cerrar sesión: el aviso debe llegar igual.
class PushService {
  PushService._();
  static final PushService instance = PushService._();

  final _local = FlutterLocalNotificationsPlugin();
  Future<void>? _initFuture;
  bool _initialized = false;
  String? _lastRegistered;

  bool get supported => !kIsWeb && Platform.isAndroid;

  /// [onOpen] recibe la ruta a abrir cuando el usuario toca una notificación (con la app en segundo plano o
  /// cerrada). Quien lo recibe decide si ya hay sesión o si espera al login.
  Future<void> init({required void Function(String path) onOpen}) {
    if (!supported) return Future.value();
    // Una sola inicialización, y registerToken la espera: si la sesión se restaura antes de que Firebase termine
    // de arrancar, el token no debe quedarse sin registrar hasta el próximo login.
    return _initFuture ??= _init(onOpen);
  }

  Future<void> _init(void Function(String path) onOpen) async {
    try {
      await Firebase.initializeApp();

      // Tocar una notificación que la app misma mostró (llegó con la app abierta): el payload es el `data` del push.
      await _local.initialize(
        settings: const InitializationSettings(
          android: AndroidInitializationSettings('@mipmap/ic_launcher'),
        ),
        onDidReceiveNotificationResponse: (response) {
          final raw = response.payload;
          if (raw == null) return;
          try {
            final path = pushTargetPath(
              Map<String, dynamic>.from(jsonDecode(raw) as Map),
            );
            if (path != null) onOpen(path);
          } catch (e, st) {
            debugPrint('PushService: payload de notificación ilegible: $e\n$st');
          }
        },
      );
      await _local
          .resolvePlatformSpecificImplementation<
            AndroidFlutterLocalNotificationsPlugin
          >()
          ?.createNotificationChannel(
            const AndroidNotificationChannel(
              kPushChannelId,
              'Asignaciones',
              description: 'Servicios y órdenes asignados a ti',
              importance: Importance.high,
            ),
          );

      // Android no muestra por sí solo un push que llega con la app en primer plano: se muestra aquí, en el mismo canal.
      FirebaseMessaging.onMessage.listen(_showForeground);

      void open(RemoteMessage message) {
        final path = pushTargetPath(message.data);
        if (path != null) onOpen(path);
      }

      FirebaseMessaging.onMessageOpenedApp.listen(open);
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) open(initial);

      // El token rota de vez en cuando: se vuelve a registrar sin esperar a otro login.
      FirebaseMessaging.instance.onTokenRefresh.listen((token) {
        _lastRegistered = null;
        registerToken(token: token);
      });
      _initialized = true;
    } catch (e, st) {
      debugPrint('PushService.init failed: $e\n$st');
    }
  }

  /// Pide el permiso (Android 13+) y registra el token del dispositivo para el usuario con sesión. Es idempotente.
  Future<void> registerToken({String? token}) async {
    if (!supported) return;
    await _initFuture;
    if (!_initialized) return;
    try {
      final messaging = FirebaseMessaging.instance;
      await messaging.requestPermission();
      final current = token ?? await messaging.getToken();
      if (current == null || current == _lastRegistered) return;

      await ApiClient.instance.dio.put(
        '/devices/push-token',
        data: {'platform': 'Android', 'token': current},
      );
      _lastRegistered = current;
    } catch (e, st) {
      // Sin push la app sigue funcionando (el tiempo real cubre la app abierta): solo se registra el error.
      debugPrint('PushService.registerToken failed: $e\n$st');
    }
  }

  Future<void> _showForeground(RemoteMessage message) async {
    final notification = message.notification;
    if (notification == null) return;
    try {
      await _local.show(
        id: message.hashCode,
        title: notification.title,
        body: notification.body,
        notificationDetails: NotificationDetails(
          android: AndroidNotificationDetails(
            kPushChannelId,
            'Asignaciones',
            channelDescription: 'Servicios y órdenes asignados a ti',
            importance: Importance.high,
            priority: Priority.high,
            // El cuerpo trae varias líneas (cliente y ciudad, criticidad, equipo): que se pueda desplegar completo.
            styleInformation: BigTextStyleInformation(notification.body ?? ''),
            // Igual que el push del servidor: con "ocultar contenido sensible" no se ve en la pantalla de bloqueo.
            visibility: NotificationVisibility.private,
          ),
        ),
        payload: jsonEncode(message.data),
      );
    } catch (e, st) {
      debugPrint('PushService: no se pudo mostrar la notificación: $e\n$st');
    }
  }
}
