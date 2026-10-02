import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../models/maintenance_schedule.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/maintenance_schedule_api.dart';

class MaintenanceScheduleDetailState extends ChangeNotifier {
  MaintenanceScheduleDetailState(ApiClient client, this.scheduleId)
    : _api = MaintenanceScheduleApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(
      ['Schedule', 'MaintenanceOrder'],
      (e) {
        if (busyWithAction) return;
        if (!(e.entity != 'Schedule' || e.affects(scheduleId))) return;
        load(silent: true);
      },
    );
  }

  final MaintenanceScheduleApi _api;
  final String scheduleId;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  MaintenanceSchedule? schedule;
  List<MaintenanceOrder> orders = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _api.getById(scheduleId),
        _api.orders(scheduleId),
      ]);
      schedule = results[0] as MaintenanceSchedule;
      orders = results[1] as List<MaintenanceOrder>;
    } catch (e, st) {
      debugPrint('MaintenanceScheduleDetailState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudo cargar el cronograma.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> setStatus(bool isActive) async {
    busyWithAction = true;
    notifyListeners();
    try {
      schedule = await _api.setStatus(scheduleId, isActive);
      return null;
    } catch (e, st) {
      debugPrint('MaintenanceScheduleDetailState.setStatus failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo actualizar el cronograma.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
