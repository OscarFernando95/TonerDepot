import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/asset_status_log.dart';
import '../services/api_client.dart';
import '../services/asset_api.dart';

class AssetDetailState extends ChangeNotifier {
  AssetDetailState(ApiClient client, this.assetId) : _api = AssetApi(client);

  final AssetApi _api;
  final String assetId;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  Asset? asset;
  List<AssetStatusLog> statusHistory = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([_api.getById(assetId), _api.getStatusHistory(assetId)]);
      asset = results[0] as Asset;
      statusHistory = results[1] as List<AssetStatusLog>;
    } catch (e, st) {
      debugPrint('AssetDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar el activo.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> updateAsset({required String assetModelId, required String serialNumber, required String type}) =>
      _runAction(() async {
        asset = await _api.update(assetId, assetModelId: assetModelId, serialNumber: serialNumber, type: type);
      });

  Future<String?> changeStatus({
    required String newStatus,
    String? clientLocationId,
    String? area,
    String? notes,
  }) =>
      _runAction(() async {
        asset = await _api.setStatus(assetId, newStatus: newStatus, clientLocationId: clientLocationId, area: area, notes: notes);
        statusHistory = await _api.getStatusHistory(assetId);
      });

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('AssetDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
