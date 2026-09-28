import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';

/// Lista de órdenes para Coordinador/Administrador — el backend ya devuelve
/// todas (sin filtrar), igual que MaintenanceOrdersView.vue el filtro de
/// estado es 100% client-side.
class MaintenanceOrdersState extends ChangeNotifier {
  MaintenanceOrdersState(ApiClient client) : _api = MaintenanceOrderApi(client);

  final MaintenanceOrderApi _api;

  bool loading = false;
  String? error;
  List<MaintenanceOrder> orders = [];
  String? statusFilter;

  List<MaintenanceOrder> get filtered => statusFilter == null
      ? orders
      : orders.where((o) => o.status == statusFilter).toList();

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      orders = await _api.list();
    } catch (e, st) {
      debugPrint('MaintenanceOrdersState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar las órdenes.';
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
