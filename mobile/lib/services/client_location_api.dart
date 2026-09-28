import 'package:flutter/foundation.dart';

import '../models/client_location.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/clientLocations.ts. `listForClient` es
/// StaffAndClientRoles (un Cliente solo puede pedir las suyas — el backend
/// valida su propio clientId contra el claim del JWT); create/update/status
/// son StaffRoles únicamente.
class ClientLocationApi {
  ClientLocationApi(this._client);
  final ApiClient _client;

  Future<List<ClientLocation>> listForClient(String clientId) async {
    try {
      final response = await _client.dio.get('/clients/$clientId/locations');
      final items = response.data as List<dynamic>;
      return items
          .map((e) => ClientLocation.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('ClientLocationApi.listForClient failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<ClientLocation> create(
    String clientId, {
    required String cityId,
    required String name,
    required String address,
    String? contactName,
    String? contactPhone,
    double? latitude,
    double? longitude,
  }) async {
    try {
      final response = await _client.dio.post(
        '/clients/$clientId/locations',
        data: {
          'cityId': cityId,
          'name': name,
          'address': address,
          'contactName': ?contactName,
          'contactPhone': ?contactPhone,
          'latitude': ?latitude,
          'longitude': ?longitude,
        },
      );
      return ClientLocation.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientLocationApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<ClientLocation> update(
    String clientId,
    String id, {
    required String cityId,
    required String name,
    required String address,
    String? contactName,
    String? contactPhone,
    double? latitude,
    double? longitude,
  }) async {
    try {
      final response = await _client.dio.put(
        '/clients/$clientId/locations/$id',
        data: {
          'cityId': cityId,
          'name': name,
          'address': address,
          'contactName': ?contactName,
          'contactPhone': ?contactPhone,
          'latitude': ?latitude,
          'longitude': ?longitude,
        },
      );
      return ClientLocation.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientLocationApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<ClientLocation> setStatus(
    String clientId,
    String id,
    bool isActive,
  ) async {
    try {
      final response = await _client.dio.patch(
        '/clients/$clientId/locations/$id/status',
        data: {'isActive': isActive},
      );
      return ClientLocation.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ClientLocationApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
