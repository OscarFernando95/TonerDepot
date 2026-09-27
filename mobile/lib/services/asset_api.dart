import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/asset_status_log.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/assets.ts. GET /assets no tiene filtros
/// server-side (ni por marca, ciudad, cliente ni estado) — el catálogo de
/// Staff filtra 100% client-side sobre las páginas ya cargadas, por eso
/// `listCatalog` sí pagina de verdad (page/pageSize explícitos) en vez de
/// confiar en el default como hace listMine() para el portal Cliente.
class AssetApi {
  AssetApi(this._client);
  final ApiClient _client;

  Future<List<Asset>> listMine() async {
    try {
      final response = await _client.dio.get('/assets');
      final page = PagedResult<Asset>.fromJson(response.data as Map<String, dynamic>, Asset.fromJson);
      return page.items;
    } catch (e, st) {
      debugPrint('AssetApi.listMine failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<PagedResult<Asset>> listCatalog({required int page, int pageSize = 50}) async {
    try {
      final response = await _client.dio.get('/assets', queryParameters: {'page': page, 'pageSize': pageSize});
      return PagedResult<Asset>.fromJson(response.data as Map<String, dynamic>, Asset.fromJson);
    } catch (e, st) {
      debugPrint('AssetApi.listCatalog failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Asset> getById(String id) async {
    try {
      final response = await _client.dio.get('/assets/$id');
      return Asset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Se crea referenciando un AssetModel ya existente — no hay marca/modelo
  /// libres. Arranca siempre en EnBodega.
  Future<Asset> create({required String assetModelId, required String serialNumber, required String type}) async {
    try {
      final response = await _client.dio.post('/assets', data: {
        'assetModelId': assetModelId,
        'serialNumber': serialNumber,
        'type': type,
      });
      return Asset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Asset> update(String id, {required String assetModelId, required String serialNumber, required String type}) async {
    try {
      final response = await _client.dio.put('/assets/$id', data: {
        'assetModelId': assetModelId,
        'serialNumber': serialNumber,
        'type': type,
      });
      return Asset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// `clientLocationId`+`area` obligatorios para pasar a Instalado (`area`
  /// se limpia solo al salir de Instalado); `clientLocationId` también
  /// obligatorio para PendienteInstalacion. Transiciones válidas en
  /// Asset.allowedTransitions.
  Future<Asset> setStatus(
    String id, {
    required String newStatus,
    String? clientLocationId,
    String? area,
    String? notes,
  }) async {
    try {
      final response = await _client.dio.post('/assets/$id/status', data: {
        'newStatus': newStatus,
        'clientLocationId': ?clientLocationId,
        'area': ?area,
        'notes': ?notes,
      });
      return Asset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('AssetApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<AssetStatusLog>> getStatusHistory(String id) async {
    try {
      final response = await _client.dio.get('/assets/$id/status-history');
      final page = PagedResult<AssetStatusLog>.fromJson(response.data as Map<String, dynamic>, AssetStatusLog.fromJson);
      return page.items;
    } catch (e, st) {
      debugPrint('AssetApi.getStatusHistory failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
