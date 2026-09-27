import 'package:flutter/foundation.dart';

import '../models/client.dart';
import '../services/api_client.dart';
import '../services/client_api.dart';

class ClientsListState extends ChangeNotifier {
  ClientsListState(ApiClient client) : _api = ClientApi(client);

  final ClientApi _api;

  bool loading = false;
  String? error;
  List<Client> clients = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      clients = await _api.list();
    } catch (e, st) {
      debugPrint('ClientsListState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudieron cargar los clientes.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
