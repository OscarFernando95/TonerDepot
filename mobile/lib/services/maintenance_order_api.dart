import '../models/maintenance_order.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

class MaintenanceOrderApi {
  MaintenanceOrderApi(this._client);
  final ApiClient _client;

  Future<List<MaintenanceOrder>> listMine() async {
    try {
      final response = await _client.dio.get('/maintenance-orders');
      final page = PagedResult<MaintenanceOrder>.fromJson(
        response.data as Map<String, dynamic>,
        MaintenanceOrder.fromJson,
      );
      return page.items;
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }
}
