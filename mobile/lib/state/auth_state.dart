import 'package:flutter/foundation.dart';

import '../models/current_user.dart';
import '../services/api_client.dart';
import '../services/auth_api.dart';
import '../services/biometric_login_service.dart';
import '../services/realtime_service.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Maneja sesión: login, logout, y quién es el usuario actual. Vive por
/// encima de MyWorkState (ver main.dart) porque check-in/checkout necesitan
/// saber si hay sesión antes de intentar cargar nada.
class AuthState extends ChangeNotifier {
  AuthState(this._client, {BiometricLoginService? biometrics})
    : _authApi = AuthApi(_client),
      _biometrics = biometrics ?? BiometricLoginService.instance {
    _client.onUnauthorized = _handleUnauthorized;
    RealtimeService.instance.onSessionRevoked = _handleUnauthorized;
  }

  final ApiClient _client;
  final AuthApi _authApi;
  final BiometricLoginService _biometrics;

  AuthStatus status = AuthStatus.unknown;
  CurrentUser? currentUser;
  String? lastError;
  // Aviso informativo para la pantalla de login (p. ej. tras cambiar la contraseña); la pantalla lo muestra y lo limpia.
  String? loginNotice;
  // El usuario cerró sesión a propósito: el login no debe pedir la biometría solo (sí al abrir la app o si venció).
  bool signedOutByUser = false;
  // El último intento fue rechazado por credenciales (no por red ni límite de intentos).
  bool _lastLoginRejectedCredentials = false;
  // Credenciales del login manual que acaba de pasar, en memoria hasta que AppShell ofrezca activar la biometría.
  StoredCredentials? _biometricOffer;

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
      RealtimeService.instance.connect();
    } catch (e, st) {
      debugPrint('AuthState.bootstrap failed: $e\n$st');
      await _client.setToken(null);
      status = AuthStatus.unauthenticated;
    }
    notifyListeners();
  }

  Future<bool> login(
    String cedula,
    String password, {
    bool offerBiometrics = true,
  }) async {
    lastError = null;
    _lastLoginRejectedCredentials = false;
    try {
      final result = await _authApi.login(cedula, password);
      if (!result.succeeded || result.token == null || result.user == null) {
        _lastLoginRejectedCredentials = true;
        lastError = 'Cédula o contraseña incorrectas.';
        notifyListeners();
        return false;
      }
      await _client.setToken(result.token);
      currentUser = result.user;
      status = AuthStatus.authenticated;
      signedOutByUser = false;
      // Con la contraseña genérica no se ofrece: se cambia enseguida y la guardada quedaría vencida.
      _biometricOffer = offerBiometrics && !result.user!.mustChangePassword
          ? await _prepareBiometricOffer(cedula, password)
          : null;
      RealtimeService.instance.connect();
      notifyListeners();
      return true;
    } catch (e, st) {
      debugPrint('AuthState.login failed: $e\n$st');
      _lastLoginRejectedCredentials =
          e is ApiException && (e.statusCode == 400 || e.statusCode == 401);
      lastError = e is ApiException ? e.message : 'No se pudo iniciar sesión.';
      notifyListeners();
      return false;
    }
  }

  Future<StoredCredentials?> _prepareBiometricOffer(
    String cedula,
    String password,
  ) async {
    try {
      if (!await _biometrics.isAvailable()) return null;
      if (await _biometrics.enabledCedula() == cedula) return null;
      if (await _biometrics.wasDeclined(cedula)) return null;
      return StoredCredentials(cedula: cedula, password: password);
    } catch (e, st) {
      debugPrint('AuthState._prepareBiometricOffer failed: $e\n$st');
      return null;
    }
  }

  /// Entrega (una sola vez) las credenciales del login manual para ofrecer la biometría; luego no se conservan.
  StoredCredentials? takeBiometricOffer() {
    final offer = _biometricOffer;
    _biometricOffer = null;
    return offer;
  }

  /// Login con huella / Face ID: pide la biometría, lee las credenciales guardadas y hace el login normal. Si el
  /// servidor ya no las acepta (la contraseña cambió), se borran para no quedar en un ciclo.
  Future<bool> loginWithBiometrics() async {
    final stored = await _biometrics.readWithBiometrics();
    if (stored == null) return false;
    final ok = await login(
      stored.cedula,
      stored.password,
      offerBiometrics: false,
    );
    if (!ok && _lastLoginRejectedCredentials) {
      await _biometrics.disable();
      lastError = 'Tu contraseña cambió. Ingresa con ella y vuelve a activar el ingreso biométrico.';
      notifyListeners();
    }
    return ok;
  }

  /// Equivalente a hasRole(...roles) en frontend-web/src/stores/auth.ts —
  /// el router (app_router.dart) y AppShell la usan para decidir qué rutas y
  /// destinos de navegación mostrar por rol.
  bool hasRole(String role) => currentUser?.role == role;

  bool hasAnyRole(List<String> roles) =>
      currentUser != null && roles.contains(currentUser!.role);

  Future<void> logout() async {
    // Revoca la sesión en el servidor (clave para la sesión única del técnico); si falla la red igual
    // se cierra localmente — la sesión vence sola o la cierra un administrador.
    try {
      await _authApi.logout();
    } catch (e, st) {
      debugPrint(
        'AuthState.logout: no se pudo revocar la sesión en el servidor: $e\n$st',
      );
    }
    await RealtimeService.instance.disconnect();
    await _client.setToken(null);
    currentUser = null;
    signedOutByUser = true;
    status = AuthStatus.unauthenticated;
    notifyListeners();
  }

  /// Tras un cambio exitoso la sesión queda cerrada (el servidor revoca todas): el `redirect` de app_router.dart
  /// reacciona al notifyListeners() y lleva al login, que muestra [loginNotice].
  Future<String?> changePassword(
    String currentPassword,
    String newPassword,
  ) async {
    try {
      await _authApi.changePassword(currentPassword, newPassword);
      // La contraseña guardada para la biometría quedó vencida: se borra y se vuelve a ofrecer en el próximo login.
      final cedula = currentUser?.cedula;
      if (cedula != null) {
        if (await _biometrics.enabledCedula() == cedula) {
          await _biometrics.disable();
        }
        await _biometrics.clearDeclined(cedula);
      }
      // El servidor invalida todas las sesiones al cambiar la contraseña: el token actual ya no sirve, así que se
      // cierra localmente (sin llamar al servidor) y se vuelve al login.
      await RealtimeService.instance.disconnect();
      await _client.setToken(null);
      currentUser = null;
      status = AuthStatus.unauthenticated;
      loginNotice = 'Contraseña actualizada. Ingresa con tu nueva contraseña.';
      notifyListeners();
      return null;
    } catch (e, st) {
      debugPrint('AuthState.changePassword failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo cambiar la contraseña.';
    }
  }

  void _handleUnauthorized() {
    if (status == AuthStatus.authenticated) {
      RealtimeService.instance.disconnect();
      currentUser = null;
      status = AuthStatus.unauthenticated;
      lastError = 'Tu sesión expiró. Ingresa de nuevo.';
      notifyListeners();
    }
  }
}
