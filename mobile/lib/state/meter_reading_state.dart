import 'package:flutter/foundation.dart';

import '../models/meter_reading_asset.dart';
import '../services/api_client.dart';
import '../services/meter_reading_api.dart';

class CityGroup {
  CityGroup(this.city, this.assets);
  final String city;
  final List<MeterReadingAsset> assets;
}

/// Solo para Administrador/Coordinador (ver comentario largo más abajo) —
/// agrupación de 2 niveles ciudad → cliente, espejo exacto de
/// `groupedByCity`/`ClientGroup`/`CityGroup` en MeterReadingsView.vue.
class MeterClientGroup {
  MeterClientGroup(this.clientLabel, this.assets);
  final String clientLabel;
  final List<MeterReadingAsset> assets;
}

class MeterCityGroup {
  MeterCityGroup(this.city, this.clientGroups);
  final String city;
  final List<MeterClientGroup> clientGroups;

  int get count => clientGroups.fold(0, (n, g) => n + g.assets.length);
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
  static const _noClient = 'Sin cliente';

  bool loading = false;
  bool saving = false;
  String? error;
  List<MeterReadingAsset> assets = [];

  bool loadingCoverage = false;
  String? coverageError;
  List<MeterReadingAsset> coverageAssets = [];

  List<CityGroup> get groupedByCity => _groupByCity(assets);
  List<CityGroup> get coverageGroupedByCity => _groupByCity(coverageAssets);

  // --- Solo Administrador/Coordinador: filtros + agrupación 2 niveles, tal
  // cual MeterReadingsView.vue (Técnico no usa nada de esto, sigue con las
  // pestañas Vinculados/Por cobertura de arriba). ---

  /// 'grouped' | 'flat' — espejo de viewMode en MeterReadingsView.vue.
  String viewMode = 'grouped';
  String? cityFilter;
  String? clientFilter;

  bool _matches(MeterReadingAsset a, String exclude) {
    final cityOk = exclude == 'city' || cityFilter == null || a.cityName == cityFilter;
    final clientOk = exclude == 'client' || clientFilter == null || a.clientId == clientFilter;
    return cityOk && clientOk;
  }

  List<MeterReadingAsset> get filtered =>
      assets.where((a) => _matches(a, '')).toList();

  List<String> get cityOptions => ({
    for (final a in assets)
      if (a.cityName != null && _matches(a, 'city')) a.cityName!,
  }.toList())..sort();

  List<(String, String)> get clientOptions {
    final seen = <String, String>{};
    for (final a in assets) {
      if (a.clientId != null && _matches(a, 'client')) {
        seen[a.clientId!] = a.clientName ?? a.clientId!;
      }
    }
    final list = seen.entries.map((e) => (e.key, e.value)).toList();
    list.sort((a, b) => a.$2.compareTo(b.$2));
    return list;
  }

  void setViewMode(String mode) {
    viewMode = mode;
    notifyListeners();
  }

  void setCityFilter(String? city) {
    cityFilter = city;
    notifyListeners();
  }

  void setClientFilter(String? clientId) {
    clientFilter = clientId;
    notifyListeners();
  }

  List<MeterCityGroup> get groupedByCityAndClient {
    final byCity = <String, Map<String, List<MeterReadingAsset>>>{};
    final clientLabels = <String, String>{};
    for (final a in filtered) {
      final city = a.cityName ?? _noCity;
      final clientKey = a.clientId ?? _noClient;
      clientLabels[clientKey] = a.clientName ?? _noClient;
      final byClient = byCity.putIfAbsent(city, () => {});
      byClient.putIfAbsent(clientKey, () => []).add(a);
    }
    final cities = byCity.entries.map((cityEntry) {
      final clientGroups = cityEntry.value.entries
          .map(
            (clientEntry) => MeterClientGroup(
              clientLabels[clientEntry.key] ?? _noClient,
              clientEntry.value,
            ),
          )
          .toList()
        ..sort((a, b) => a.clientLabel.compareTo(b.clientLabel));
      return MeterCityGroup(cityEntry.key, clientGroups);
    }).toList()
      ..sort((a, b) => a.city.compareTo(b.city));
    return cities;
  }

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
    int counterValue,
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
