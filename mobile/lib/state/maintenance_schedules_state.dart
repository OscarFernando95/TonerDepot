import 'package:flutter/foundation.dart';

import '../models/maintenance_schedule.dart';
import '../services/api_client.dart';
import '../services/maintenance_schedule_api.dart';

class MaintenanceSchedulesState extends ChangeNotifier {
  MaintenanceSchedulesState(ApiClient client)
    : _api = MaintenanceScheduleApi(client);

  final MaintenanceScheduleApi _api;
  static const _pageSize = 50;

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  bool busyWithAction = false;
  String? error;
  List<MaintenanceSchedule> schedules = [];
  int _page = 1;
  String? urgencyFilter;

  /// GET /maintenance-schedules no filtra server-side: filtramos por urgencia
  /// sobre lo ya cargado, igual que Activos/Contratos.
  List<MaintenanceSchedule> get filtered => urgencyFilter == null
      ? schedules
      : schedules.where((s) => s.urgency == urgencyFilter).toList();

  Future<void> load() async {
    loading = true;
    error = null;
    _page = 1;
    notifyListeners();
    try {
      final page = await _api.list(page: _page, pageSize: _pageSize);
      schedules = page.items;
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los cronogramas.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> loadMore() async {
    if (loadingMore || !hasMore) return;
    loadingMore = true;
    notifyListeners();
    try {
      final page = await _api.list(page: _page + 1, pageSize: _pageSize);
      _page += 1;
      schedules = [...schedules, ...page.items];
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.loadMore failed: $e\n$st');
    } finally {
      loadingMore = false;
      notifyListeners();
    }
  }

  void setUrgencyFilter(String? urgency) {
    urgencyFilter = urgency;
    notifyListeners();
  }

  /// Devuelve el mensaje a mostrar (éxito o error) para que la pantalla solo
  /// tenga que pintarlo en un SnackBar.
  Future<String> backfill() async {
    busyWithAction = true;
    notifyListeners();
    try {
      final created = await _api.backfill();
      await load();
      return created > 0
          ? 'Se crearon $created cronograma(s) nuevo(s).'
          : 'No había activos instalados sin cronograma.';
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.backfill failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo completar la regeneración.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  Future<String> evaluateNow() async {
    busyWithAction = true;
    notifyListeners();
    try {
      final created = await _api.evaluateNow();
      await load();
      return created > 0
          ? 'Se generaron $created orden(es) de mantenimiento.'
          : 'Ningún cronograma está por vencer todavía.';
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.evaluateNow failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo evaluar los cronogramas.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
