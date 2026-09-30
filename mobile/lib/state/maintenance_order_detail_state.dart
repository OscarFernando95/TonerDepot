import 'package:flutter/foundation.dart';

import '../models/assignment_history.dart';
import '../models/maintenance_order.dart';
import '../models/technician.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../services/technician_management_api.dart';

class MaintenanceOrderDetailState extends ChangeNotifier {
  /// `isStaff` decide si se pide el historial de asignación — el endpoint es
  /// [Authorize(Roles = StaffRoles)] en el backend, así que un Técnico (que
  /// también puede llegar al detalle de su propia orden) recibiría 403.
  MaintenanceOrderDetailState(
    ApiClient client,
    this.orderId, {
    required this.isStaff,
  }) : _orderApi = MaintenanceOrderApi(client),
       _technicianApi = TechnicianManagementApi(client);

  final MaintenanceOrderApi _orderApi;
  final TechnicianManagementApi _technicianApi;
  final String orderId;
  final bool isStaff;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  MaintenanceOrder? order;
  List<Technician> technicians = [];
  List<AssignmentHistory> assignmentHistory = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([
        _orderApi.getById(orderId),
        _technicianApi.list(),
        if (isStaff) _orderApi.getAssignmentHistory(orderId),
      ]);
      order = results[0] as MaintenanceOrder;
      technicians = results[1] as List<Technician>;
      if (isStaff) {
        assignmentHistory = results[2] as List<AssignmentHistory>;
      }
    } catch (e, st) {
      debugPrint('MaintenanceOrderDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar la orden.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Tras asignar, recarga el historial (igual que MaintenanceOrderDetailView.vue
  /// saveAssign) — la nueva entrada no viene en la respuesta de /assign.
  Future<String?> assign(String technicianId, {String? reason}) =>
      _runAction(() async {
        order = await _orderApi.assign(
          orderId,
          technicianId: technicianId,
          reason: reason,
        );
        if (isStaff) {
          assignmentHistory = await _orderApi.getAssignmentHistory(orderId);
        }
      });

  Future<String?> complete(int counterValue, DateTime? readingDate) =>
      _runAction(() async {
        order = await _orderApi.complete(
          orderId,
          counterValue: counterValue,
          readingDate: readingDate,
        );
      });

  Future<String?> cancel() =>
      _runAction(() async => order = await _orderApi.cancel(orderId));

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
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
