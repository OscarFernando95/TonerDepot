import 'package:flutter/foundation.dart';

import '../models/managed_user.dart';
import '../services/client_location_api.dart';
import '../services/api_client.dart';
import '../services/user_api.dart';

/// No hay GET /users/{id} en el backend — la lista ya trae el shape
/// completo, así que este estado arranca con el ManagedUser que ya se tenía
/// (pasado por `extra` de go_router) en vez de volver a pedirlo.
class UserDetailState extends ChangeNotifier {
  UserDetailState(ApiClient client, ManagedUser initialUser)
    : _api = UserApi(client),
      _locationApi = ClientLocationApi(client),
      user = initialUser;

  final UserApi _api;
  final ClientLocationApi _locationApi;

  /// Nombres de las sedes de la empresa del usuario Cliente (id → nombre),
  /// para mostrar a qué sedes accede. Vacío hasta que cargue (o si falla: la
  /// etiqueta cae a "N sedes" sin nombres).
  Map<String, String> locationNames = {};

  Future<void> loadLocationNames() async {
    final clientId = user.clientId;
    if (clientId == null) return;
    try {
      final locations = await _locationApi.listForClient(clientId);
      locationNames = {for (final l in locations) l.id: l.name};
      notifyListeners();
    } catch (e, st) {
      debugPrint('UserDetailState.loadLocationNames failed: $e\n$st');
    }
  }

  /// null = todas las sedes de la empresa. Devuelve el mensaje de error, o
  /// null si salió bien.
  Future<String?> setClientLocations(List<String>? allowedLocationIds) =>
      _runAction(() async {
        user = await _api.setClientLocations(user.id, allowedLocationIds);
      });

  bool busyWithAction = false;
  ManagedUser user;

  Future<String?> updateUser({
    required String cedula,
    String? email,
    required String fullName,
    required String phone,
    required String address,
    required String cityId,
  }) => _runAction(() async {
    user = await _api.update(
      user.id,
      cedula: cedula,
      email: email,
      fullName: fullName,
      phone: phone,
      address: address,
      cityId: cityId,
    );
  });

  Future<String?> setStatus(bool isActive) => _runAction(() async {
    user = await _api.setStatus(user.id, isActive);
  });

  /// Devuelve la contraseña generada para mostrarla una vez, o null si falló
  /// (en cuyo caso el mensaje de error ya quedó en el resultado normal).
  Future<({String? password, String? error})> resetPassword() async {
    busyWithAction = true;
    notifyListeners();
    try {
      final result = await _api.resetPassword(user.id);
      user = result.user;
      return (password: result.generatedPassword, error: null);
    } catch (e, st) {
      debugPrint('UserDetailState.resetPassword failed: $e\n$st');
      return (
        password: null,
        error: e is ApiException
            ? e.message
            : 'No se pudo restablecer la contraseña.',
      );
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  /// Devuelve cuántas sesiones cerró, o el mensaje de error.
  Future<({int? revoked, String? error})> revokeSessions() async {
    busyWithAction = true;
    notifyListeners();
    try {
      return (revoked: await _api.revokeSessions(user.id), error: null);
    } catch (e, st) {
      debugPrint('UserDetailState.revokeSessions failed: $e\n$st');
      return (
        revoked: null,
        error: e is ApiException
            ? e.message
            : 'No se pudieron cerrar las sesiones.',
      );
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('UserDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
