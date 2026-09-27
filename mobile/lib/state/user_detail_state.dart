import 'package:flutter/foundation.dart';

import '../models/managed_user.dart';
import '../services/api_client.dart';
import '../services/user_api.dart';

/// No hay GET /users/{id} en el backend — la lista ya trae el shape
/// completo, así que este estado arranca con el ManagedUser que ya se tenía
/// (pasado por `extra` de go_router) en vez de volver a pedirlo.
class UserDetailState extends ChangeNotifier {
  UserDetailState(ApiClient client, ManagedUser initialUser)
      : _api = UserApi(client),
        user = initialUser;

  final UserApi _api;

  bool busyWithAction = false;
  ManagedUser user;

  Future<String?> updateUser({
    required String cedula,
    String? email,
    required String fullName,
    required String phone,
    required String address,
    required String cityId,
  }) =>
      _runAction(() async {
        user = await _api.update(user.id, cedula: cedula, email: email, fullName: fullName, phone: phone, address: address, cityId: cityId);
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
      return (password: null, error: e is ApiException ? e.message : 'No se pudo restablecer la contraseña.');
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
