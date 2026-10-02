import 'package:flutter/foundation.dart';

import '../models/assignment_history.dart';
import '../models/service_ticket.dart';
import '../models/technician.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_management_api.dart';
import '../services/ticket_api.dart';

/// Estado de una sola pantalla de detalle — se instancia por ruta (ver
/// ticket_detail_screen.dart), no vive en el árbol de providers globales.
/// `isStaff` decide si se cargan también el historial de asignación y el
/// catálogo de técnicos (Administrador/Coordinador únicamente, igual que
/// TicketDetailView.vue).
class TicketDetailState extends ChangeNotifier {
  TicketDetailState(ApiClient client, this.ticketId, {required this.isStaff})
    : _api = TicketApi(client),
      _technicianApi = TechnicianManagementApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Ticket'], (e) {
      if (assigning || changingStatus) return;
      if (!(e.affects(ticketId))) return;
      load(silent: true);
    });
  }

  final TicketApi _api;
  final TechnicianManagementApi _technicianApi;
  final String ticketId;
  final bool isStaff;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  ServiceTicket? ticket;
  List<AssignmentHistory> history = [];
  List<Technician> technicians = [];

  bool assigning = false;
  bool changingStatus = false;
  String? actionError;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _api.getById(ticketId),
        if (isStaff)
          _api.getAssignmentHistory(ticketId)
        else
          Future.value(<AssignmentHistory>[]),
        if (isStaff) _technicianApi.list() else Future.value(<Technician>[]),
      ]);
      ticket = results[0] as ServiceTicket;
      if (isStaff) {
        history = results[1] as List<AssignmentHistory>;
        technicians = results[2] as List<Technician>;
      }
    } catch (e, st) {
      debugPrint('TicketDetailState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudo cargar el ticket.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Devuelve null si salió bien, o un mensaje de error.
  Future<String?> assign({required String technicianId, String? reason}) async {
    assigning = true;
    notifyListeners();
    try {
      ticket = await _api.assign(
        ticketId,
        technicianId: technicianId,
        reason: reason,
      );
      history = await _api.getAssignmentHistory(ticketId);
      return null;
    } catch (e) {
      return e is ApiException ? e.message : 'No se pudo asignar el ticket.';
    } finally {
      assigning = false;
      notifyListeners();
    }
  }

  Future<String?> changeStatus(String status) async {
    changingStatus = true;
    notifyListeners();
    try {
      ticket = await _api.setStatus(ticketId, status);
      return null;
    } catch (e) {
      return e is ApiException ? e.message : 'No se pudo cambiar el estado.';
    } finally {
      changingStatus = false;
      notifyListeners();
    }
  }
}
