import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import 'api_client.dart';

/// Festivos de Colombia (calculados por el backend) más los ajustes de la empresa. Solo lectura en móvil.
class HolidayApi {
  HolidayApi(this._client);
  final ApiClient _client;

  Future<List<Holiday>> list(int year) async {
    try {
      final response = await _client.dio.get(
        '/holidays',
        queryParameters: {'year': year},
      );
      return (response.data as List<dynamic>)
          .map((e) => Holiday.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('HolidayApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
