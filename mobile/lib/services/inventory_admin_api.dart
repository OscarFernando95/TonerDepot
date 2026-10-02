import 'package:flutter/foundation.dart';

import '../models/inventory_admin.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Gestión del inventario de la empresa (backend: InventoryController, /api/inventory). Solo Staff; la sede
/// principal solo la edita el Administrador. Separada de `InventoryApi` (kit/piezas/tóner de campo).
class InventoryAdminApi {
  InventoryAdminApi(this._client);
  final ApiClient _client;

  Future<T> _guard<T>(String op, Future<T> Function() action) async {
    try {
      return await action();
    } catch (e, st) {
      debugPrint('InventoryAdminApi.$op failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<PagedResult<InventoryItem>> listItems({
    String? search,
    String? category,
    bool activeOnly = false,
    int page = 1,
    int pageSize = 20,
  }) => _guard('listItems', () async {
    final response = await _client.dio.get(
      '/inventory/items',
      queryParameters: buildItemsQuery(
        search: search,
        category: category,
        activeOnly: activeOnly,
        page: page,
        pageSize: pageSize,
      ),
    );
    return PagedResult<InventoryItem>.fromJson(
      response.data as Map<String, dynamic>,
      InventoryItem.fromJson,
    );
  });

  Future<InventoryItem> createItem(ItemDraft draft) =>
      _guard('createItem', () async {
        final response = await _client.dio.post(
          '/inventory/items',
          data: draft.toJson(editing: false),
        );
        return InventoryItem.fromJson(response.data as Map<String, dynamic>);
      });

  Future<InventoryItem> updateItem(String id, ItemDraft draft) =>
      _guard('updateItem', () async {
        final response = await _client.dio.put(
          '/inventory/items/$id',
          data: draft.toJson(editing: true),
        );
        return InventoryItem.fromJson(response.data as Map<String, dynamic>);
      });

  Future<List<InventoryLocation>> listLocations() =>
      _guard('listLocations', () async {
        final response = await _client.dio.get('/inventory/locations');
        return (response.data as List<dynamic>)
            .map((e) => InventoryLocation.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<InventoryLocation> updateMainLocation(MainLocationDraft draft) =>
      _guard('updateMainLocation', () async {
        final response = await _client.dio.put(
          '/inventory/locations/main',
          data: draft.toJson(),
        );
        return InventoryLocation.fromJson(
          response.data as Map<String, dynamic>,
        );
      });

  Future<PagedResult<StockRow>> listStock({
    String? locationId,
    String? category,
    bool onlyLow = false,
    int page = 1,
    int pageSize = 20,
  }) => _guard('listStock', () async {
    final response = await _client.dio.get(
      '/inventory/stock',
      queryParameters: buildStockQuery(
        locationId: locationId,
        category: category,
        onlyLow: onlyLow,
        page: page,
        pageSize: pageSize,
      ),
    );
    return PagedResult<StockRow>.fromJson(
      response.data as Map<String, dynamic>,
      StockRow.fromJson,
    );
  });

  /// Movimientos, más reciente primero (cursor/keyset).
  Future<PagedResult<InventoryMovement>> listMovements({
    String? locationId,
    String? cursor,
    int pageSize = 25,
  }) => _guard('listMovements', () async {
    final response = await _client.dio.get(
      '/inventory/movements',
      queryParameters: buildMovementsQuery(
        locationId: locationId,
        cursor: cursor,
        pageSize: pageSize,
      ),
    );
    return PagedResult<InventoryMovement>.fromJson(
      response.data as Map<String, dynamic>,
      InventoryMovement.fromJson,
    );
  });

  /// Entrada, traspaso o ajuste según `draft.kind`. El servidor responde 409 "Saldo insuficiente" si un traspaso
  /// supera el saldo de origen (llega traducido en el mensaje de la [ApiException]).
  Future<void> registerOperation(OperationDraft draft) =>
      _guard('registerOperation', () async {
        final path = switch (draft.kind) {
          InventoryOperation.entry => '/inventory/entries',
          InventoryOperation.transfer => '/inventory/transfers',
          InventoryOperation.adjust => '/inventory/adjustments',
        };
        await _client.dio.post(path, data: draft.toJson());
      });
}
