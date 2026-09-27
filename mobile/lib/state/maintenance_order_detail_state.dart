import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../models/technician.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../services/technician_management_api.dart';

class MaintenanceOrderDetailState extends ChangeNotifier {
  MaintenanceOrderDetailState(ApiClient client, this.orderId)
      : _orderApi = MaintenanceOrderApi(client),
        _technicianApi = TechnicianManagementApi(client);

  final MaintenanceOrderApi _orderApi;
  final TechnicianManagementApi _technicianApi;
  final String orderId;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  MaintenanceOrder? order;
  List<Technician> technicians = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([_orderApi.getById(orderId), _technicianApi.list()]);
      order = results[0] as MaintenanceOrder;
      technicians = results[1] as List<Technician>;
    } catch (e, st) {
      debugPrint('MaintenanceOrderDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar la orden.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> assign(String technicianId, {String? reason}) => _runAction(
        () => _orderApi.assign(orderId, technicianId: technicianId, reason: reason),
      );

  Future<String?> complete(int counterValue, DateTime? readingDate) => _runAction(
        () => _orderApi.complete(orderId, counterValue: counterValue, readingDate: readingDate),
      );

  Future<String?> cancel() => _runAction(() => _orderApi.cancel(orderId));

  Future<String?> _runAction(Future<MaintenanceOrder> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      order = await action();
      return null;
    } catch (e, st) {
      debugPrint('MaintenanceOrderDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
