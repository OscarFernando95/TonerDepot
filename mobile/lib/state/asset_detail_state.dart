import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/asset_status_log.dart';
import '../models/meter_reading.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/asset_api.dart';

class AssetDetailState extends ChangeNotifier {
  /// `isStaff` decide si se piden las lecturas de contador — mismo endpoint
  /// [Authorize(Roles = StaffRoles)] que status-history, pero se pide aparte
  /// por si algún día un rol ve estado sin ver lecturas.
  AssetDetailState(ApiClient client, this.assetId, {required this.isStaff})
    : _api = AssetApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(
      ['Asset', 'MeterReading', 'Contract'],
      (e) {
        if (busyWithAction) return;
        if (!(e.entity != 'Asset' || e.affects(assetId))) return;
        load(silent: true);
      },
    );
  }

  final AssetApi _api;
  final String assetId;
  final bool isStaff;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  Asset? asset;
  List<AssetStatusLog> statusHistory = [];
  List<MeterReading> meterReadings = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
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
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudo cargar el activo.';
      }
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
