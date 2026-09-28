import 'package:flutter/foundation.dart';

import '../models/meter_reading_asset.dart';
import '../services/api_client.dart';
import '../services/meter_reading_api.dart';

class CityGroup {
  CityGroup(this.city, this.assets);
  final String city;
  final List<MeterReadingAsset> assets;
}

/// Espejo recortado de MeterReadingsView.vue: agrupación por ciudad (la web
/// también agrupa por cliente dentro de cada ciudad; en el celular, con
/// pantallas más angostas, una sola agrupación por ciudad es más legible).
class MeterReadingState extends ChangeNotifier {
  MeterReadingState(ApiClient client) : _api = MeterReadingApi(client);

  final MeterReadingApi _api;
  static const _noCity = 'Sin ciudad';

  bool loading = false;
  bool saving = false;
  String? error;
  List<MeterReadingAsset> assets = [];

  List<CityGroup> get groupedByCity {
    final byCity = <String, List<MeterReadingAsset>>{};
    for (final a in assets) {
      byCity.putIfAbsent(a.cityName ?? _noCity, () => []).add(a);
    }
    final cities = byCity.keys.toList()..sort();
    return [
      for (final c in cities)
        CityGroup(
          c,
          byCity[c]!..sort(
            (x, y) => (x.clientName ?? '').compareTo(y.clientName ?? ''),
          ),
        ),
    ];
  }

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      assets = await _api.listAssets();
    } catch (e, st) {
      debugPrint('MeterReadingState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los equipos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Devuelve null si el registro salió bien, o un mensaje de error.
  Future<String?> register(
    MeterReadingAsset asset,
    double counterValue,
    DateTime? readingDate,
  ) async {
    if (asset.lastMeterReading != null &&
        counterValue < asset.lastMeterReading!) {
      return 'El contador no puede ser menor al último registrado (${asset.lastMeterReading!.toStringAsFixed(0)}).';
    }
    saving = true;
    notifyListeners();
    try {
      await _api.register(
        asset.assetId,
        counterValue: counterValue,
        readingDate: readingDate,
      );
      await load();
      return null;
    } catch (e, st) {
      debugPrint('MeterReadingState.register failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo registrar el contador.';
    } finally {
      saving = false;
      notifyListeners();
    }
  }
}
