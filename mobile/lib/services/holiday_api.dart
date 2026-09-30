import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import 'api_client.dart';

/// Festivos de Colombia (calculados por el backend) más los ajustes de la
/// empresa. Los ajustes (agregar/quitar) son RoleNames.Administrador
/// únicamente en el backend — espejo de frontend-web/src/api/holidays.ts.
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

  /// `isWorkingDay: false` = nuevo cierre de la empresa (festivo custom);
  /// `true` = forzar un festivo legal a laborable.
  Future<Holiday> setOverride(String date, String name, bool isWorkingDay) async {
    try {
      final response = await _client.dio.put(
        '/holidays/overrides',
        data: {'date': date, 'name': name, 'isWorkingDay': isWorkingDay},
      );
      return Holiday.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('HolidayApi.setOverride failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<void> removeOverride(String date) async {
    try {
      await _client.dio.delete('/holidays/overrides/$date');
    } catch (e, st) {
      debugPrint('HolidayApi.removeOverride failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
