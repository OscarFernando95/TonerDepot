import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../models/my_work_filters.dart';
import '../models/pending_installation.dart';
import '../models/service_ticket.dart';
import '../models/technician_status.dart';

import 'package:image_picker/image_picker.dart';

import '../services/api_client.dart';
import '../services/device_capture.dart';
import '../services/inventory_api.dart';
import '../services/maintenance_order_api.dart';
import '../services/realtime_service.dart';
import '../services/technician_api.dart';
import '../services/ticket_api.dart';

/// Espejo de MyWorkView.vue, recortado al alcance de esta versión: tickets,
/// órdenes e instalaciones pendientes propias, con check-in/check-out.
/// Coverage (trabajo de otros técnicos) queda para una siguiente iteración.
class MyWorkState extends ChangeNotifier {
  MyWorkState(ApiClient client)
    : _technicianApi = TechnicianApi(client),
      _ticketApi = TicketApi(client),
      _orderApi = MaintenanceOrderApi(client),
      inventoryApi = InventoryApi(client) {
    // Un ticket/orden asignado o quitado desde la web actualiza esta pantalla sola. No mientras hay una
    // acción de check-in/out en curso (busyWithAction): eso ya recarga todo al terminar.
    _unsubscribe = RealtimeService.instance.subscribe(
      [
        'Ticket',
        'MaintenanceOrder',
        'Visit',
        'Technician',
        'TechnicianAsset',
        'Asset',
      ],
      (_) {
        if (!busyWithAction) loadAll(silent: true);
      },
    );
  }

  final TechnicianApi _technicianApi;
  final TicketApi _ticketApi;
  final MaintenanceOrderApi _orderApi;

  /// Kit y repuestos del check-out (la hoja los consulta directamente).
  final InventoryApi inventoryApi;
  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;

  /// Avisos de stock negativo del último check-out exitoso (la visita ya está cerrada); vacío si no hubo.
  List<String> lastStockWarnings = [];

  TechnicianSelfStatus? status;
  List<ServiceTicket> tickets = [];
  List<MaintenanceOrder> orders = [];
  List<PendingInstallation> installations = [];

  /// Tickets pendientes de las máquinas del técnico que NO son suyos (de otro técnico o sin asignar). Solo lectura.
  List<ServiceTicket> machineTickets = [];

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

  List<ServiceTicket> get checkInableTickets => pendingTickets(tickets);

  List<MaintenanceOrder> get checkInableOrders => pendingOrders(orders);

  /// A diferencia de tickets/órdenes, una instalación "tomada por otro
  /// técnico" (takenByAnotherTechnician) sigue apareciendo en la lista — el
  /// backend la deja visible, solo bloquea el check-in (ver
  /// InstallationCard). No hay filtro de "checkInable" aquí porque eso lo
  /// decide cada tarjeta individualmente.
  List<PendingInstallation> get pendingInstallations => installations;

  Future<void> loadAll({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _technicianApi.getMyStatus(),
        _ticketApi.listMine(),
        _orderApi.list(),
        _technicianApi.listPendingInstallations(),
        // Sección informativa: si falla no debe tumbar el resto de la pantalla.
        _loadMachineTickets(),
      ]);
      status = results[0] as TechnicianSelfStatus;
      tickets = results[1] as List<ServiceTicket>;
      orders = results[2] as List<MaintenanceOrder>;
      installations = results[3] as List<PendingInstallation>;
      final machine = results[4] as List<ServiceTicket>?;
      if (machine != null) machineTickets = machine;
    } catch (e, st) {
      debugPrint('MyWorkState.loadAll failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudo cargar tu trabajo.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<List<ServiceTicket>?> _loadMachineTickets() async {
    try {
      return await _technicianApi.listMachineTickets();
    } catch (e, st) {
      debugPrint('MyWorkState._loadMachineTickets failed: $e\n$st');
      return null; // conserva lo que había
    }
  }

  /// Tickets y órdenes exigen la foto "antes" (ya tomada por quien llama). Se sube primero, y su id viaja en el
  /// check-in junto con la ubicación (se registra y se alerta si falta o está lejos; nunca bloquea).
  Future<String?> checkInTicket(ServiceTicket ticket, XFile photo) =>
      _runAction(() async {
        final (evidenceId, position) = await _uploadAndLocate(
          photo,
          'Antes',
          ticketId: ticket.id,
        );
        return _technicianApi.checkIn(
          CheckInRequest(
            serviceTicketId: ticket.id,
            beforeEvidenceId: evidenceId,
            position: position,
          ),
        );
      });

  Future<String?> checkInOrder(MaintenanceOrder order, XFile photo) =>
      _runAction(() async {
        final (evidenceId, position) = await _uploadAndLocate(
          photo,
          'Antes',
          orderId: order.id,
        );
        return _technicianApi.checkIn(
          CheckInRequest(
            maintenanceOrderId: order.id,
            beforeEvidenceId: evidenceId,
            position: position,
          ),
        );
      });

  /// Las instalaciones no llevan foto (no hay ticket/orden al que atarla), pero sí ubicación.
  Future<String?> checkInInstallation(PendingInstallation installation) =>
      _runAction(() async {
        final position = await DeviceCapture.currentPosition();
        return _technicianApi.checkIn(
          CheckInRequest(assetId: installation.assetId, position: position),
        );
      });

  /// `photo` es la foto "después": obligatoria al resolver un ticket u orden (la hoja de check-out lo exige).
  Future<String?> checkOut(
    CheckOutRequest request, {
    XFile? photo,
    XFile? counterPhoto,
  }) => _runAction(() async {
    if (counterPhoto != null) {
      final (evidenceId, _) = await _uploadAndLocate(
        counterPhoto,
        'Contador',
        ticketId: activeTicket?.id,
        orderId: activeTicket == null ? activeOrder?.id : null,
      );
      request.counterEvidenceId = evidenceId;
    }
    if (photo != null) {
      final (evidenceId, position) = await _uploadAndLocate(
        photo,
        'Despues',
        ticketId: activeTicket?.id,
        orderId: activeTicket == null ? activeOrder?.id : null,
      );
      request.afterEvidenceId = evidenceId;
      request.position = position;
    } else {
      request.position = await DeviceCapture.currentPosition();
    }
    return _technicianApi.checkOut(request);
  });

  Future<(String, PositionFix?)> _uploadAndLocate(
    XFile photo,
    String kind, {
    String? ticketId,
    String? orderId,
  }) async {
    final results = await Future.wait<Object?>([
      _technicianApi.uploadEvidence(
        photo,
        kind: kind,
        ticketId: ticketId,
        orderId: orderId,
      ),
      DeviceCapture.currentPosition(),
    ]);
    return (results[0] as String, results[1] as PositionFix?);
  }

  /// Corre una acción de check-in/check-out, recarga todo al terminar (igual
  /// que loadAll() tras cada acción en MyWorkView.vue) y devuelve un mensaje
  /// de error para mostrar en un SnackBar, o null si salió bien.
  Future<String?> _runAction(
    Future<TechnicianSelfStatus> Function() action,
  ) async {
    busyWithAction = true;
    lastStockWarnings = [];
    notifyListeners();
    try {
      final result = await action();
      lastStockWarnings = result.stockWarnings;
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
