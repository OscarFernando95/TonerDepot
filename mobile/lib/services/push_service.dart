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

/// A qué pantalla lleva tocar una notificación. El payload trae solo identificadores (type: ticket | order,
/// id, event: assigned | removed | cancelled | unassigned); el detalle se pide a la API con la sesión del usuario.
/// Devuelve null si el payload no es reconocible (no se navega a nada).
String? pushTargetPath(Map<String, dynamic> data) {
  final type = data['type']?.toString();
  final id = data['id']?.toString();
  final event = data['event']?.toString();
  if (type != 'ticket' && type != 'order') return null;
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

  bool _initialized = false;
  String? _lastRegistered;

  bool get supported => !kIsWeb && Platform.isAndroid;

  /// [onOpen] recibe la ruta a abrir cuando el usuario toca una notificación (con la app en segundo plano o
  /// cerrada). Quien lo recibe decide si ya hay sesión o si espera al login.
  Future<void> init({required void Function(String path) onOpen}) async {
    if (!supported || _initialized) return;
    try {
      await Firebase.initializeApp();
      _initialized = true;

      final local = FlutterLocalNotificationsPlugin();
      await local.initialize(
        settings: const InitializationSettings(
          android: AndroidInitializationSettings('@mipmap/ic_launcher'),
        ),
      );
      await local
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
    } catch (e, st) {
      debugPrint('PushService.init failed: $e\n$st');
    }
  }

  /// Pide el permiso (Android 13+) y registra el token del dispositivo para el usuario con sesión. Es idempotente.
  Future<void> registerToken({String? token}) async {
    if (!supported || !_initialized) return;
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
}
