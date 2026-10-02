import 'package:flutter/foundation.dart';

import '../models/paged_result.dart';
import '../models/technician.dart';
import '../models/technician_asset.dart';
import '../models/technician_schedule.dart';
import '../models/technician_visit.dart';
import '../models/zone.dart';
import 'api_client.dart';

/// Gestión de técnicos y sus zonas — solo Staff. Distinto de
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

  Future<List<TechnicianZone>> getZones(String technicianId) async {
    try {
      final response = await _client.dio.get('/technicians/$technicianId/zones');
      return _parseZones(response.data);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.getZones failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Reemplaza el conjunto completo de zonas del técnico.
  Future<List<TechnicianZone>> setZones(
    String technicianId,
    List<String> zoneIds,
  ) async {
    try {
      final response = await _client.dio.put(
        '/technicians/$technicianId/zones',
        data: {'zoneIds': zoneIds},
      );
      return _parseZones(response.data);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.setZones failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  List<TechnicianZone> _parseZones(dynamic data) => (data as List<dynamic>)
      .map((e) => TechnicianZone.fromJson(e as Map<String, dynamic>))
      .toList();

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

  Future<List<TechnicianAsset>> getLinkedAssets(String technicianId) async {
    try {
      final response = await _client.dio.get('/technicians/$technicianId/assets');
      final items = response.data as List<dynamic>;
      return items.map((e) => TechnicianAsset.fromJson(e as Map<String, dynamic>)).toList();
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.getLinkedAssets failed: $e\n$st');
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

  Future<TechnicianAsset> linkAsset(String technicianId, String assetId) async {
    try {
      final response = await _client.dio.post('/technicians/$technicianId/assets', data: {'assetId': assetId});
      return TechnicianAsset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.linkAsset failed: $e\n$st');
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

  Future<void> unlinkAsset(String technicianId, String technicianAssetId) async {
    try {
      await _client.dio.delete('/technicians/$technicianId/assets/$technicianAssetId');
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.unlinkAsset failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
