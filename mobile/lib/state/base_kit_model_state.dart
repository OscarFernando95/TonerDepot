import 'package:flutter/foundation.dart';

import '../models/base_kit.dart';
import '../services/api_client.dart';
import '../services/base_kit_api.dart';

/// Kit efectivo de un modelo (lo heredado de la marca + sus ajustes), en edición dentro de la hoja "Kit".
class BaseKitModelState extends ChangeNotifier {
  BaseKitModelState(ApiClient client, this.brandId, this.modelId)
    : api = BaseKitApi(client);

  final BaseKitApi api;
  final String brandId;
  final String modelId;
  bool _disposed = false;

  bool loading = false;
  bool saving = false;
  String? error;
  List<ModelKitRow> rows = [];

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  List<String> get groups =>
      BaseKitLogic.distinctGroups(rows.map((r) => r.groupName));

  bool get hasMissingGroup => rows.any((r) => r.hasMissingGroup);

  bool get canSave => !loading && !saving && error == null && !hasMissingGroup;

  Future<void> load() async {
    loading = true;
    error = null;
    notifyListeners();
    try {
      // El kit de la marca se pide fresco (no el que esté editándose sin guardar) para saber qué se heredó realmente.
      final (effective, brandKit) = await (
        api.getModelKit(brandId, modelId),
        api.getBrandKit(brandId),
      ).wait;
      rows = BaseKitLogic.buildModelRows(effective, brandKit);
    } catch (e, st) {
      debugPrint('BaseKitModelState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudo cargar el kit del modelo.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  void setIncluded(String itemId, bool included) =>
      _update(itemId, (r) => r.copyWith(excluded: !included));

  void setGroup(String itemId, String group) =>
      _update(itemId, (r) => r.copyWith(groupName: group));

  void setQuantity(String itemId, int quantity) => _update(
    itemId,
    (r) => r.copyWith(quantity: BaseKitLogic.clampQuantity(quantity)),
  );

  void _update(String itemId, ModelKitRow Function(ModelKitRow) change) {
    rows = [for (final r in rows) r.itemId == itemId ? change(r) : r];
    notifyListeners();
  }

  /// Agrega un ítem solo a este modelo. Devuelve un mensaje de error o null.
  String? addModelOnly(KitItemOption item, String groupName, int quantity) {
    final group = groupName.trim();
    if (group.isEmpty) return 'Indica la unidad o grupo del ítem.';
    if (rows.any((r) => r.itemId == item.id)) {
      return 'Ese ítem ya está en el kit del modelo.';
    }
    rows = [
      ...rows,
      ModelKitRow(
        itemId: item.id,
        itemName: item.name,
        category: item.category,
        groupName: group,
        quantity: BaseKitLogic.clampQuantity(quantity),
        excluded: false,
        fromBrand: false,
      ),
    ];
    notifyListeners();
    return null;
  }

  void removeModelOnly(String itemId) {
    rows = rows.where((r) => r.itemId != itemId || r.fromBrand).toList();
    notifyListeners();
  }

  /// Devuelve un mensaje de error o null si se guardó.
  Future<String?> save() async {
    saving = true;
    notifyListeners();
    try {
      await api.setModelKit(
        brandId,
        modelId,
        BaseKitLogic.buildOverrides(rows),
      );
      return null;
    } catch (e, st) {
      debugPrint('BaseKitModelState.save failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo guardar el kit del modelo.';
    } finally {
      saving = false;
      notifyListeners();
    }
  }
}
