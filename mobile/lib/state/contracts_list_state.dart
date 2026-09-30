import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../services/api_client.dart';
import '../services/contract_api.dart';

class ContractsListState extends ChangeNotifier {
  ContractsListState(ApiClient client) : _api = ContractApi(client);

  final ContractApi _api;
  static const _pageSize = 50;

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  String? error;
  List<Contract> contracts = [];
  int _page = 1;
  String? statusFilter;
  String? cityFilter;
  String? clientFilter;

  /// `exclude` es la clave del propio filtro que está construyendo sus
  /// opciones — se omite a sí mismo, espejo exacto de `matches(c, exclude)`
  /// en ContractsListView.vue. Un contrato puede cubrir varias ciudades
  /// (`cityNames` es una lista, a diferencia de Activos/Cronogramas), por
  /// eso `cityOk` usa `contains` en vez de `==`.
  bool _matches(Contract c, String exclude) {
    final statusOk = exclude == 'status' || statusFilter == null || c.status == statusFilter;
    final cityOk = exclude == 'city' || cityFilter == null || c.cityNames.contains(cityFilter);
    final clientOk = exclude == 'client' || clientFilter == null || c.clientId == clientFilter;
    return statusOk && cityOk && clientOk;
  }

  /// Espejo de ContractsListView.vue: filtro client-side sobre lo ya
  /// cargado, estado + ciudad + cliente combinados.
  List<Contract> get filtered => contracts.where((c) => _matches(c, '')).toList();

  /// Opciones de ciudad/cliente en cascada: cada una se calcula sobre los
  /// contratos que ya cumplen los OTROS filtros elegidos — elegir una
  /// ciudad reduce las opciones de cliente a las de esa ciudad, y viceversa.
  List<String> get cityOptions => ({
    for (final c in contracts)
      if (_matches(c, 'city')) ...c.cityNames,
  }.toList())..sort();

  List<(String, String)> get clientOptions {
    final seen = <String, String>{};
    for (final c in contracts) {
      if (_matches(c, 'client')) seen[c.clientId] = c.clientName;
    }
    final list = seen.entries.map((e) => (e.key, e.value)).toList();
    list.sort((a, b) => a.$2.compareTo(b.$2));
    return list;
  }

  Future<void> load() async {
    loading = true;
    error = null;
    _page = 1;
    notifyListeners();
    try {
      final page = await _api.listCatalog(page: _page, pageSize: _pageSize);
      contracts = page.items;
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('ContractsListState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los contratos.';
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
      contracts = [...contracts, ...page.items];
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('ContractsListState.loadMore failed: $e\n$st');
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
