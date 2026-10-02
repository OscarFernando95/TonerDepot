import 'package:flutter/foundation.dart';

import '../models/asset_brand.dart';
import '../models/asset_model.dart';
import '../models/client.dart';
import '../models/toner_analytics.dart';
import '../models/zone.dart';
import '../services/api_client.dart';
import '../services/asset_brand_api.dart';
import '../services/asset_model_api.dart';
import '../services/client_api.dart';
import '../services/realtime_service.dart';
import '../services/toner_analytics_api.dart';
import '../services/zone_api.dart';

/// BI de tóner (espejo de TonerBiView.vue): filtros, resumen y máquinas paginadas. Solo Administrador.
class TonerAnalyticsState extends ChangeNotifier {
  TonerAnalyticsState(ApiClient client, {DateTime? now})
    : _api = TonerAnalyticsApi(client),
      _zoneApi = ZoneApi(client),
      _clientApi = ClientApi(client),
      _brandApi = AssetBrandApi(client),
      _modelApi = AssetModelApi(client),
      filter = TonerFilter.defaultRange(now ?? DateTime.now()) {
    // Movimientos de inventario hechos por otros usuarios/dispositivos: recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Inventory'], (e) {
      reload(silent: true);
    });
  }

  static const pageSize = 15;

  final TonerAnalyticsApi _api;
  final ZoneApi _zoneApi;
  final ClientApi _clientApi;
  final AssetBrandApi _brandApi;
  final AssetModelApi _modelApi;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;
  int _requestId = 0;

  TonerFilter filter;
  bool loading = false;
  bool loadingMachines = false;
  String? error;

  TonerSummary? summary;
  List<TonerMachineRow> machines = [];
  int page = 1;
  int totalCount = 0;

  List<Zone> zones = [];
  List<Client> clients = [];
  List<AssetBrand> brands = [];
  List<AssetModel> models = [];

  int get totalPages =>
      totalCount == 0 ? 1 : ((totalCount + pageSize - 1) ~/ pageSize);

  @override
  void dispose() {
    _disposed = true;
    _unsubscribe();
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  /// Catálogos de los filtros y primera carga.
  Future<void> init() async {
    await _loadCatalogs();
    await reload();
  }

  Future<void> _loadCatalogs() async {
    try {
      final (z, c, b) = await (
        _zoneApi.list(),
        _clientApi.list(),
        _brandApi.list(),
      ).wait;
      zones = z;
      clients = c;
      brands = b;
      notifyListeners();
    } catch (e, st) {
      // Sin catálogos el BI sigue funcionando; solo faltan opciones de filtro.
      debugPrint('TonerAnalyticsState._loadCatalogs failed: $e\n$st');
    }
  }

  Future<void> reload({bool silent = false}) async {
    final id = ++_requestId;
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final (s, m) = await (
        _api.getSummary(filter),
        _api.listMachines(filter, page: page, pageSize: pageSize),
      ).wait;
      if (id != _requestId) return;
      summary = s;
      machines = m.items;
      totalCount = m.totalCount;
      error = null;
    } catch (e, st) {
      debugPrint('TonerAnalyticsState.reload failed: $e\n$st');
      if (id != _requestId) return;
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudo cargar el BI de tóner.';
      }
    } finally {
      if (id == _requestId) {
        loading = false;
        notifyListeners();
      }
    }
  }

  Future<void> goToPage(int target) async {
    final clamped = target.clamp(1, totalPages);
    if (clamped == page) return;
    page = clamped;
    final id = ++_requestId;
    loadingMachines = true;
    notifyListeners();
    try {
      final result = await _api.listMachines(
        filter,
        page: page,
        pageSize: pageSize,
      );
      if (id != _requestId) return;
      machines = result.items;
      totalCount = result.totalCount;
    } catch (e, st) {
      debugPrint('TonerAnalyticsState.goToPage failed: $e\n$st');
      if (id != _requestId) return;
      error = e is ApiException
          ? e.message
          : 'No se pudo cargar la página de máquinas.';
    } finally {
      if (id == _requestId) {
        loadingMachines = false;
        notifyListeners();
      }
    }
  }

  Future<void> _apply(TonerFilter next) {
    filter = next;
    page = 1;
    return reload();
  }

  Future<void> setRange(DateTime from, DateTime to) =>
      _apply(filter.copyWith(from: from, to: to));

  Future<void> setZone(String? zoneId) =>
      _apply(filter.copyWith(zoneId: zoneId));

  Future<void> setClient(String? clientId) =>
      _apply(filter.copyWith(clientId: clientId));

  Future<void> setModel(String? modelId) =>
      _apply(filter.copyWith(modelId: modelId));

  /// Quita zona, cliente, marca y modelo (deja el rango de fechas).
  Future<void> clearDimensionFilters() {
    models = [];
    return _apply(
      filter.copyWith(
        zoneId: null,
        clientId: null,
        brandId: null,
        modelId: null,
      ),
    );
  }

  /// Cambiar la marca limpia el modelo y carga los modelos de la nueva marca.
  Future<void> setBrand(String? brandId) async {
    models = [];
    final next = filter.copyWith(brandId: brandId, modelId: null);
    if (brandId != null) {
      try {
        models = await _modelApi.listForBrand(brandId);
      } catch (e, st) {
        debugPrint('TonerAnalyticsState.setBrand loadModels failed: $e\n$st');
      }
    }
    await _apply(next);
  }
}
