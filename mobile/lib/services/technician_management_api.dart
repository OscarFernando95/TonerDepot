import 'package:flutter/foundation.dart';

import '../models/paged_result.dart';
import '../models/technician.dart';
import '../models/technician_coverage.dart';
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
      final page = PagedResult<Technician>.fromJson(response.data as Map<String, dynamic>, Technician.fromJson);
      return page.items;
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<TechnicianCoverage>> getCoverage(String technicianId) async {
    try {
      final response = await _client.dio.get('/technicians/$technicianId/coverage');
      final items = response.data as List<dynamic>;
      return items.map((e) => TechnicianCoverage.fromJson(e as Map<String, dynamic>)).toList();
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.getCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianCoverage> addCoverage(String technicianId, String cityId) async {
    try {
      final response = await _client.dio.post('/technicians/$technicianId/coverage', data: {'cityId': cityId});
      return TechnicianCoverage.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.addCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<void> removeCoverage(String technicianId, String coverageId) async {
    try {
      await _client.dio.delete('/technicians/$technicianId/coverage/$coverageId');
    } catch (e, st) {
      debugPrint('TechnicianManagementApi.removeCoverage failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
