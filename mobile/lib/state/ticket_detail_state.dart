import 'package:flutter/foundation.dart';

import '../models/service_ticket.dart';
import '../services/api_client.dart';
import '../services/ticket_api.dart';

/// Estado de una sola pantalla de detalle — se instancia por ruta (ver
/// ticket_detail_screen.dart), no vive en el árbol de providers globales.
class TicketDetailState extends ChangeNotifier {
  TicketDetailState(ApiClient client, this.ticketId) : _api = TicketApi(client);

  final TicketApi _api;
  final String ticketId;

  bool loading = false;
  String? error;
  ServiceTicket? ticket;

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      ticket = await _api.getById(ticketId);
    } catch (e, st) {
      debugPrint('TicketDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar el ticket.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
