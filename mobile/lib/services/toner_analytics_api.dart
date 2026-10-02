import 'package:flutter/foundation.dart';

import '../models/toner_analytics.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/tonerAnalytics.ts (sin exportar CSV: eso es solo web). Solo Administrador.
class TonerAnalyticsApi {
  TonerAnalyticsApi(this._client);
  final ApiClient _client;

  Future<TonerSummary> getSummary(TonerFilter filter) async {
    try {
      final response = await _client.dio.get(
        '/analytics/toner/summary',
        queryParameters: filter.toQueryParams(),
      );
      return TonerSummary.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TonerAnalyticsApi.getSummary failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TonerMachinesPage> listMachines(
    TonerFilter filter, {
    required int page,
    required int pageSize,
  }) async {
    try {
      final response = await _client.dio.get(
        '/analytics/toner/machines',
        queryParameters: {
          ...filter.toQueryParams(),
          'page': page,
          'pageSize': pageSize,
        },
      );
      return TonerMachinesPage.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('TonerAnalyticsApi.listMachines failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
