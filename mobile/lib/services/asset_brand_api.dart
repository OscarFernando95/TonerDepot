import 'package:flutter/foundation.dart';

import '../models/asset_brand.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/assetBrands.ts. Solo List + Create — el
/// backend no tiene PUT/DELETE de marca. Array plano, no PagedResult.
class AssetBrandApi {
  AssetBrandApi(this._client);
  final ApiClient _client;

  Future<List<AssetBrand>> list() async {
    try {
      final response = await _client.dio.get('/asset-brands');
      final items = response.data as List<dynamic>;
      return items.map((e) => AssetBrand.fromJson(e as Map<String, dynamic>)).toList();
    } catch (e, st) {
      debugPrint('AssetBrandApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<AssetBrand> create(String name) async {
    try {
      final response = await _client.dio.post('/asset-brands', data: {'name': name});
      return AssetBrand.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetBrandApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
