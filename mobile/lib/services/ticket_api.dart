import '../models/paged_result.dart';
import '../models/service_ticket.dart';
import 'api_client.dart';

class TicketApi {
  TicketApi(this._client);
  final ApiClient _client;

  /// El backend ya filtra por rol: un Tecnico solo ve los tickets asignados
  /// a él (RoleNames.StaffClientAndTechnicianRoles, ver README "Tickets de
  /// soporte correctivo") — no hace falta pasar ningún filtro adicional.
  Future<List<ServiceTicket>> listMine() async {
    try {
      final response = await _client.dio.get('/tickets');
      final page = PagedResult<ServiceTicket>.fromJson(
        response.data as Map<String, dynamic>,
        ServiceTicket.fromJson,
      );
      return page.items;
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }
}
