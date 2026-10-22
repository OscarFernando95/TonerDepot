import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../services/realtime_service.dart';

/// Portal Cliente: órdenes de mantenimiento de los equipos de su empresa, de solo lectura. El backend ya filtra
/// por empresa (mismo endpoint que Staff/Técnico), así que no hay parámetro de alcance que mandar.
class MyMaintenanceState extends ChangeNotifier {
  MyMaintenanceState(ApiClient client) : _api = MaintenanceOrderApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['MaintenanceOrder'], (
      e,
    ) {
      load(silent: true);
    });
  }

  final MaintenanceOrderApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;

  /// Abiertas primero (la más próxima arriba) y luego las cerradas.
  List<MaintenanceOrder> orders = [];

  List<MaintenanceOrder> get upcoming => orders.where((o) => o.isOpen).toList();
  List<MaintenanceOrder> get finished =>
      orders.where((o) => !o.isOpen).toList();

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      orders = MaintenanceOrder.sortForClient(await _api.list());
    } catch (e, st) {
      debugPrint('MyMaintenanceState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los mantenimientos.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
