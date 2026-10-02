import 'package:flutter/foundation.dart';

import '../models/technician_home.dart';
import 'api_client.dart';

class TechnicianHomeApi {
  TechnicianHomeApi(this._client);
  final ApiClient _client;

  Future<TechnicianHome> get() async {
    try {
      final response = await _client.dio.get('/technicians/me/home');
      return TechnicianHome.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TechnicianHomeApi.get failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
