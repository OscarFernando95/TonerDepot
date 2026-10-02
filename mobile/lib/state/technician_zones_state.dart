import 'package:flutter/foundation.dart';

import '../models/zone.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_management_api.dart';
import '../services/zone_api.dart';

/// Zonas asignadas a un técnico (pantalla de gestión, solo Staff).
class TechnicianZonesState extends ChangeNotifier {
  TechnicianZonesState(ApiClient client, this.technicianId)
    : _api = TechnicianManagementApi(client),
      _zoneApi = ZoneApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Technician', 'Zone'], (e) {
      if (busyWithAction) return;
      load(silent: true);
    });
  }

  final TechnicianManagementApi _api;
  final ZoneApi _zoneApi;
  final String technicianId;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<TechnicianZone> zones = [];
  List<Zone> allZones = [];

  List<String> get assignedIds => [for (final z in zones) z.zoneId];

  /// Zonas del catálogo que aún no tiene asignadas.
  List<Zone> get availableZones => ZoneSelection.available(allZones, assignedIds);

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _api.getZones(technicianId),
        _zoneApi.list(),
      ]);
      zones = results[0] as List<TechnicianZone>;
      allZones = results[1] as List<Zone>;
    } catch (e, st) {
      debugPrint('TechnicianZonesState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudieron cargar las zonas.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> addZone(String zoneId) => _save(ZoneSelection.add(assignedIds, zoneId));

  Future<String?> replaceZone(String oldZoneId, String newZoneId) =>
      _save(ZoneSelection.replace(assignedIds, oldZoneId, newZoneId));

  Future<String?> removeZone(String zoneId) => _save(ZoneSelection.remove(assignedIds, zoneId));

  /// Devuelve null si salió bien, o un mensaje de error.
  Future<String?> _save(List<String> zoneIds) async {
    busyWithAction = true;
    notifyListeners();
    try {
      zones = await _api.setZones(technicianId, zoneIds);
      return null;
    } catch (e, st) {
      debugPrint('TechnicianZonesState._save failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudieron guardar las zonas.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
