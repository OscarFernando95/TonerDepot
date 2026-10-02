import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_management_api.dart';

class TechnicianTimeOffState extends ChangeNotifier {
  TechnicianTimeOffState(ApiClient client, this.technicianId)
    : _api = TechnicianManagementApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Technician'], (e) {
      if (busy) return;
      load(silent: true);
    });
  }

  final TechnicianManagementApi _api;
  final String technicianId;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busy = false;
  String? error;
  List<TimeOff> items = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      items = await _api.listTimeOff(technicianId);
    } catch (e, st) {
      debugPrint('TechnicianTimeOffState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudo cargar el listado.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> add({
    required DateTime startsAt,
    required DateTime endsAt,
    String? reason,
  }) => _run(
    () => _api.addTimeOff(
      technicianId,
      startsAt: startsAt,
      endsAt: endsAt,
      reason: reason,
    ),
  );

  Future<String?> cancel(String timeOffId) =>
      _run(() => _api.cancelTimeOff(technicianId, timeOffId));

  Future<String?> _run(Future<void> Function() action) async {
    busy = true;
    notifyListeners();
    try {
      await action();
      await load();
      return null;
    } catch (e, st) {
      debugPrint('TechnicianTimeOffState action failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }
}
