import 'package:flutter/foundation.dart';

import '../models/asset_model.dart';
import '../services/api_client.dart';
import '../services/asset_model_api.dart';

class AssetBrandDetailState extends ChangeNotifier {
  AssetBrandDetailState(ApiClient client, this.brandId)
    : _api = AssetModelApi(client);

  final AssetModelApi _api;
  final String brandId;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<AssetModel> models = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      models = await _api.listForBrand(brandId);
    } catch (e, st) {
      debugPrint('AssetBrandDetailState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los modelos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> addModel({
    required String name,
    required int generalPrintThreshold,
    required int generalMonthsInterval,
    required int unitsPrintThreshold,
    required int unitsMonthsInterval,
    required int consumablesPrintThreshold,
  }) => _runAction(() async {
    final created = await _api.create(
      brandId,
      name: name,
      generalPrintThreshold: generalPrintThreshold,
      generalMonthsInterval: generalMonthsInterval,
      unitsPrintThreshold: unitsPrintThreshold,
      unitsMonthsInterval: unitsMonthsInterval,
      consumablesPrintThreshold: consumablesPrintThreshold,
    );
    models = [...models, created];
  });

  Future<String?> updateModel(
    String modelId, {
    required String name,
    required int generalPrintThreshold,
    required int generalMonthsInterval,
    required int unitsPrintThreshold,
    required int unitsMonthsInterval,
    required int consumablesPrintThreshold,
  }) => _runAction(() async {
    final updated = await _api.update(
      brandId,
      modelId,
      name: name,
      generalPrintThreshold: generalPrintThreshold,
      generalMonthsInterval: generalMonthsInterval,
      unitsPrintThreshold: unitsPrintThreshold,
      unitsMonthsInterval: unitsMonthsInterval,
      consumablesPrintThreshold: consumablesPrintThreshold,
    );
    models = [for (final m in models) m.id == modelId ? updated : m];
  });

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('AssetBrandDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
