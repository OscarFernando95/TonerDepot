import 'package:flutter/foundation.dart';

import '../models/inventory.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Inventario en el campo (backend: VisitInventoryController, /api/inventory). Staff y técnicos; el servidor
/// limita al técnico a las máquinas que tiene vinculadas.
class InventoryApi {
  InventoryApi(this._client);
  final ApiClient _client;

  /// Kit base del modelo del equipo con el saldo de su zona. Sin [assetId] el kit viene vacío; si además
  /// hay [clientLocationId] (ticket de equipo no catalogado), el saldo se resuelve por la zona de esa
  /// sede en vez de caer directo a la bodega principal.
  Future<VisitKit> getKit(String? assetId, {String? clientLocationId}) async {
    try {
      final response = await _client.dio.get(
        '/inventory/kit',
        queryParameters: {
          'assetId': ?assetId,
          'clientLocationId': ?clientLocationId,
        },
      );
      return VisitKit.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('InventoryApi.getKit failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Ítems activos con el saldo de la zona del equipo (o, sin equipo catalogado, de la zona de
  /// [clientLocationId] — ver [getKit]) (paginación offset).
  Future<PagedResult<PartOption>> searchParts({
    String? assetId,
    String? clientLocationId,
    String? search,
    String? category,
    int page = 1,
    int pageSize = 30,
  }) async {
    try {
      final trimmed = search?.trim();
      final response = await _client.dio.get(
        '/inventory/parts',
        queryParameters: {
          'assetId': ?assetId,
          'clientLocationId': ?clientLocationId,
          if (trimmed != null && trimmed.isNotEmpty) 'search': trimmed,
          'category': ?category,
          'page': page,
          'pageSize': pageSize,
        },
      );
      return PagedResult<PartOption>.fromJson(
        response.data as Map<String, dynamic>,
        PartOption.fromJson,
      );
    } catch (e, st) {
      debugPrint('InventoryApi.searchParts failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TonerEntry> registerToner({
    required String assetId,
    required String itemId,
    required int quantity,
    required bool deliveredToUser,
    DateTime? occurredAt,
    int? counterValue,
    String? notes,
  }) async {
    try {
      final response = await _client.dio.post(
        '/inventory/toner',
        data: {
          'assetId': assetId,
          'itemId': itemId,
          'quantity': quantity,
          'occurredAt': occurredAt?.toUtc().toIso8601String(),
          'deliveredToUser': deliveredToUser,
          'counterValue': counterValue,
          'notes': notes,
        },
      );
      return TonerEntry.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('InventoryApi.registerToner failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Historial de tóner de una máquina, más reciente primero (cursor/keyset).
  Future<PagedResult<TonerEntry>> listToner(
    String assetId, {
    String? cursor,
    int pageSize = 20,
  }) async {
    try {
      final response = await _client.dio.get(
        '/inventory/toner/assets/$assetId',
        queryParameters: {'cursor': ?cursor, 'pageSize': pageSize},
      );
      return PagedResult<TonerEntry>.fromJson(
        response.data as Map<String, dynamic>,
        TonerEntry.fromJson,
      );
    } catch (e, st) {
      debugPrint('InventoryApi.listToner failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
