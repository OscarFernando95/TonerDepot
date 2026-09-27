import 'package:flutter/foundation.dart';

import '../models/dashboard_summary.dart';
import '../services/api_client.dart';
import '../services/dashboard_api.dart';

class DashboardState extends ChangeNotifier {
  DashboardState(ApiClient client) : _api = DashboardApi(client);

  final DashboardApi _api;

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
      error = e is ApiException ? e.message : 'No se pudieron cargar los indicadores.';
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
