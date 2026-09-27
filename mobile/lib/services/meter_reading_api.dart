import 'package:flutter/foundation.dart';

import '../models/meter_reading_asset.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/meterReadings.ts. Módulo abierto a
/// Admin/Coordinador/Técnico (RoleNames.StaffAndTechnicianRoles en el
/// backend) — un técnico puede listar y registrar sin pasar por
/// TechnicianSelfServiceController.
class MeterReadingApi {
  MeterReadingApi(this._client);
  final ApiClient _client;

  Future<List<MeterReadingAsset>> listAssets() async {
    try {
      final response = await _client.dio.get('/meter-readings');
      final page = PagedResult<MeterReadingAsset>.fromJson(
        response.data as Map<String, dynamic>,
        MeterReadingAsset.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('MeterReadingApi.listAssets failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<void> register(String assetId, {required double counterValue, DateTime? readingDate}) async {
    try {
      await _client.dio.post('/meter-readings/$assetId', data: {
        'counterValue': counterValue,
        'readingDate': readingDate?.toUtc().toIso8601String(),
      });
    } catch (e, st) {
      debugPrint('MeterReadingApi.register failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
