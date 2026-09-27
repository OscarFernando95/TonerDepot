import 'package:flutter/foundation.dart';

import '../models/service_ticket.dart';
import '../services/api_client.dart';
import '../services/ticket_api.dart';

/// Lista de tickets para Cliente/Administrador/Coordinador (el backend ya
/// filtra por rol — ver TicketApi.listMine). Distinta de MyWorkState, que es
/// la vista de autoservicio de Tecnico y no aplica aquí.
class TicketsListState extends ChangeNotifier {
  TicketsListState(ApiClient client) : _api = TicketApi(client);

  final TicketApi _api;

  bool loading = false;
  String? error;
  List<ServiceTicket> tickets = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      tickets = await _api.listMine();
    } catch (e, st) {
      debugPrint('TicketsListState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudieron cargar los tickets.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
