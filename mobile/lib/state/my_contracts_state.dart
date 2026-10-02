import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/contract_api.dart';

class MyContractsState extends ChangeNotifier {
  MyContractsState(ApiClient client) : _api = ContractApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Contract'], (e) {
      load(silent: true);
    });
  }

  final ContractApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  List<Contract> contracts = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      contracts = await _api.listMine();
    } catch (e, st) {
      debugPrint('MyContractsState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar tus contratos.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
