import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import '../services/api_client.dart';
import '../services/technician_management_api.dart';

class TechnicianTimeOffState extends ChangeNotifier {
  TechnicianTimeOffState(ApiClient client, this.technicianId)
    : _api = TechnicianManagementApi(client);

  final TechnicianManagementApi _api;
  final String technicianId;

  bool loading = false;
  bool busy = false;
  String? error;
  List<TimeOff> items = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      items = await _api.listTimeOff(technicianId);
    } catch (e, st) {
      debugPrint('TechnicianTimeOffState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar el listado.';
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
