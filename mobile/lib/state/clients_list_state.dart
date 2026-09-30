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
  String? cityFilter;

  /// Espejo de ClientsListView.vue: filtro client-side por ciudad.
  List<Client> get filtered => cityFilter == null
      ? clients
      : clients.where((c) => c.cityNames.contains(cityFilter)).toList();

  /// Opciones de ciudad derivadas de lo ya cargado, igual que cityOptions en
  /// ClientsListView.vue.
  List<String> get cityOptions =>
      ({for (final c in clients) ...c.cityNames}.toList())..sort();

  void setCityFilter(String? city) {
    cityFilter = city;
    notifyListeners();
  }

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      clients = await _api.list();
    } catch (e, st) {
      debugPrint('ClientsListState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los clientes.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
