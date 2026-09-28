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

  List<Contract> get filtered => statusFilter == null
      ? contracts
      : contracts.where((c) => c.status == statusFilter).toList();

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
}
