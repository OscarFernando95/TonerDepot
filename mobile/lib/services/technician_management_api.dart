import 'package:flutter/foundation.dart';

import '../models/paged_result.dart';
import '../models/technician.dart';
import '../models/technician_coverage.dart';
import '../models/technician_schedule.dart';
import '../models/technician_visit.dart';
import 'api_client.dart';

/// Gestión de técnicos y su cobertura geográfica — solo Staff. Distinto de
/// technician_api.dart, que es el self-service del propio técnico
/// (`/technicians/me/*`). Este pega a `/technicians/{id}/...` de gestión.
class TechnicianManagementApi {
  TechnicianManagementApi(this._client);
  final ApiClient _client;

  Future<List<Technician>> list() async {
    try {
      final response = await _client.dio.get('/technicians');
      final page = PagedResult<Technician>.fromJson(
        response.data as Map<String, dynamic>,
        Technician.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<TechnicianCoverage>> getCoverage(String technicianId) async {
    try {
      final response = await _client.dio.get(
        '/technicians/$technicianId/coverage',
      );
      final items = response.data as List<dynamic>;
      return items
          .map((e) => TechnicianCoverage.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.getCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianCoverage> addCoverage(
    String technicianId,
    String cityId,
  ) async {
    try {
      final response = await _client.dio.post(
        '/technicians/$technicianId/coverage',
        data: {'cityId': cityId},
      );
      return TechnicianCoverage.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.addCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<void> removeCoverage(String technicianId, String coverageId) async {
    try {
      await _client.dio.delete(
        '/technicians/$technicianId/coverage/$coverageId',
      );
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.removeCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSchedule> getSchedule(String technicianId) async {
    try {
      final response = await _client.dio.get(
        '/technicians/$technicianId/schedule',
      );
      return TechnicianSchedule.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.getSchedule failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSchedule> setSchedule(
    String technicianId,
    List<WorkInterval> intervals,
  ) async {
    try {
      final response = await _client.dio.put(
        '/technicians/$technicianId/schedule',
        data: {
          'intervals': [for (final i in intervals) i.toJson()],
        },
      );
      return TechnicianSchedule.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.setSchedule failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Borra el horario propio: vuelve a aplicar el de la empresa.
  Future<TechnicianSchedule> resetSchedule(String technicianId) async {
    try {
      final response = await _client.dio.delete(
        '/technicians/$technicianId/schedule',
      );
      return TechnicianSchedule.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.resetSchedule failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<TimeOff>> listTimeOff(String technicianId) async {
    try {
      final response = await _client.dio.get(
        '/technicians/$technicianId/time-off',
      );
      return (response.data as List<dynamic>)
          .map((e) => TimeOff.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.listTimeOff failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TimeOff> addTimeOff(
    String technicianId, {
    required DateTime startsAt,
    required DateTime endsAt,
    String? reason,
  }) async {
    try {
      final response = await _client.dio.post(
        '/technicians/$technicianId/time-off',
        data: {
          'startsAt': startsAt.toUtc().toIso8601String(),
          'endsAt': endsAt.toUtc().toIso8601String(),
          'reason': ?reason,
        },
      );
      return TimeOff.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.addTimeOff failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TimeOff> cancelTimeOff(String technicianId, String timeOffId) async {
    try {
      final response = await _client.dio.post(
        '/technicians/$technicianId/time-off/$timeOffId/cancel',
      );
      return TimeOff.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.cancelTimeOff failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<TechnicianVisit>> listVisits(String technicianId) async {
    try {
      final response = await _client.dio.get('/technicians/$technicianId/time-logs');
      final page = PagedResult<TechnicianVisit>.fromJson(
        response.data as Map<String, dynamic>,
        TechnicianVisit.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.listVisits failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
