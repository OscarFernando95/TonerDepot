import 'package:flutter/foundation.dart';

import '../models/client_home.dart';
import '../services/api_client.dart';
import '../services/client_portal_api.dart';
import '../services/realtime_service.dart';

/// Estado del Inicio del Cliente: un solo llamado (GET /client-portal/home) y recarga silenciosa cuando cambia algo
/// que lo afecta (tickets, órdenes de mantenimiento o activos de su empresa).
class ClientHomeState extends ChangeNotifier {
  ClientHomeState(ApiClient client) : _api = ClientPortalApi(client) {
    _unsubscribe = RealtimeService.instance.subscribe([
      'Ticket',
      'MaintenanceOrder',
      'Asset',
    ], (_) => load(silent: true));
  }

  final ClientPortalApi _api;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;

  ClientHome? home;
  bool loading = false;
  String? error;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final result = await _api.getHome();
      if (_disposed) return;
      home = result;
      error = null;
    } catch (e, st) {
      debugPrint('ClientHomeState.load failed: $e\n$st');
      // Una recarga silenciosa que falla no tapa lo que ya se ve.
      if (!silent || home == null) {
        error = e is ApiException ? e.message : 'No se pudo cargar tu inicio.';
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
