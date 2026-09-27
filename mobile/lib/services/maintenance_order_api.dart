import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
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
  Future<MaintenanceOrder> assign(String id, {required String technicianId, String? reason}) async {
    try {
      final response = await _client.dio.post('/maintenance-orders/$id/assign', data: {
        'technicianId': technicianId,
        'reason': ?reason,
      });
      return MaintenanceOrder.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('MaintenanceOrderApi.assign failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Staff únicamente. `counterValue` debe ser >= la última lectura registrada.
  Future<MaintenanceOrder> complete(String id, {required int counterValue, DateTime? readingDate}) async {
    try {
      final response = await _client.dio.post('/maintenance-orders/$id/complete', data: {
        'counterValue': counterValue,
        'readingDate': readingDate?.toUtc().toIso8601String(),
      });
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
}
