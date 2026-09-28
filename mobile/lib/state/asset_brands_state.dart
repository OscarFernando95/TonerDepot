import 'package:flutter/foundation.dart';

import '../models/asset_brand.dart';
import '../services/api_client.dart';
import '../services/asset_brand_api.dart';

class AssetBrandsState extends ChangeNotifier {
  AssetBrandsState(ApiClient client) : _api = AssetBrandApi(client);

  final AssetBrandApi _api;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<AssetBrand> brands = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      brands = await _api.list();
    } catch (e, st) {
      debugPrint('AssetBrandsState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar las marcas.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> addBrand(String name) async {
    busyWithAction = true;
    notifyListeners();
    try {
      final created = await _api.create(name);
      brands = [...brands, created];
      return null;
    } catch (e, st) {
      debugPrint('AssetBrandsState.addBrand failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo crear la marca.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
