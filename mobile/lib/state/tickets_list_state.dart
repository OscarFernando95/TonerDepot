import 'package:flutter/foundation.dart';

import '../models/service_ticket.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/ticket_api.dart';

/// Lista de tickets para Cliente/Administrador/Coordinador (el backend ya
/// filtra por rol — ver TicketApi.listMine). Distinta de MyWorkState, que es
/// la vista de autoservicio de Tecnico y no aplica aquí.
class TicketsListState extends ChangeNotifier {
  TicketsListState(ApiClient client) : _api = TicketApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Ticket'], (e) {
      load(silent: true);
    });
  }

  final TicketApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  List<ServiceTicket> tickets = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      tickets = await _api.listMine();
    } catch (e, st) {
      debugPrint('TicketsListState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los tickets.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
