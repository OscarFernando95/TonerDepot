import 'package:flutter/foundation.dart';

import '../models/city.dart';
import '../models/inventory_admin.dart';
import '../services/api_client.dart';
import '../services/city_api.dart';
import '../services/inventory_admin_api.dart';
import '../services/realtime_service.dart';

/// Inventario de la empresa (espejo de InventoryView.vue): existencias (offset), movimientos (cursor), catálogo
/// (offset) y ubicaciones. Los eventos en tiempo real de Inventory/Zone recargan todo en silencio.
class InventoryState extends ChangeNotifier {
  InventoryState(ApiClient client, {InventoryAdminApi? api, CityApi? cityApi})
    : _api = api ?? InventoryAdminApi(client),
      _cityApi = cityApi ?? CityApi(client) {
    _unsubscribe = RealtimeService.instance.subscribe(['Inventory', 'Zone'], (
      _,
    ) {
      refreshSilently();
    });
  }

  static const stockPageSize = 20;
  static const movementsPageSize = 25;
  static const itemsPageSize = 20;

  final InventoryAdminApi _api;
  final CityApi _cityApi;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;

  @override
  void dispose() {
    _disposed = true;
    _unsubscribe();
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  /// Mutación en curso (guardar ítem / operación / sede principal).
  bool busy = false;

  // ── Ubicaciones ───────────────────────────────────────────────────────────────────────────────
  List<InventoryLocation> locations = [];
  String? locationsError;
  List<City> cities = [];
  bool _citiesLoaded = false;

  InventoryLocation? get mainLocation {
    for (final l in locations) {
      if (l.isMain) return l;
    }
    return null;
  }

  List<InventoryLocation> get zoneLocations => [
    for (final l in locations)
      if (l.isZone) l,
  ];

  Future<void> loadLocations({bool silent = false}) async {
    try {
      locations = await _api.listLocations();
      locationsError = null;
      if (!_citiesLoaded && !silent) await loadCities();
    } catch (e, st) {
      debugPrint('InventoryState.loadLocations failed: $e\n$st');
      if (!silent) {
        locationsError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las ubicaciones.';
      }
    } finally {
      notifyListeners();
    }
  }

  /// Catálogo de municipios, una sola vez (para la sede principal).
  Future<void> loadCities() async {
    if (_citiesLoaded) return;
    try {
      cities = await _cityApi.list();
      _citiesLoaded = true;
    } catch (e, st) {
      debugPrint('InventoryState.loadCities failed: $e\n$st');
    }
  }

  // ── Existencias ───────────────────────────────────────────────────────────────────────────────
  List<StockRow> stock = [];
  bool stockLoading = false;
  bool stockLoadingMore = false;
  bool stockHasMore = false;
  String? stockError;
  String? stockLocationId;
  String? stockCategory;
  bool stockOnlyLow = false;
  int _stockPage = 1;
  int _stockSize = stockPageSize;
  int _stockReq = 0;

  void setStockFilter({
    Object? locationId = _keep,
    Object? category = _keep,
    bool? onlyLow,
  }) {
    if (locationId != _keep) stockLocationId = locationId as String?;
    if (category != _keep) stockCategory = category as String?;
    if (onlyLow != null) stockOnlyLow = onlyLow;
    loadStock();
  }

  Future<void> loadStock({bool silent = false, bool more = false}) async {
    final req = ++_stockReq;
    final int page;
    final int size;
    if (more) {
      page = _stockPage + 1;
      size = _stockSize;
      stockLoadingMore = true;
    } else if (silent) {
      page = 1;
      size = silentReloadPageSize(stock.length, stockPageSize);
    } else {
      page = 1;
      size = stockPageSize;
      stockLoading = true;
      stockError = null;
    }
    notifyListeners();
    try {
      final result = await _api.listStock(
        locationId: stockLocationId,
        category: stockCategory,
        onlyLow: stockOnlyLow,
        page: page,
        pageSize: size,
      );
      if (req != _stockReq) return;
      stock = more
          ? mergePage(stock, result.items, (r) => r.key)
          : result.items;
      stockHasMore = result.hasMore;
      _stockPage = page;
      _stockSize = size;
      stockError = null;
    } catch (e, st) {
      debugPrint('InventoryState.loadStock failed: $e\n$st');
      if (req != _stockReq) return;
      if (!silent) {
        stockError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las existencias.';
      }
    } finally {
      if (req == _stockReq) {
        stockLoading = false;
        stockLoadingMore = false;
        notifyListeners();
      }
    }
  }

  // ── Movimientos ───────────────────────────────────────────────────────────────────────────────
  List<InventoryMovement> movements = [];
  bool movementsLoading = false;
  bool movementsLoadingMore = false;
  String? movementsError;
  String? movementsLocationId;
  String? _movementsCursor;
  int _movementsReq = 0;

  bool get movementsHasMore => _movementsCursor != null;

  void setMovementsLocation(String? locationId) {
    movementsLocationId = locationId;
    loadMovements();
  }

  Future<void> loadMovements({bool silent = false, bool more = false}) async {
    if (more && _movementsCursor == null) return;
    final req = ++_movementsReq;
    final int size;
    if (more) {
      size = movementsPageSize;
      movementsLoadingMore = true;
    } else if (silent) {
      size = silentReloadPageSize(movements.length, movementsPageSize);
    } else {
      size = movementsPageSize;
      movementsLoading = true;
      movementsError = null;
    }
    notifyListeners();
    try {
      final result = await _api.listMovements(
        locationId: movementsLocationId,
        cursor: more ? _movementsCursor : null,
        pageSize: size,
      );
      if (req != _movementsReq) return;
      movements = more
          ? mergePage(movements, result.items, (m) => m.id)
          : result.items;
      _movementsCursor = nextCursorOf(
        hasMore: result.hasMore,
        nextCursor: result.nextCursor,
      );
      movementsError = null;
    } catch (e, st) {
      debugPrint('InventoryState.loadMovements failed: $e\n$st');
      if (req != _movementsReq) return;
      if (!silent) {
        movementsError = e is ApiException
            ? e.message
            : 'No se pudieron cargar los movimientos.';
      }
    } finally {
      if (req == _movementsReq) {
        movementsLoading = false;
        movementsLoadingMore = false;
        notifyListeners();
      }
    }
  }

  // ── Catálogo ──────────────────────────────────────────────────────────────────────────────────
  List<InventoryItem> items = [];
  bool itemsLoading = false;
  bool itemsLoadingMore = false;
  bool itemsHasMore = false;
  String? itemsError;
  String itemSearch = '';
  String? itemCategory;
  int _itemsPage = 1;
  int _itemsSize = itemsPageSize;
  int _itemsReq = 0;

  void setItemFilter({String? search, Object? category = _keep}) {
    if (search != null) itemSearch = search;
    if (category != _keep) itemCategory = category as String?;
    loadItems();
  }

  Future<void> loadItems({bool silent = false, bool more = false}) async {
    final req = ++_itemsReq;
    final int page;
    final int size;
    if (more) {
      page = _itemsPage + 1;
      size = _itemsSize;
      itemsLoadingMore = true;
    } else if (silent) {
      page = 1;
      size = silentReloadPageSize(items.length, itemsPageSize);
    } else {
      page = 1;
      size = itemsPageSize;
      itemsLoading = true;
      itemsError = null;
    }
    notifyListeners();
    try {
      final result = await _api.listItems(
        search: itemSearch,
        category: itemCategory,
        page: page,
        pageSize: size,
      );
      if (req != _itemsReq) return;
      items = more ? mergePage(items, result.items, (i) => i.id) : result.items;
      itemsHasMore = result.hasMore;
      _itemsPage = page;
      _itemsSize = size;
      itemsError = null;
    } catch (e, st) {
      debugPrint('InventoryState.loadItems failed: $e\n$st');
      if (req != _itemsReq) return;
      if (!silent) {
        itemsError = e is ApiException
            ? e.message
            : 'No se pudo cargar el catálogo.';
      }
    } finally {
      if (req == _itemsReq) {
        itemsLoading = false;
        itemsLoadingMore = false;
        notifyListeners();
      }
    }
  }

  /// Buscador del selector de ítem de las operaciones: solo activos, 20 resultados.
  Future<List<InventoryItem>> searchActiveItems(String query) async {
    final result = await _api.listItems(
      search: query,
      activeOnly: true,
      pageSize: 20,
    );
    return result.items;
  }

  // ── Carga conjunta ────────────────────────────────────────────────────────────────────────────
  Future<void> loadAll() async {
    await loadLocations();
    await Future.wait([loadStock(), loadMovements(), loadItems()]);
  }

  Future<void> refreshSilently() async {
    await Future.wait([
      loadLocations(silent: true),
      loadStock(silent: true),
      loadMovements(silent: true),
      loadItems(silent: true),
    ]);
  }

  // ── Mutaciones (devuelven null si salió bien, o el mensaje de error) ──────────────────────────
  Future<String?> saveItem(String? editingId, ItemDraft draft) {
    final invalid = draft.validate();
    if (invalid != null) return Future.value(invalid);
    return _run('saveItem', 'No se pudo guardar el ítem.', () async {
      if (editingId == null) {
        await _api.createItem(draft);
      } else {
        await _api.updateItem(editingId, draft);
      }
    }, then: () => loadItems(silent: true));
  }

  Future<String?> saveOperation(OperationDraft draft) {
    final invalid = draft.validate();
    if (invalid != null) return Future.value(invalid);
    return _run(
      'saveOperation',
      'No se pudo registrar el movimiento.',
      () => _api.registerOperation(draft),
      then: () =>
          Future.wait([loadStock(silent: true), loadMovements(silent: true)]),
    );
  }

  Future<String?> saveMainLocation(MainLocationDraft draft) {
    final invalid = draft.validate();
    if (invalid != null) return Future.value(invalid);
    return _run(
      'saveMainLocation',
      'No se pudo actualizar la sede principal.',
      () => _api.updateMainLocation(draft),
      then: () => loadLocations(silent: true),
    );
  }

  Future<String?> _run(
    String op,
    String fallback,
    Future<void> Function() action, {
    required Future<void> Function() then,
  }) async {
    busy = true;
    notifyListeners();
    try {
      await action();
    } catch (e, st) {
      debugPrint('InventoryState.$op failed: $e\n$st');
      busy = false;
      notifyListeners();
      return e is ApiException ? e.message : fallback;
    }
    try {
      await then();
    } catch (e, st) {
      // La mutación ya se aplicó; si la recarga falla el realtime/pull-to-refresh lo corrige.
      debugPrint('InventoryState.$op reload failed: $e\n$st');
    }
    busy = false;
    notifyListeners();
    return null;
  }
}

const Object _keep = Object();
