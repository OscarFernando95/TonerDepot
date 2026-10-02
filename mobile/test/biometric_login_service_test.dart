import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:local_auth/local_auth.dart';
import 'package:local_auth_platform_interface/local_auth_platform_interface.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:toner_tecnico/services/biometric_login_service.dart';

class _FakeAuth implements LocalAuthentication {
  _FakeAuth({
    this.supported = true,
    this.biometrics = const [BiometricType.fingerprint],
    this.passes = true,
    this.throwsOnAuthenticate = false,
  });

  final bool supported;
  final List<BiometricType> biometrics;
  bool passes;
  final bool throwsOnAuthenticate;
  int authenticateCalls = 0;

  @override
  Future<bool> isDeviceSupported() async => supported;

  @override
  Future<List<BiometricType>> getAvailableBiometrics() async => biometrics;

  @override
  Future<bool> authenticate({
    required String localizedReason,
    Iterable<AuthMessages> authMessages = const [],
    bool biometricOnly = false,
    bool sensitiveTransaction = true,
    bool persistAcrossBackgrounding = false,
  }) async {
    authenticateCalls++;
    if (throwsOnAuthenticate) {
      throw const LocalAuthException(code: LocalAuthExceptionCode.userCanceled);
    }
    // El servicio debe exigir biometría real, no el PIN del dispositivo.
    expect(biometricOnly, isTrue);
    return passes;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

BiometricLoginService _service(_FakeAuth auth) {
  FlutterSecureStorage.setMockInitialValues({});
  SharedPreferences.setMockInitialValues({});
  return BiometricLoginService.forTesting(
    auth: auth,
    storage: const FlutterSecureStorage(),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('no está disponible sin soporte o sin biometría registrada', () async {
    expect(await _service(_FakeAuth(supported: false)).isAvailable(), isFalse);
    expect(
      await _service(_FakeAuth(biometrics: const [])).isAvailable(),
      isFalse,
    );
    expect(await _service(_FakeAuth()).isAvailable(), isTrue);
  });

  test('Face ID solo si hay rostro y no huella', () async {
    expect(
      await _service(_FakeAuth(biometrics: const [BiometricType.face]))
          .methodLabel(),
      'Face ID',
    );
    expect(
      await _service(
        _FakeAuth(
          biometrics: const [BiometricType.face, BiometricType.fingerprint],
        ),
      ).methodLabel(),
      'huella',
    );
    expect(await _service(_FakeAuth()).methodLabel(), 'huella');
  });

  test('activar guarda las credenciales solo si pasa la biometría', () async {
    final denied = _service(_FakeAuth(passes: false));
    expect(await denied.enable('123', 'Secreta*123'), isFalse);
    expect(await denied.isEnabled(), isFalse);

    final granted = _service(_FakeAuth());
    expect(await granted.enable('123', 'Secreta*123'), isTrue);
    expect(await granted.enabledCedula(), '123');
  });

  test('leer exige biometría: sin ella no entrega la contraseña', () async {
    final auth = _FakeAuth();
    final service = _service(auth);
    await service.enable('123', 'Secreta*123');

    auth.passes = false;
    expect(await service.readWithBiometrics(), isNull);

    auth.passes = true;
    final creds = await service.readWithBiometrics();
    expect(creds?.cedula, '123');
    expect(creds?.password, 'Secreta*123');
  });

  test('cancelar el diálogo no lanza ni entrega nada', () async {
    final service = _service(_FakeAuth(throwsOnAuthenticate: true));
    expect(await service.enable('123', 'x'), isFalse);
    expect(await service.readWithBiometrics(), isNull);
  });

  test('leer sin nada guardado no pide biometría', () async {
    final auth = _FakeAuth();
    final service = _service(auth);
    expect(await service.readWithBiometrics(), isNull);
    expect(auth.authenticateCalls, 0);
  });

  test('desactivar borra todo', () async {
    final service = _service(_FakeAuth());
    await service.enable('123', 'Secreta*123');
    await service.disable();
    expect(await service.isEnabled(), isFalse);
    expect(await service.readWithBiometrics(), isNull);
  });

  test('"ahora no" se recuerda por cédula y se puede limpiar', () async {
    final service = _service(_FakeAuth());
    expect(await service.wasDeclined('123'), isFalse);
    await service.markDeclined('123');
    expect(await service.wasDeclined('123'), isTrue);
    expect(await service.wasDeclined('456'), isFalse);
    await service.clearDeclined('123');
    expect(await service.wasDeclined('123'), isFalse);
  });
}
