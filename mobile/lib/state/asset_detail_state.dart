import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/asset_status_log.dart';
import '../models/meter_reading.dart';
import '../services/api_client.dart';
import '../services/asset_api.dart';

class AssetDetailState extends ChangeNotifier {
  /// `isStaff` decide si se piden las lecturas de contador — mismo endpoint
  /// [Authorize(Roles = StaffRoles)] que status-history, pero se pide aparte
  /// por si algún día un rol ve estado sin ver lecturas.
  AssetDetailState(ApiClient client, this.assetId, {required this.isStaff})
    : _api = AssetApi(client);

  final AssetApi _api;
  final String assetId;
  final bool isStaff;

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  Asset? asset;
  List<AssetStatusLog> statusHistory = [];
  List<MeterReading> meterReadings = [];

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      final results = await Future.wait([
        _api.getById(assetId),
        _api.getStatusHistory(assetId),
        if (isStaff) _api.getMeterReadings(assetId),
      ]);
      asset = results[0] as Asset;
      statusHistory = results[1] as List<AssetStatusLog>;
      if (isStaff) {
        meterReadings = results[2] as List<MeterReading>;
      }
    } catch (e, st) {
      debugPrint('AssetDetailState.load failed: $e\n$st');
      error = e is ApiException ? e.message : 'No se pudo cargar el activo.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Tras registrar, recarga el activo (para el `lastMeterReading` fresco en
  /// el encabezado) y el historial — igual que AssetDetailView.vue saveReading.
  Future<String?> addMeterReading(int counterValue) => _runAction(() async {
    await _api.addMeterReading(assetId, counterValue: counterValue);
    final results = await Future.wait([
      _api.getById(assetId),
      _api.getMeterReadings(assetId),
    ]);
    asset = results[0] as Asset;
    meterReadings = results[1] as List<MeterReading>;
  });

  Future<String?> updateAsset({
    required String assetModelId,
    required String serialNumber,
    required String type,
  }) => _runAction(() async {
    asset = await _api.update(
      assetId,
      assetModelId: assetModelId,
      serialNumber: serialNumber,
      type: type,
    );
  });

  Future<String?> changeStatus({
    required String newStatus,
    String? clientLocationId,
    String? area,
    String? notes,
  }) => _runAction(() async {
    asset = await _api.setStatus(
      assetId,
      newStatus: newStatus,
      clientLocationId: clientLocationId,
      area: area,
      notes: notes,
    );
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
