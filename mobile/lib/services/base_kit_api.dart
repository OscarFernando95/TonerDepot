import 'package:flutter/foundation.dart';

import '../models/base_kit.dart';
import 'api_client.dart';

/// Kit base de consumibles (frontend-web/src/api/inventory.ts: getBrandKit/setBrandKit/getModelKit/setModelKit) más
/// una búsqueda mínima en el catálogo de inventario para elegir ítems. Solo Staff.
class BaseKitApi {
  BaseKitApi(this._client);
  final ApiClient _client;

  Future<List<BaseKitItem>> getBrandKit(String brandId) async {
    try {
      final response = await _client.dio.get(
        '/asset-brands/$brandId/base-items',
      );
      return _items(response.data, BaseKitItem.fromJson);
    } catch (e, st) {
      debugPrint('BaseKitApi.getBrandKit failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<BaseKitItem>> setBrandKit(
    String brandId,
    List<BaseKitItem> items,
  ) async {
    try {
      final response = await _client.dio.put(
        '/asset-brands/$brandId/base-items',
        data: {'items': BaseKitLogic.buildBrandItems(items)},
      );
      return _items(response.data, BaseKitItem.fromJson);
    } catch (e, st) {
      debugPrint('BaseKitApi.setBrandKit failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<ModelKitItem>> getModelKit(String brandId, String modelId) async {
    try {
      final response = await _client.dio.get(
        '/asset-brands/$brandId/models/$modelId/base-items',
      );
      return _items(response.data, ModelKitItem.fromJson);
    } catch (e, st) {
      debugPrint('BaseKitApi.getModelKit failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// [overrides]: solo los ajustes (ver [BaseKitLogic.buildOverrides]).
  Future<List<ModelKitItem>> setModelKit(
    String brandId,
    String modelId,
    List<Map<String, dynamic>> overrides,
  ) async {
    try {
      final response = await _client.dio.put(
        '/asset-brands/$brandId/models/$modelId/base-items',
        data: {'overrides': overrides},
      );
      return _items(response.data, ModelKitItem.fromJson);
    } catch (e, st) {
      debugPrint('BaseKitApi.setModelKit failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// GET /inventory/items?search=&activeOnly=true&pageSize=20 (listado paginado: solo se usa `items`).
  Future<List<KitItemOption>> searchItems(String query) async {
    try {
      final response = await _client.dio.get(
        '/inventory/items',
        queryParameters: {
          if (query.trim().isNotEmpty) 'search': query.trim(),
          'activeOnly': true,
          'pageSize': 20,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return _items(data['items'], KitItemOption.fromJson);
    } catch (e, st) {
      debugPrint('BaseKitApi.searchItems failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  static List<T> _items<T>(
    Object? raw,
    T Function(Map<String, dynamic>) fromJson,
  ) => (raw as List<dynamic>? ?? [])
      .map((e) => fromJson(e as Map<String, dynamic>))
      .toList();
}
