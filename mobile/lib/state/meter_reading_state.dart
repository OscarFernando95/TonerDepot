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
///
/// Maneja dos listas independientes:
/// - `assets` ("Vinculados"): activos que un administrador vinculó
///   explícitamente al técnico (TechnicianAsset).
/// - `coverageAssets` ("Por cobertura", respaldo): todos los activos
///   instalados en las ciudades de cobertura del técnico (TechnicianCoverage),
///   estén o no vinculados a él o a otro técnico — para cuando el técnico
///   titular de un activo no está disponible (vacaciones, incapacidad,
///   renuncia/despido) y de otro modo nadie más podría registrarle lecturas.
/// Nunca se fusionan: un mismo activo puede aparecer en ambas si, además de
/// estar vinculado a este técnico, también cae en su cobertura.
class MeterReadingState extends ChangeNotifier {
  MeterReadingState(ApiClient client) : _api = MeterReadingApi(client);

  final MeterReadingApi _api;
  static const _noCity = 'Sin ciudad';

  bool loading = false;
  bool saving = false;
  String? error;
  List<MeterReadingAsset> assets = [];

  bool loadingCoverage = false;
  String? coverageError;
  List<MeterReadingAsset> coverageAssets = [];

  List<CityGroup> get groupedByCity => _groupByCity(assets);
  List<CityGroup> get coverageGroupedByCity => _groupByCity(coverageAssets);

  List<CityGroup> _groupByCity(List<MeterReadingAsset> source) {
    final byCity = <String, List<MeterReadingAsset>>{};
    for (final a in source) {
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

  Future<void> loadCoverage() async {
    loadingCoverage = true;
    coverageError = null;
    notifyListeners();
    try {
      coverageAssets = await _api.listAssetsByCoverage();
    } catch (e, st) {
      debugPrint('MeterReadingState.loadCoverage failed: $e\n$st');
      coverageError = e is ApiException ? e.message : 'No se pudieron cargar los equipos de tu cobertura.';
    } finally {
      loadingCoverage = false;
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
      // El mismo activo puede aparecer en "vinculados" y en "por cobertura" a
      // la vez, así que hay que refrescar ambas listas tras registrar.
      await Future.wait([load(), loadCoverage()]);
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
