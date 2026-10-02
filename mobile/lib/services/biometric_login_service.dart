import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:local_auth/local_auth.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Credenciales guardadas para el ingreso con huella / Face ID.
class StoredCredentials {
  const StoredCredentials({required this.cedula, required this.password});

  final String cedula;
  final String password;
}

/// Ingreso con huella o Face ID. La contraseña vive en el almacenamiento seguro del sistema (Keystore en Android,
/// Keychain en iOS, solo en este dispositivo) y solo se lee después de que el usuario pasa la biometría; con ella se
/// hace el login normal, así que siguen valiendo la sesión única del técnico, el bloqueo por intentos y la
/// invalidación al cambiar la contraseña. Es opt-in y se puede desactivar.
class BiometricLoginService {
  BiometricLoginService._({
    LocalAuthentication? auth,
    FlutterSecureStorage? storage,
  }) : _auth = auth ?? LocalAuthentication(),
       _storage =
           storage ??
           const FlutterSecureStorage(
             aOptions: AndroidOptions(encryptedSharedPreferences: true),
             // Sin sincronizar con iCloud ni migrar a otro dispositivo.
             iOptions: IOSOptions(
               accessibility: KeychainAccessibility.passcode,
               synchronizable: false,
             ),
           );

  static final BiometricLoginService instance = BiometricLoginService._();

  @visibleForTesting
  factory BiometricLoginService.forTesting({
    required LocalAuthentication auth,
    required FlutterSecureStorage storage,
  }) => BiometricLoginService._(auth: auth, storage: storage);

  final LocalAuthentication _auth;
  final FlutterSecureStorage _storage;

  static const _cedulaKey = 'toner_bio_cedula';
  static const _passwordKey = 'toner_bio_password';
  static const _declinedPrefix = 'toner_bio_declined_';

  /// El dispositivo tiene biometría y al menos una registrada.
  Future<bool> isAvailable() async {
    try {
      if (!await _auth.isDeviceSupported()) return false;
      return (await _auth.getAvailableBiometrics()).isNotEmpty;
    } catch (e, st) {
      debugPrint('BiometricLoginService.isAvailable failed: $e\n$st');
      return false;
    }
  }

  /// "Face ID" si el dispositivo reconoce rostro (y no huella); "huella" en cualquier otro caso.
  Future<String> methodLabel() async {
    try {
      final types = await _auth.getAvailableBiometrics();
      if (types.contains(BiometricType.face) &&
          !types.contains(BiometricType.fingerprint)) {
        return 'Face ID';
      }
    } catch (e, st) {
      debugPrint('BiometricLoginService.methodLabel failed: $e\n$st');
    }
    return 'huella';
  }

  Future<String?> enabledCedula() => _storage.read(key: _cedulaKey);

  Future<bool> isEnabled() async => await enabledCedula() != null;

  Future<bool> _authenticate(String reason) async {
    try {
      return await _auth.authenticate(
        localizedReason: reason,
        biometricOnly: true,
      );
    } on LocalAuthException catch (e) {
      // Cancelar no es un error; el resto (bloqueo por intentos, sin biometría) tampoco debe romper el login normal.
      debugPrint('BiometricLoginService: ${e.code}');
      return false;
    } catch (e, st) {
      debugPrint('BiometricLoginService._authenticate failed: $e\n$st');
      return false;
    }
  }

  /// Pide la biometría y, si pasa, guarda las credenciales. Devuelve si quedó activada.
  Future<bool> enable(String cedula, String password) async {
    if (!await _authenticate('Confirma para activar el ingreso biométrico')) {
      return false;
    }
    await _storage.write(key: _cedulaKey, value: cedula);
    await _storage.write(key: _passwordKey, value: password);
    return true;
  }

  Future<void> disable() async {
    await _storage.delete(key: _cedulaKey);
    await _storage.delete(key: _passwordKey);
  }

  /// Pide la biometría y devuelve las credenciales guardadas, o null si no hay, se canceló o falló.
  Future<StoredCredentials?> readWithBiometrics() async {
    final cedula = await _storage.read(key: _cedulaKey);
    final password = await _storage.read(key: _passwordKey);
    if (cedula == null || password == null) return null;
    if (!await _authenticate('Ingresa a Toner')) return null;
    return StoredCredentials(cedula: cedula, password: password);
  }

  /// El usuario dijo "ahora no" para esta cédula: no se vuelve a ofrecer (sí desde el menú).
  Future<bool> wasDeclined(String cedula) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool('$_declinedPrefix$cedula') ?? false;
  }

  Future<void> markDeclined(String cedula) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('$_declinedPrefix$cedula', true);
  }

  Future<void> clearDeclined(String cedula) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('$_declinedPrefix$cedula');
  }
}
