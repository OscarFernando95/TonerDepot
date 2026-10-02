import 'package:flutter/foundation.dart';

import '../models/city.dart';
import '../models/zone.dart';
import '../services/api_client.dart';
import '../services/city_api.dart';
import '../services/realtime_service.dart';
import '../services/zone_api.dart';

/// Gestión de zonas (crear, renombrar, borrar, municipios) — solo Staff.
class ZonesState extends ChangeNotifier {
  ZonesState(ApiClient client)
    : _api = ZoneApi(client),
      _cityApi = CityApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Zone'], (e) {
      if (busyWithAction) return;
      load(silent: true);
    });
  }

  final ZoneApi _api;
  final CityApi _cityApi;
  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<Zone> zones = [];
  List<City> _cities = [];
  bool _citiesLoaded = false;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      zones = await _api.list();
    } catch (e, st) {
      debugPrint('ZonesState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudieron cargar las zonas.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Catálogo de municipios (se pide una sola vez, solo al editar municipios).
  Future<List<City>> loadCities() async {
    if (!_citiesLoaded) {
      _cities = await _cityApi.list();
      _citiesLoaded = true;
    }
    return _cities;
  }

  Future<String?> create(String name) => _run('create', 'No se pudo crear la zona.', () => _api.create(name));

  Future<String?> rename(String id, String name) =>
      _run('rename', 'No se pudo renombrar la zona.', () => _api.rename(id, name));

  Future<String?> setCities(String id, List<String> cityIds) =>
      _run('setCities', 'No se pudieron guardar los municipios.', () => _api.setCities(id, cityIds));

  Future<String?> delete(String id) => _run('delete', 'No se pudo eliminar la zona.', () => _api.delete(id));

  /// Ejecuta la mutación y recarga el catálogo (un cambio de municipios puede
  /// tocar otras zonas). Devuelve null si salió bien, o un mensaje de error.
  Future<String?> _run(String op, String fallback, Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      zones = await _api.list();
      return null;
    } catch (e, st) {
      debugPrint('ZonesState.$op failed: $e\n$st');
      return e is ApiException ? e.message : fallback;
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
