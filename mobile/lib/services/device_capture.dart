import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:image_picker/image_picker.dart';

/// Ubicación del técnico al hacer check-in/out (se compara con la sede en el backend).
class PositionFix {
  const PositionFix({
    required this.latitude,
    required this.longitude,
    required this.accuracyMeters,
  });

  final double latitude;
  final double longitude;
  final double accuracyMeters;
}

/// Cámara y GPS del dispositivo. Ninguna de las dos lanza: si el usuario niega el permiso, no hay GPS o
/// cancela la cámara, devuelven null y quien llama decide (la ubicación solo se registra y se alerta en el
/// servidor; la foto sí es obligatoria y el flujo lo exige).
class DeviceCapture {
  DeviceCapture._();

  static Future<PositionFix?> currentPosition({
    Duration timeout = const Duration(seconds: 10),
  }) async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return null;

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        return null;
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: timeout,
        ),
      );
      return PositionFix(
        latitude: position.latitude,
        longitude: position.longitude,
        accuracyMeters: position.accuracy,
      );
    } catch (e, st) {
      debugPrint('DeviceCapture.currentPosition failed: $e\n$st');
      return null;
    }
  }

  /// Abre la cámara. Reduce la imagen al tomarla (las fotos de celular pesan varios MB; el servidor acepta hasta 10).
  static Future<XFile?> takePhoto() async {
    try {
      return await ImagePicker().pickImage(
        source: ImageSource.camera,
        maxWidth: 1600,
        maxHeight: 1600,
        imageQuality: 82,
      );
    } catch (e, st) {
      debugPrint('DeviceCapture.takePhoto failed: $e\n$st');
      return null;
    }
  }
}
