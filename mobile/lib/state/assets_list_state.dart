import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/contract.dart';
import '../models/grouping.dart';
import '../models/paged_result.dart';
import '../services/api_client.dart';
import '../services/asset_api.dart';
import '../services/contract_api.dart';

const _noCity = 'Sin ciudad';
const _warehouse = 'Bodega — Oficina principal';
const _noClient = 'Sin cliente';
const _noContract = 'Sin contrato';

/// A diferencia del portal Cliente (listMine, una sola página confiando en
/// el default), este catálogo pagina de verdad — GET /assets no tiene
/// filtros server-side, así que con más de una página el filtro por estado
/// necesita ir cargando más antes de poder confiar en lo que se ve.
class AssetsListState extends ChangeNotifier {
  AssetsListState(ApiClient client)
    : _api = AssetApi(client),
      _contractApi = ContractApi(client);

  final AssetApi _api;
  final ContractApi _contractApi;
  static const _pageSize = 50;

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  String? error;
  List<Asset> assets = [];
  List<Contract> _contracts = [];
  int _page = 1;
  String? statusFilter;
  String? cityFilter;
  String? clientFilter;

  /// 'grouped' | 'flat' — espejo de viewMode en AssetsListView.vue.
  String viewMode = 'grouped';

  /// `exclude` es la clave del propio filtro que está construyendo sus
  /// opciones — se omite a sí mismo para que, al elegir ciudad, el cliente
  /// siga ofreciendo todas las opciones de ESA ciudad (si también se
  /// filtrara por el cliente ya elegido, cambiar de ciudad podría vaciar la
  /// lista de un cliente que ya no aplica). Espejo exacto de `matches(a,
  /// exclude)` en AssetsListView.vue.
  bool _matches(Asset a, String exclude) {
    final cityOk = exclude == 'city' || cityFilter == null || a.cityName == cityFilter;
    final clientOk = exclude == 'client' || clientFilter == null || a.currentClientId == clientFilter;
    final statusOk = exclude == 'status' || statusFilter == null || a.lifecycleStatus == statusFilter;
    return cityOk && clientOk && statusOk;
  }

  /// Espejo de AssetsListView.vue: filtro client-side sobre lo ya cargado,
  /// ciudad + cliente + estado combinados.
  List<Asset> get filtered => assets.where((a) => _matches(a, '')).toList();

  /// Opciones de ciudad/cliente en cascada: cada una se calcula sobre los
  /// activos que ya cumplen los OTROS filtros elegidos, igual que
  /// cityOptions/clientOptions en AssetsListView.vue — elegir una ciudad
  /// reduce las opciones de cliente a las de esa ciudad, y viceversa.
  List<String> get cityOptions => ({
    for (final a in assets)
      if (a.cityName != null && _matches(a, 'city')) a.cityName!,
  }.toList())..sort();

  List<(String, String)> get clientOptions {
    final seen = <String, String>{};
    for (final a in assets) {
      if (a.currentClientId != null && _matches(a, 'client')) {
        seen[a.currentClientId!] = a.currentClientName ?? a.currentClientId!;
      }
    }
    final list = seen.entries.map((e) => (e.key, e.value)).toList();
    list.sort((a, b) => a.$2.compareTo(b.$2));
    return list;
  }

  String _contractLabel(String contractId) {
    Contract? contract;
    for (final c in _contracts) {
      if (c.id == contractId) {
        contract = c;
        break;
      }
    }
    if (contract == null) return contractId;
    final end = contract.endDate == null
        ? 'indefinida'
        : _formatDate(contract.endDate!);
    return '${contract.clientName} (${_formatDate(contract.startDate)} – $end)';
  }

  static String _formatDate(String iso) {
    final d = DateTime.tryParse(iso);
    if (d == null) return iso;
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(d.day)}/${two(d.month)}/${d.year}';
  }

  /// Árbol ciudad → cliente → contrato sobre [filtered] — espejo de
  /// groupedByCity en AssetsListView.vue, incluida la regla de un activo
  /// EnBodega: está físicamente en la bodega de la empresa, no "sin ciudad".
  List<CityGroup<Asset>> get groupedByCity => groupByCityClientContract<Asset>(
    filtered,
    city: (a) =>
        a.cityName ?? (a.lifecycleStatus == 'EnBodega' ? _warehouse : _noCity),
    clientKey: (a) => a.currentClientId ?? _noClient,
    clientLabel: (a) => a.currentClientName ?? _noClient,
    contractKey: (a) => a.activeContractId ?? _noContract,
    contractLabel: (a) =>
        a.activeContractId != null ? _contractLabel(a.activeContractId!) : _noContract,
  );

  void setViewMode(String mode) {
    viewMode = mode;
    notifyListeners();
  }

  Future<void> load() async {
    loading = true;
    error = null;
    _page = 1;
    notifyListeners();
    try {
      final results = await Future.wait([
        _api.listCatalog(page: _page, pageSize: _pageSize),
        _contractApi.listCatalog(page: 1, pageSize: 200),
      ]);
      final page = results[0] as PagedResult<Asset>;
      assets = page.items;
      hasMore = page.hasMore;
      _contracts = (results[1] as PagedResult<Contract>).items;
    } catch (e, st) {
      debugPrint('AssetsListState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los activos.';
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

  void setCityFilter(String? city) {
    cityFilter = city;
    notifyListeners();
  }

  void setClientFilter(String? clientId) {
    clientFilter = clientId;
    notifyListeners();
  }
}
