import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../services/api_client.dart';
import '../services/asset_api.dart';

/// A diferencia del portal Cliente (listMine, una sola página confiando en
/// el default), este catálogo pagina de verdad — GET /assets no tiene
/// filtros server-side, así que con más de una página el filtro por estado
/// necesita ir cargando más antes de poder confiar en lo que se ve.
class AssetsListState extends ChangeNotifier {
  AssetsListState(ApiClient client) : _api = AssetApi(client);

  final AssetApi _api;
  static const _pageSize = 50;

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  String? error;
  List<Asset> assets = [];
  int _page = 1;
  String? statusFilter;

  List<Asset> get filtered =>
      statusFilter == null ? assets : assets.where((a) => a.lifecycleStatus == statusFilter).toList();

  Future<void> load() async {
    loading = true;
    error = null;
    _page = 1;
    notifyListeners();
    try {
      final page = await _api.listCatalog(page: _page, pageSize: _pageSize);
      assets = page.items;
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('AssetsListState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudieron cargar los activos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> loadMore() async {
    if (loadingMore || !hasMore) return;
    loadingMore = true;
    notifyListeners();
    try {
      final page = await _api.listCatalog(page: _page + 1, pageSize: _pageSize);
      _page += 1;
      assets = [...assets, ...page.items];
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('AssetsListState.loadMore failed: $e\n$st');
    } finally {
      loadingMore = false;
      notifyListeners();
    }
  }

  void setFilter(String? status) {
    statusFilter = status;
    notifyListeners();
  }
}
