import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/contracts.ts. GET /contracts no tiene
/// filtros server-side — igual que Activos, listCatalog pagina de verdad
/// (page/pageSize explícitos). Solo Staff: el rol Cliente recibe 403.
class ContractApi {
  ContractApi(this._client);
  final ApiClient _client;

  Future<PagedResult<Contract>> listCatalog({
    required int page,
    int pageSize = 50,
  }) async {
    try {
      final response = await _client.dio.get(
        '/contracts',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return PagedResult<Contract>.fromJson(
        response.data as Map<String, dynamic>,
        Contract.fromJson,
      );
    } catch (e, st) {
      debugPrint('ContractApi.listCatalog failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Contract> getById(String id) async {
    try {
      final response = await _client.dio.get('/contracts/$id');
      return Contract.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ContractApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// `clientId` solo se manda al crear — no es editable después (inmutable
  /// tras creación, ver comentario en UpdateContractRequest del backend).
  Future<Contract> create({
    required String clientId,
    required String startDate,
    String? endDate,
    int? includedPrintsPerMonth,
    double? pricePerExtraPage,
    String? notes,
  }) async {
    try {
      final response = await _client.dio.post(
        '/contracts',
        data: {
          'clientId': clientId,
          'startDate': startDate,
          'endDate': ?endDate,
          'includedPrintsPerMonth': ?includedPrintsPerMonth,
          'pricePerExtraPage': ?pricePerExtraPage,
          'notes': ?notes,
        },
      );
      return Contract.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ContractApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Contract> update(
    String id, {
    required String startDate,
    String? endDate,
    int? includedPrintsPerMonth,
    double? pricePerExtraPage,
    String? notes,
  }) async {
    try {
      final response = await _client.dio.put(
        '/contracts/$id',
        data: {
          'startDate': startDate,
          'endDate': ?endDate,
          'includedPrintsPerMonth': ?includedPrintsPerMonth,
          'pricePerExtraPage': ?pricePerExtraPage,
          'notes': ?notes,
        },
      );
      return Contract.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ContractApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Manual, sin transiciones restringidas — el backend acepta cualquier
  /// valor del enum sin importar el estado actual.
  Future<Contract> setStatus(String id, String status) async {
    try {
      final response = await _client.dio.patch(
        '/contracts/$id/status',
        data: {'status': status},
      );
      return Contract.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ContractApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
