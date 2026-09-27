import 'package:flutter/foundation.dart';

import '../models/city.dart';
import '../models/client.dart';
import '../models/client_location.dart';
import '../services/api_client.dart';
import '../services/city_api.dart';
import '../services/client_api.dart';
import '../services/client_location_api.dart';

/// Espejo de ClientDetailView.vue: siempre 3 llamadas al abrir (cliente +
/// sedes + catálogo de ciudades para los selects) — ClientDto no trae las
/// sedes embebidas.
class ClientDetailState extends ChangeNotifier {
  ClientDetailState(ApiClient client, this.clientId)
      : _clientApi = ClientApi(client),
        _locationApi = ClientLocationApi(client),
        _cityApi = CityApi(client);

  final ClientApi _clientApi;
  final ClientLocationApi _locationApi;
  final CityApi _cityApi;
  final String clientId;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  Client? client;
  List<ClientLocation> locations = [];
  List<City> cities = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([
        _clientApi.getById(clientId),
        _locationApi.listForClient(clientId),
        _cityApi.list(),
      ]);
      client = results[0] as Client;
      locations = results[1] as List<ClientLocation>;
      cities = results[2] as List<City>;
    } catch (e, st) {
      debugPrint('ClientDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar el cliente.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> updateClient({
    required String name,
    String? taxId,
    String? contactName,
    String? contactEmail,
    String? contactPhone,
    required bool isContractClient,
  }) =>
      _runAction(() async {
        client = await _clientApi.update(
          clientId,
          name: name,
          taxId: taxId,
          contactName: contactName,
          contactEmail: contactEmail,
          contactPhone: contactPhone,
          isContractClient: isContractClient,
        );
      });

  Future<String?> setClientStatus(bool isActive) => _runAction(() async {
        client = await _clientApi.setStatus(clientId, isActive);
      });

  Future<String?> addLocation({
    required String cityId,
    required String name,
    required String address,
    String? contactName,
    String? contactPhone,
  }) =>
      _runAction(() async {
        final created = await _locationApi.create(
          clientId,
          cityId: cityId,
          name: name,
          address: address,
          contactName: contactName,
          contactPhone: contactPhone,
        );
        locations = [...locations, created];
      });

  Future<String?> updateLocation(
    String locationId, {
    required String cityId,
    required String name,
    required String address,
    String? contactName,
    String? contactPhone,
  }) =>
      _runAction(() async {
        final updated = await _locationApi.update(
          clientId,
          locationId,
          cityId: cityId,
          name: name,
          address: address,
          contactName: contactName,
          contactPhone: contactPhone,
        );
        locations = [
          for (final loc in locations) loc.id == locationId ? updated : loc,
        ];
      });

  Future<String?> setLocationStatus(String locationId, bool isActive) => _runAction(() async {
        final updated = await _locationApi.setStatus(clientId, locationId, isActive);
        locations = [
          for (final loc in locations) loc.id == locationId ? updated : loc,
        ];
      });

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('ClientDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
