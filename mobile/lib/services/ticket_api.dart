import 'package:flutter/foundation.dart';

import '../models/assignment_history.dart';
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

  /// Staff únicamente (RoleNames.StaffRoles en el backend).
  Future<ServiceTicket> assign(
    String id, {
    required String technicianId,
    String? reason,
  }) async {
    try {
      final response = await _client.dio.post(
        '/tickets/$id/assign',
        data: {'technicianId': technicianId, 'reason': ?reason},
      );
      return ServiceTicket.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TicketApi.assign failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente. `status` debe ser una transición válida desde el
  /// estado actual (ver TicketAllowedTransitions en status_labels.dart).
  Future<ServiceTicket> setStatus(String id, String status) async {
    try {
      final response = await _client.dio.patch(
        '/tickets/$id/status',
        data: {'status': status},
      );
      return ServiceTicket.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TicketApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<AssignmentHistory>> getAssignmentHistory(String id) async {
    try {
      final response = await _client.dio.get('/tickets/$id/assignment-history');
      final page = PagedResult<AssignmentHistory>.fromJson(
        response.data as Map<String, dynamic>,
        AssignmentHistory.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('TicketApi.getAssignmentHistory failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
