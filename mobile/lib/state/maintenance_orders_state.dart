import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/maintenance_order_api.dart';

/// Lista de órdenes para Coordinador/Administrador — el backend ya devuelve
/// todas (sin filtrar), igual que MaintenanceOrdersView.vue el filtro de
/// estado es 100% client-side.
class MaintenanceOrdersState extends ChangeNotifier {
  MaintenanceOrdersState(ApiClient client)
    : _api = MaintenanceOrderApi(client) {
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
  List<MaintenanceOrder> orders = [];
  String? statusFilter;

  List<MaintenanceOrder> get filtered => statusFilter == null
      ? orders
      : orders.where((o) => o.status == statusFilter).toList();

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      orders = await _api.list();
    } catch (e, st) {
      debugPrint('MaintenanceOrdersState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar las órdenes.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  void setFilter(String? status) {
    statusFilter = status;
    notifyListeners();
  }
}
