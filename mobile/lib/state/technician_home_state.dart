import 'package:flutter/foundation.dart';

import '../models/technician_home.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_home_api.dart';

/// Estado del Inicio del técnico: un solo llamado y recarga silenciosa cuando cambia algo que lo afecta (trabajo
/// asignado o quitado, visitas, stock, su zona).
class TechnicianHomeState extends ChangeNotifier {
  TechnicianHomeState(ApiClient client) : _api = TechnicianHomeApi(client) {
    _unsubscribe = RealtimeService.instance.subscribe([
      'Ticket',
      'MaintenanceOrder',
      'Visit',
      'Technician',
      'Zone',
      'Inventory',
    ], (_) => load(silent: true));
  }

  final TechnicianHomeApi _api;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;

  TechnicianHome? home;
  bool loading = false;
  String? error;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final result = await _api.get();
      home = result;
      error = null;
    } catch (e, st) {
      debugPrint('TechnicianHomeState.load failed: $e\n$st');
      // Una recarga silenciosa que falla no tapa lo que ya se ve.
      if (!silent || home == null) {
        error = e is ApiException ? e.message : 'No se pudo cargar el inicio.';
      }
    } finally {
      loading = false;
      if (!_disposed) notifyListeners();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _unsubscribe();
    super.dispose();
  }
}
