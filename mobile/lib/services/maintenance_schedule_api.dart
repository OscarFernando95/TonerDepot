import 'package:flutter/foundation.dart';

import '../models/maintenance_order.dart';
import '../models/maintenance_schedule.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/maintenanceSchedules.ts. Staff únicamente;
/// `evaluateNow` además exige Administrador (el backend lo hace cumplir).
class MaintenanceScheduleApi {
  MaintenanceScheduleApi(this._client);
  final ApiClient _client;

  Future<PagedResult<MaintenanceSchedule>> list({
    required int page,
    int pageSize = 50,
  }) async {
    try {
      final response = await _client.dio.get(
        '/maintenance-schedules',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return PagedResult<MaintenanceSchedule>.fromJson(
        response.data as Map<String, dynamic>,
        MaintenanceSchedule.fromJson,
      );
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<MaintenanceSchedule> getById(String id) async {
    try {
      final response = await _client.dio.get('/maintenance-schedules/$id');
      return MaintenanceSchedule.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<MaintenanceSchedule> setStatus(String id, bool isActive) async {
    try {
      final response = await _client.dio.patch(
        '/maintenance-schedules/$id/status',
        data: {'isActive': isActive},
      );
      return MaintenanceSchedule.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Órdenes generadas por este cronograma. Es paginación por cursor; para el
  /// detalle solo mostramos la primera página (las más recientes).
  Future<List<MaintenanceOrder>> orders(String id, {int pageSize = 20}) async {
    try {
      final response = await _client.dio.get(
        '/maintenance-schedules/$id/orders',
        queryParameters: {'pageSize': pageSize},
      );
      return PagedResult<MaintenanceOrder>.fromJson(
        response.data as Map<String, dynamic>,
        MaintenanceOrder.fromJson,
      ).items;
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.orders failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<int> backfill() async {
    try {
      final response = await _client.dio.post(
        '/maintenance-schedules/backfill',
      );
      return (response.data as Map<String, dynamic>)['created'] as int;
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.backfill failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<int> evaluateNow() async {
    try {
      final response = await _client.dio.post(
        '/maintenance-schedules/evaluate-now',
      );
      return (response.data as Map<String, dynamic>)['ordersCreated'] as int;
    } catch (e, st) {
      debugPrint('MaintenanceScheduleApi.evaluateNow failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
