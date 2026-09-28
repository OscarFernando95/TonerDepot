import 'package:flutter/foundation.dart';

import '../models/contract_asset.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/contractAssets.ts — vive en un controller
/// aparte (ContractAssetsController), no dentro de ContractsController.
/// Solo StaffRoles, sin acceso de Cliente.
class ContractAssetApi {
  ContractAssetApi(this._client);
  final ApiClient _client;

  Future<List<ContractAsset>> list(String contractId) async {
    try {
      final response = await _client.dio.get('/contracts/$contractId/assets');
      final page = PagedResult<ContractAsset>.fromJson(
        response.data as Map<String, dynamic>,
        ContractAsset.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('ContractAssetApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// 409 si `clientLocationId` no pertenece al cliente del contrato, o si el
  /// activo ya tiene un vínculo activo en cualquier contrato, o si el activo
  /// no estaba en EnBodega (queda en PendienteInstalacion como side-effect).
  Future<ContractAsset> add(
    String contractId, {
    required String assetId,
    required String clientLocationId,
    String? startDate,
  }) async {
    try {
      final response = await _client.dio.post(
        '/contracts/$contractId/assets',
        data: {
          'assetId': assetId,
          'clientLocationId': clientLocationId,
          'startDate': ?startDate,
        },
      );
      return ContractAsset.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ContractAssetApi.add failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// No es un DELETE — cierra la vigencia (endDate = ahora), idempotente.
  Future<void> endAssociation(String contractId, String contractAssetId) async {
    try {
      await _client.dio.patch(
        '/contracts/$contractId/assets/$contractAssetId/end',
      );
    } catch (e, st) {
      debugPrint('ContractAssetApi.endAssociation failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
