import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../models/pending_installation.dart';
import '../models/service_ticket.dart';
import '../models/technician_status.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../services/technician_api.dart';
import '../services/ticket_api.dart';

/// Espejo de MyWorkView.vue, recortado al alcance de esta versión: tickets,
/// órdenes e instalaciones pendientes propias, con check-in/check-out.
/// Coverage (trabajo de otros técnicos) queda para una siguiente iteración.
class MyWorkState extends ChangeNotifier {
  MyWorkState(ApiClient client)
      : _technicianApi = TechnicianApi(client),
        _ticketApi = TicketApi(client),
        _orderApi = MaintenanceOrderApi(client);

  final TechnicianApi _technicianApi;
  final TicketApi _ticketApi;
  final MaintenanceOrderApi _orderApi;

  bool loading = false;
  bool busyWithAction = false;
  String? error;

  TechnicianSelfStatus? status;
  List<ServiceTicket> tickets = [];
  List<MaintenanceOrder> orders = [];
  List<PendingInstallation> installations = [];

  ServiceTicket? get activeTicket {
    final id = status?.activeServiceTicketId;
    if (id == null) return null;
    for (final t in tickets) {
      if (t.id == id) return t;
    }
    return null;
  }

  MaintenanceOrder? get activeOrder {
    final id = status?.activeMaintenanceOrderId;
    if (id == null) return null;
    for (final o in orders) {
      if (o.id == id) return o;
    }
    return null;
  }

  PendingInstallation? get activeInstallation {
    final id = status?.activeAssetInstallationId;
    if (id == null) return null;
    for (final i in installations) {
      if (i.assetId == id) return i;
    }
    return null;
  }

  bool get isBusy => status?.isBusy ?? false;

  List<ServiceTicket> get checkInableTickets =>
      tickets.where((t) => t.status == 'Asignado' || t.status == 'EnProceso').toList();

  List<MaintenanceOrder> get checkInableOrders =>
      orders.where((o) => o.status == 'Asignada' || o.status == 'EnProceso').toList();

  /// A diferencia de tickets/órdenes, una instalación "tomada por otro
  /// técnico" (takenByAnotherTechnician) sigue apareciendo en la lista — el
  /// backend la deja visible, solo bloquea el check-in (ver
  /// InstallationCard). No hay filtro de "checkInable" aquí porque eso lo
  /// decide cada tarjeta individualmente.
  List<PendingInstallation> get pendingInstallations => installations;

  Future<void> loadAll() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([
        _technicianApi.getMyStatus(),
        _ticketApi.listMine(),
        _orderApi.list(),
        _technicianApi.listPendingInstallations(),
      ]);
      status = results[0] as TechnicianSelfStatus;
      tickets = results[1] as List<ServiceTicket>;
      orders = results[2] as List<MaintenanceOrder>;
      installations = results[3] as List<PendingInstallation>;
    } catch (e, st) {
      debugPrint('MyWorkState.loadAll failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar tu trabajo.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> checkInTicket(ServiceTicket ticket) => _runAction(
        () => _technicianApi.checkIn(CheckInRequest(serviceTicketId: ticket.id)),
      );

  Future<String?> checkInOrder(MaintenanceOrder order) => _runAction(
        () => _technicianApi.checkIn(CheckInRequest(maintenanceOrderId: order.id)),
      );

  Future<String?> checkInInstallation(PendingInstallation installation) => _runAction(
        () => _technicianApi.checkIn(CheckInRequest(assetId: installation.assetId)),
      );

  Future<String?> checkOut(CheckOutRequest request) => _runAction(
        () => _technicianApi.checkOut(request),
      );

  /// Corre una acción de check-in/check-out, recarga todo al terminar (igual
  /// que loadAll() tras cada acción en MyWorkView.vue) y devuelve un mensaje
  /// de error para mostrar en un SnackBar, o null si salió bien.
  Future<String?> _runAction(Future<TechnicianSelfStatus> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      await loadAll();
      return null;
    } catch (e, st) {
      debugPrint('MyWorkState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      // `finally` (no solo el `catch`) para que el botón de check-in/check-out se reactive también
      // cuando la acción SÍ sale bien — si no, queda deshabilitado hasta el próximo hot reload, que es
      // lo que recrea el estado desde cero y "lo arregla" de casualidad.
      busyWithAction = false;
      notifyListeners();
    }
  }
}
