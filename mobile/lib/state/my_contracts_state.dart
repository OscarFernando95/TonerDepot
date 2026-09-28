import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../services/api_client.dart';
import '../services/contract_api.dart';

class MyContractsState extends ChangeNotifier {
  MyContractsState(ApiClient client) : _api = ContractApi(client);

  final ContractApi _api;

  bool loading = false;
  String? error;
  List<Contract> contracts = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      contracts = await _api.listMine();
    } catch (e, st) {
      debugPrint('MyContractsState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar tus contratos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
