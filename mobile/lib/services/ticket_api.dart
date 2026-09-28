import 'package:flutter/foundation.dart';

import '../models/paged_result.dart';
import '../models/service_ticket.dart';
import 'api_client.dart';

class TicketApi {
  TicketApi(this._client);
  final ApiClient _client;

  /// El backend ya filtra por rol: un Tecnico solo ve los tickets asignados
  /// a él, un Cliente solo los suyos (RoleNames.StaffClientAndTechnicianRoles,
  /// ver README "Tickets de soporte correctivo") — no hace falta pasar ningún
  /// filtro adicional.
  Future<List<ServiceTicket>> listMine() async {
    try {
      final response = await _client.dio.get('/tickets');
      final page = PagedResult<ServiceTicket>.fromJson(
        response.data as Map<String, dynamic>,
        ServiceTicket.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('TicketApi.listMine failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<ServiceTicket> getById(String id) async {
    try {
      final response = await _client.dio.get('/tickets/$id');
      return ServiceTicket.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TicketApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// El body NO lleva clientId: el backend lo infiere de
  /// clientLocationId → ClientLocation.ClientId (y devuelve 403 si un
  /// Cliente intenta reportar sobre una sede que no es suya).
  Future<ServiceTicket> create({
    required String clientLocationId,
    String? assetId,
    required String description,
    String? priority,
  }) async {
    try {
      final response = await _client.dio.post(
        '/tickets',
        data: {
          'clientLocationId': clientLocationId,
          'assetId': ?assetId,
          'description': description,
          'priority': ?priority,
        },
      );
      return ServiceTicket.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TicketApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
