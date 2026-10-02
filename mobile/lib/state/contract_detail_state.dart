import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../models/contract_asset.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/contract_api.dart';
import '../services/contract_asset_api.dart';

class ContractDetailState extends ChangeNotifier {
  ContractDetailState(ApiClient client, this.contractId)
    : _contractApi = ContractApi(client),
      _contractAssetApi = ContractAssetApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(
      ['Contract', 'MeterReading'],
      (e) {
        if (busyWithAction) return;
        if (!(e.entity != 'Contract' || e.affects(contractId))) return;
        load(silent: true);
      },
    );
  }

  final ContractApi _contractApi;
  final ContractAssetApi _contractAssetApi;
  final String contractId;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  Contract? contract;
  List<ContractAsset> contractAssets = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _contractApi.getById(contractId),
        _contractAssetApi.list(contractId),
      ]);
      contract = results[0] as Contract;
      contractAssets = results[1] as List<ContractAsset>;
    } catch (e, st) {
      debugPrint('ContractDetailState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudo cargar el contrato.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<String?> updateContract({
    required String startDate,
    String? endDate,
    int? includedPrintsPerMonth,
    double? pricePerExtraPage,
    String? notes,
  }) => _runAction(() async {
    contract = await _contractApi.update(
      contractId,
      startDate: startDate,
      endDate: endDate,
      includedPrintsPerMonth: includedPrintsPerMonth,
      pricePerExtraPage: pricePerExtraPage,
      notes: notes,
    );
  });

  Future<String?> setStatus(String status) => _runAction(() async {
    contract = await _contractApi.setStatus(contractId, status);
  });

  Future<String?> addAsset({
    required String assetId,
    required String clientLocationId,
    String? startDate,
  }) => _runAction(() async {
    final added = await _contractAssetApi.add(
      contractId,
      assetId: assetId,
      clientLocationId: clientLocationId,
      startDate: startDate,
    );
    contractAssets = [...contractAssets, added];
    contract = await _contractApi.getById(contractId);
  });

  Future<String?> endAssetAssociation(String contractAssetId) =>
      _runAction(() async {
        await _contractAssetApi.endAssociation(contractId, contractAssetId);
        contractAssets = await _contractAssetApi.list(contractId);
        contract = await _contractApi.getById(contractId);
      });

  Future<String?> _runAction(Future<void> Function() action) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('ContractDetailState._runAction failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
