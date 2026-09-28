import 'dart:async';
import 'package:flutter/foundation.dart';

import '../models/dashboard_summary.dart';
import '../services/api_client.dart';
import '../services/dashboard_api.dart';
import '../services/realtime_service.dart';

class DashboardState extends ChangeNotifier {
  DashboardState(ApiClient client) : _api = DashboardApi(client) {
    // Son agregados (SLA, MTTR, utilización): no vale la pena recalcular en cada ticket, así que se espacía un poco.
    _unsubscribe = RealtimeService.instance.subscribe(['Ticket', 'MaintenanceOrder', 'Visit'], (_) {
      _debounce?.cancel();
      _debounce = Timer(const Duration(seconds: 3), load);
    });
  }

  final DashboardApi _api;
  late final VoidCallback _unsubscribe;
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  DashboardSummary? summary;
  int periodDays = 30;

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      summary = await _api.getSummary(periodDays);
    } catch (e, st) {
      debugPrint('DashboardState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los indicadores.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> setPeriod(int days) async {
    if (days == periodDays) return;
    periodDays = days;
    await load();
  }
}
