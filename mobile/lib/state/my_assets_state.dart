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

  /// Equipos que el cliente ya tiene en sus sedes (todo lo que no es una instalación pendiente).
  List<Asset> assets = [];

  /// Equipos que un técnico todavía tiene que instalar.
  List<Asset> pendingInstallations = [];

  /// Separa por estado lo que devuelve GET /assets: para el cliente incluye los equipos pendientes de instalar en sus sedes.
  static ({List<Asset> installed, List<Asset> pending}) partition(
    Iterable<Asset> all,
  ) => (
    installed: all
        .where((a) => a.lifecycleStatus != 'PendienteInstalacion')
        .toList(),
    pending: all
        .where((a) => a.lifecycleStatus == 'PendienteInstalacion')
        .toList(),
  );

  bool get isEmpty => assets.isEmpty && pendingInstallations.isEmpty;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final split = partition(await _api.listMine());
      assets = split.installed;
      pendingInstallations = split.pending;
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
