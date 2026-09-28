import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../services/api_client.dart';
import '../services/asset_api.dart';

class MyAssetsState extends ChangeNotifier {
  MyAssetsState(ApiClient client) : _api = AssetApi(client);

  final AssetApi _api;

  bool loading = false;
  String? error;
  List<Asset> assets = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      assets = await _api.listMine();
    } catch (e, st) {
      debugPrint('MyAssetsState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar tus activos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
