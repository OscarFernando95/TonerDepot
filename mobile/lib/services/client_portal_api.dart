import 'package:flutter/foundation.dart';

import '../models/client_home.dart';
import 'api_client.dart';

/// Portal Cliente: resumen del inicio (solo rol Cliente; el servidor aplica el alcance por empresa y sede).
class ClientPortalApi {
  ClientPortalApi(this._client);
  final ApiClient _client;

  Future<ClientHome> getHome() async {
    try {
      final response = await _client.dio.get('/client-portal/home');
      final data = response.data;
      if (data is! Map) {
        throw const FormatException(
          'Respuesta inesperada de /client-portal/home',
        );
      }
      return ClientHome.fromJson(Map<String, dynamic>.from(data));
    } catch (e, st) {
      debugPrint('ClientPortalApi.getHome failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
