import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/asset_api.dart';

class MyAssetsState extends ChangeNotifier {
  MyAssetsState(ApiClient client) : _api = AssetApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Asset'], (e) {
      load(silent: true);
    });
  }

  final AssetApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  List<Asset> assets = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      assets = await _api.listMine();
    } catch (e, st) {
      debugPrint('MyAssetsState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar tus activos.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
