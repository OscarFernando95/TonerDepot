import 'package:flutter/foundation.dart';

import '../models/client.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/client_api.dart';

class ClientsListState extends ChangeNotifier {
  ClientsListState(ApiClient client) : _api = ClientApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Client'], (e) {
      load(silent: true);
    });
  }

  final ClientApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

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

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      clients = await _api.list();
    } catch (e, st) {
      debugPrint('ClientsListState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los clientes.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
