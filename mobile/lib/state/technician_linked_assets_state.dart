import 'package:flutter/foundation.dart';

import '../models/asset.dart';
import '../models/technician_asset.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/asset_api.dart';
import '../services/technician_management_api.dart';

/// Activos vinculados explícitamente a un técnico — gobierna qué ve en
/// "Lectura de contadores" en la app (ver AssetService.ListForMeterReadingAsync,
/// backend). Independiente de TechnicianCoverageState: la cobertura por
/// ciudad ya no otorga visibilidad de activos por sí sola.
///
/// GET /assets no tiene filtro server-side (mismo comentario que
/// AssetsListState) — así que el catálogo completo de activos Instalado se
/// trae paginando hasta el final y el buscador de la UI filtra client-side.
class TechnicianLinkedAssetsState extends ChangeNotifier {
  TechnicianLinkedAssetsState(ApiClient client, this.technicianId)
    : _api = TechnicianManagementApi(client),
      _assetApi = AssetApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(
      ['TechnicianAsset', 'Asset'],
      (e) {
        if (busyWithAction) return;
        // Un vínculo nuevo solo cambia la lista de vinculados; un activo que cambia de estado puede
        // cambiar también los candidatos (instalados), que es la parte cara.
        if (e.entity == 'TechnicianAsset') {
          _reloadLinked();
        } else {
          load(silent: true);
        }
      },
    );
  }

  final TechnicianManagementApi _api;
  final AssetApi _assetApi;
  final String technicianId;
  static const _pageSize = 100;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool busyWithAction = false;
  String? error;
  List<TechnicianAsset> linked = [];
  List<Asset> _installedAssets = [];
  String searchQuery = '';

  List<Asset> get availableAssets {
    final linkedIds = linked.map((l) => l.assetId).toSet();
    var candidates = _installedAssets.where((a) => !linkedIds.contains(a.id));
    final q = searchQuery.trim().toLowerCase();
    if (q.isNotEmpty) {
      candidates = candidates.where(
        (a) =>
            a.serialNumber.toLowerCase().contains(q) ||
            a.model.toLowerCase().contains(q) ||
            (a.currentClientName?.toLowerCase().contains(q) ?? false) ||
            (a.currentClientLocationName?.toLowerCase().contains(q) ?? false) ||
            (a.cityName?.toLowerCase().contains(q) ?? false),
      );
    }
    return candidates.toList();
  }

  void setSearchQuery(String value) {
    searchQuery = value;
    notifyListeners();
  }

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final results = await Future.wait([
        _api.getLinkedAssets(technicianId),
        _loadAllInstalledAssets(),
      ]);
      linked = results[0] as List<TechnicianAsset>;
    } catch (e, st) {
      debugPrint('TechnicianLinkedAssetsState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los activos vinculados.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> _reloadLinked() async {
    try {
      linked = await _api.getLinkedAssets(technicianId);
      notifyListeners();
    } catch (e, st) {
      debugPrint('TechnicianLinkedAssetsState._reloadLinked failed: $e\n$st');
    }
  }

  Future<void> _loadAllInstalledAssets() async {
    final all = <Asset>[];
    var page = 1;
    while (true) {
      final result = await _assetApi.listCatalog(
        page: page,
        pageSize: _pageSize,
      );
      all.addAll(result.items.where((a) => a.lifecycleStatus == 'Instalado'));
      if (!result.hasMore) break;
      page += 1;
    }
    _installedAssets = all;
  }

  Future<String?> linkAsset(String assetId) async {
    busyWithAction = true;
    notifyListeners();
    try {
      final added = await _api.linkAsset(technicianId, assetId);
      linked = [...linked, added];
      return null;
    } catch (e, st) {
      debugPrint('TechnicianLinkedAssetsState.linkAsset failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo vincular el activo.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  /// Vincula de una vez todos los activos instalados de un cliente — cubre
  /// el caso "técnico con cobertura nueva en una ciudad, sin activos
  /// vinculados todavía": el admin filtra por el nombre del cliente y
  /// vincula todo en lugar de uno por uno.
  Future<String?> linkAllVisible() async {
    final toLink = List<Asset>.from(availableAssets);
    if (toLink.isEmpty) return null;
    busyWithAction = true;
    notifyListeners();
    try {
      for (final asset in toLink) {
        final added = await _api.linkAsset(technicianId, asset.id);
        linked = [...linked, added];
      }
      return null;
    } catch (e, st) {
      debugPrint('TechnicianLinkedAssetsState.linkAllVisible failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudieron vincular todos los activos.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  Future<String?> unlinkAsset(String technicianAssetId) async {
    busyWithAction = true;
    notifyListeners();
    try {
      await _api.unlinkAsset(technicianId, technicianAssetId);
      linked = linked.where((l) => l.id != technicianAssetId).toList();
      return null;
    } catch (e, st) {
      debugPrint('TechnicianLinkedAssetsState.unlinkAsset failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo quitar el activo.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
