import 'package:flutter/foundation.dart';

import '../models/city.dart';
import '../models/technician_coverage.dart';
import '../services/api_client.dart';
import '../services/city_api.dart';
import '../services/technician_management_api.dart';

class TechnicianCoverageState extends ChangeNotifier {
  TechnicianCoverageState(ApiClient client, this.technicianId)
      : _api = TechnicianManagementApi(client),
        _cityApi = CityApi(client);

  final TechnicianManagementApi _api;
  final CityApi _cityApi;
  final String technicianId;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<TechnicianCoverage> coverage = [];
  List<City> allCities = [];

  List<City> get availableCities =>
      allCities.where((c) => !coverage.any((cov) => cov.cityId == c.id)).toList();

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([_api.getCoverage(technicianId), _cityApi.list()]);
      coverage = results[0] as List<TechnicianCoverage>;
      allCities = results[1] as List<City>;
    } catch (e, st) {
      debugPrint('TechnicianCoverageState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar la cobertura.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> addCity(String cityId) async {
    busyWithAction = true;
    notifyListeners();
    try {
      final added = await _api.addCoverage(technicianId, cityId);
      coverage = [...coverage, added];
      return null;
    } catch (e, st) {
      debugPrint('TechnicianCoverageState.addCity failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo agregar la ciudad.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  Future<String?> removeCoverage(String coverageId) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await _api.removeCoverage(technicianId, coverageId);
      coverage = coverage.where((c) => c.id != coverageId).toList();
      return null;
    } catch (e, st) {
      debugPrint('TechnicianCoverageState.removeCoverage failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo quitar la ciudad.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
