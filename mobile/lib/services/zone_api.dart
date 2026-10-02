import 'package:flutter/foundation.dart';

import '../models/zone.dart';
import 'api_client.dart';

/// Zonas (agrupan municipios) — solo Staff. Catálogo sin paginar.
class ZoneApi {
  ZoneApi(this._client);
  final ApiClient _client;

  Future<List<Zone>> list() async {
    try {
      final response = await _client.dio.get('/zones');
      return (response.data as List<dynamic>)
          .map((e) => Zone.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('ZoneApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Zone> create(String name) async {
    try {
      final response = await _client.dio.post('/zones', data: {'name': name});
      return Zone.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ZoneApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<Zone> rename(String id, String name) async {
    try {
      final response = await _client.dio.patch('/zones/$id', data: {'name': name});
      return Zone.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ZoneApi.rename failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Fija el conjunto completo de municipios de la zona (los de otra zona se mueven).
  Future<Zone> setCities(String id, List<String> cityIds) async {
    try {
      final response = await _client.dio.put(
        '/zones/$id/cities',
        data: {'cityIds': cityIds},
      );
      return Zone.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('ZoneApi.setCities failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<void> delete(String id) async {
    try {
      await _client.dio.delete('/zones/$id');
    } catch (e, st) {
      debugPrint('ZoneApi.delete failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
