import 'package:flutter/foundation.dart';

import '../models/city.dart';
import 'api_client.dart';

/// Catálogo fijo de municipios (sembrado por DataSeeder), solo lectura, sin
/// paginar. Staff únicamente (RoleNames.StaffRoles) — un Tecnico no le pega
/// directo a este endpoint.
class CityApi {
  CityApi(this._client);
  final ApiClient _client;

  Future<List<City>> list() async {
    try {
      final response = await _client.dio.get('/cities');
      final items = response.data as List<dynamic>;
      return items
          .map((e) => City.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('CityApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
