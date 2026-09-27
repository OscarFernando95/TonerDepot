import 'package:flutter/foundation.dart';

import '../models/current_user.dart';
import '../services/api_client.dart';
import '../services/auth_api.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Maneja sesión: login, logout, y quién es el usuario actual. Vive por
/// encima de MyWorkState (ver main.dart) porque check-in/checkout necesitan
/// saber si hay sesión antes de intentar cargar nada.
class AuthState extends ChangeNotifier {
  AuthState(this._client) : _authApi = AuthApi(_client) {
    _client.onUnauthorized = _handleUnauthorized;
  }

  final ApiClient _client;
  final AuthApi _authApi;

  AuthStatus status = AuthStatus.unknown;
  CurrentUser? currentUser;
  String? lastError;

  Future<void> bootstrap() async {
    await _client.init();
    if (!_client.hasToken) {
      status = AuthStatus.unauthenticated;
      notifyListeners();
      return;
    }
    try {
      currentUser = await _authApi.me();
      status = AuthStatus.authenticated;
    } catch (e, st) {
      debugPrint('AuthState.bootstrap failed: $e\n$st');
      await _client.setToken(null);
      status = AuthStatus.unauthenticated;
    }
    notifyListeners();
  }

  Future<bool> login(String cedula, String password) async {
    lastError = null;
    try {
      final result = await _authApi.login(cedula, password);
      if (!result.succeeded || result.token == null || result.user == null) {
        lastError = 'Cédula o contraseña incorrectas.';
        notifyListeners();
        return false;
      }
      await _client.setToken(result.token);
      currentUser = result.user;
      status = AuthStatus.authenticated;
      notifyListeners();
      return true;
    } catch (e, st) {
      debugPrint('AuthState.login failed: $e\n$st');
      lastError = e is ApiException ? e.message : 'No se pudo iniciar sesión.';
      notifyListeners();
      return false;
    }
  }

  /// Equivalente a hasRole(...roles) en frontend-web/src/stores/auth.ts —
  /// el router (app_router.dart) y AppShell la usan para decidir qué rutas y
  /// destinos de navegación mostrar por rol.
  bool hasRole(String role) => currentUser?.role == role;

  bool hasAnyRole(List<String> roles) => currentUser != null && roles.contains(currentUser!.role);

  Future<void> logout() async {
    await _client.setToken(null);
    currentUser = null;
    status = AuthStatus.unauthenticated;
    notifyListeners();
  }

  /// Espejo de setMustChangePassword(false) en el store de auth del frontend
  /// web tras un cambio de contraseña exitoso: este notifyListeners() es lo
  /// que dispara el `redirect` de app_router.dart (vía refreshListenable) y
  /// hace que deje de mostrar ChangePasswordScreen y pase a AppShell.
  Future<String?> changePassword(String currentPassword, String newPassword) async {
    try {
      await _authApi.changePassword(currentPassword, newPassword);
      if (currentUser != null) {
        currentUser = currentUser!.copyWith(mustChangePassword: false);
      }
      notifyListeners();
      return null;
    } catch (e, st) {
      debugPrint('AuthState.changePassword failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo cambiar la contraseña.';
    }
  }

  void _handleUnauthorized() {
    if (status == AuthStatus.authenticated) {
      currentUser = null;
      status = AuthStatus.unauthenticated;
      lastError = 'Tu sesión expiró. Ingresa de nuevo.';
      notifyListeners();
    }
  }
}
