import 'package:flutter/foundation.dart';

import '../models/client.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Entrada de sede inicial al crear un cliente — POST /clients exige al
/// menos una (ver CreateClientRequestValidator). No es un DTO que el
/// servidor devuelva, solo la forma del body de creación.
typedef NewClientLocation = ({
  String cityId,
  String name,
  String address,
  String? contactName,
  String? contactPhone,
});

/// Espejo de frontend-web/src/api/clients.ts. Toda la clase es
/// RoleNames.StaffRoles (Administrador/Coordinador) en el backend.
class ClientApi {
  ClientApi(this._client);
  final ApiClient _client;

  Future<List<Client>> list() async {
    try {
      final response = await _client.dio.get('/clients');
      final page = PagedResult<Client>.fromJson(response.data as Map<String, dynamic>, Client.fromJson);
      return page.items;
    } catch (e, st) {
      debugPrint('ClientApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Client> getById(String id) async {
    try {
      final response = await _client.dio.get('/clients/$id');
      return Client.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientApi.getById failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Exige al menos una sede en `locations` — el backend responde 409 si no.
  Future<Client> create({
    required String name,
    String? taxId,
    String? contactName,
    String? contactEmail,
    String? contactPhone,
    required bool isContractClient,
    required List<NewClientLocation> locations,
  }) async {
    try {
      final response = await _client.dio.post('/clients', data: {
        'name': name,
        'taxId': ?taxId,
        'contactName': ?contactName,
        'contactEmail': ?contactEmail,
        'contactPhone': ?contactPhone,
        'isContractClient': isContractClient,
        'locations': [
          for (final loc in locations)
            {
              'cityId': loc.cityId,
              'name': loc.name,
              'address': loc.address,
              'contactName': ?loc.contactName,
              'contactPhone': ?loc.contactPhone,
            },
        ],
      });
      return Client.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// A propósito no toca sedes — esas se editan aparte (client_location_api.dart).
  Future<Client> update(
    String id, {
    required String name,
    String? taxId,
    String? contactName,
    String? contactEmail,
    String? contactPhone,
    required bool isContractClient,
  }) async {
    try {
      final response = await _client.dio.put('/clients/$id', data: {
        'name': name,
        'taxId': ?taxId,
        'contactName': ?contactName,
        'contactEmail': ?contactEmail,
        'contactPhone': ?contactPhone,
        'isContractClient': isContractClient,
      });
      return Client.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Client> setStatus(String id, bool isActive) async {
    try {
      final response = await _client.dio.patch('/clients/$id/status', data: {'isActive': isActive});
      return Client.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
