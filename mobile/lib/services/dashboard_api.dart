import 'package:flutter/foundation.dart';

import '../models/dashboard_summary.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/dashboard.ts. Solo StaffRoles
/// (Administrador/Coordinador) — DashboardHomeScreen ya se encarga de no
/// llamar esto para ningún otro rol.
class DashboardApi {
  DashboardApi(this._client);
  final ApiClient _client;

  Future<DashboardSummary> getSummary(int periodDays) async {
    try {
      final response = await _client.dio.get('/dashboard/summary', queryParameters: {'days': periodDays});
      return DashboardSummary.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('DashboardApi.getSummary failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
