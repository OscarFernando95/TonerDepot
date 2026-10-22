import 'package:flutter/foundation.dart';

import '../models/assignment_history.dart';
import '../models/maintenance_order.dart';
import '../models/maintenance_order_comment.dart';
import '../models/maintenance_thread_text.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Un solo endpoint para Tecnico y Staff: el backend filtra internamente por
/// RequestingUser (Tecnico solo ve las suyas, Staff las ve todas) — no hay
/// parámetro de alcance que mandar desde el cliente.
class MaintenanceOrderApi {
  MaintenanceOrderApi(this._client);
  final ApiClient _client;

  Future<List<MaintenanceOrder>> list() async {
    try {
      final response = await _client.dio.get('/maintenance-orders');
      final page = PagedResult<MaintenanceOrder>.fromJson(
        response.data as Map<String, dynamic>,
        MaintenanceOrder.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente. Mantenimiento a demanda de un activo instalado, fuera de los umbrales del cronograma.
  Future<MaintenanceOrder> createManual({
    required String assetId,
    required bool includesGeneral,
    required bool includesUnits,
    required bool includesConsumables,
    String? reason,
  }) async {
    try {
      final response = await _client.dio.post(
        '/maintenance-orders',
        data: {
          'assetId': assetId,
          'includesGeneral': includesGeneral,
          'includesUnits': includesUnits,
          'includesConsumables': includesConsumables,
          'reason': ?reason,
        },
      );
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.createManual failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<MaintenanceOrder> getById(String id) async {
    try {
      final response = await _client.dio.get('/maintenance-orders/$id');
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente. Válido solo si la orden está Pendiente o Asignada.
  Future<MaintenanceOrder> assign(
    String id, {
    required String technicianId,
    String? reason,
  }) async {
    try {
      final response = await _client.dio.post(
        '/maintenance-orders/$id/assign',
        data: {'technicianId': technicianId, 'reason': ?reason},
      );
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.assign failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente. `counterValue` debe ser >= la última lectura registrada.
  Future<MaintenanceOrder> complete(
    String id, {
    required int counterValue,
    DateTime? readingDate,
  }) async {
    try {
      final response = await _client.dio.post(
        '/maintenance-orders/$id/complete',
        data: {
          'counterValue': counterValue,
          'readingDate': readingDate?.toUtc().toIso8601String(),
        },
      );
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.complete failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente.
  Future<MaintenanceOrder> cancel(String id) async {
    try {
      final response = await _client.dio.post('/maintenance-orders/$id/cancel');
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.cancel failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente (endpoint [Authorize(Roles = StaffRoles)] en el
  /// backend). Espejo de getMaintenanceOrderAssignmentHistory en
  /// frontend-web/src/api/maintenanceOrders.ts.
  Future<List<AssignmentHistory>> getAssignmentHistory(String id) async {
    try {
      final response = await _client.dio.get(
        '/maintenance-orders/$id/assignment-history',
      );
      final page = PagedResult<AssignmentHistory>.fromJson(
        response.data as Map<String, dynamic>,
        AssignmentHistory.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.getAssignmentHistory failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Hilo compartido de la orden (cliente dueño, técnico asignado y staff), en orden cronológico.
  Future<List<MaintenanceOrderComment>> listComments(String id) async {
    try {
      final response = await _client.dio.get(
        '/maintenance-orders/$id/comments',
      );
      final data = response.data;
      // Tolera tanto la lista suelta como el envelope paginado.
      final raw = data is List
          ? data
          : (data as Map<String, dynamic>)['items'] as List;
      return [
        for (final item in raw)
          MaintenanceOrderComment.fromJson(item as Map<String, dynamic>),
      ];
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.listComments failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Agrega un comentario al hilo (1..1000 caracteres). Si la orden ya cerró, el servidor responde 409.
  Future<MaintenanceOrderComment> addComment(String id, String body) async {
    try {
      final response = await _client.dio.post(
        '/maintenance-orders/$id/comments',
        data: {'body': body.trim()},
      );
      return MaintenanceOrderComment.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.addComment failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Técnico asignado únicamente (endpoint propio del técnico): programa o reprograma cuándo hará el
  /// mantenimiento. [plannedAt] se envía como ISO UTC; el servidor exige orden Asignada y fecha futura a <= 365 días.
  Future<MaintenanceOrder> schedule(String id, DateTime plannedAt) async {
    try {
      final response = await _client.dio.post(
        '/technicians/me/maintenance-orders/$id/schedule',
        data: {'plannedAt': plannedAtToUtcIso(plannedAt)},
      );
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.schedule failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
