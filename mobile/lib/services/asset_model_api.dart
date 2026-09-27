import 'package:flutter/foundation.dart';

import '../models/asset_model.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/assetModels.ts. No hay ruta plana
/// `/asset-models` — siempre anidada bajo la marca. Solo Create + Update, sin
/// delete/activar-desactivar.
class AssetModelApi {
  AssetModelApi(this._client);
  final ApiClient _client;

  Future<List<AssetModel>> listForBrand(String brandId) async {
    try {
      final response = await _client.dio.get('/asset-brands/$brandId/models');
      final items = response.data as List<dynamic>;
      return items.map((e) => AssetModel.fromJson(e as Map<String, dynamic>)).toList();
    } catch (e, st) {
      debugPrint('AssetModelApi.listForBrand failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<AssetModel> create(
    String brandId, {
    required String name,
    required int generalPrintThreshold,
    required int generalMonthsInterval,
    required int unitsPrintThreshold,
    required int unitsMonthsInterval,
    required int consumablesPrintThreshold,
  }) async {
    try {
      final response = await _client.dio.post('/asset-brands/$brandId/models', data: {
        'name': name,
        'generalPrintThreshold': generalPrintThreshold,
        'generalMonthsInterval': generalMonthsInterval,
        'unitsPrintThreshold': unitsPrintThreshold,
        'unitsMonthsInterval': unitsMonthsInterval,
        'consumablesPrintThreshold': consumablesPrintThreshold,
      });
      return AssetModel.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetModelApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<AssetModel> update(
    String brandId,
    String id, {
    required String name,
    required int generalPrintThreshold,
    required int generalMonthsInterval,
    required int unitsPrintThreshold,
    required int unitsMonthsInterval,
    required int consumablesPrintThreshold,
  }) async {
    try {
      final response = await _client.dio.put('/asset-brands/$brandId/models/$id', data: {
        'name': name,
        'generalPrintThreshold': generalPrintThreshold,
        'generalMonthsInterval': generalMonthsInterval,
        'unitsPrintThreshold': unitsPrintThreshold,
        'unitsMonthsInterval': unitsMonthsInterval,
        'consumablesPrintThreshold': consumablesPrintThreshold,
      });
      return AssetModel.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetModelApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
