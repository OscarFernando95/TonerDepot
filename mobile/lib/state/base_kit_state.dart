import 'package:flutter/foundation.dart';

import '../models/base_kit.dart';
import '../services/api_client.dart';
import '../services/base_kit_api.dart';
import '../services/realtime_service.dart';

/// Kit base de consumibles de una marca (espejo de la tarjeta de AssetBrandDetailView.vue). Se edita en memoria y se
/// guarda completo con [save] (PUT reemplaza el kit entero).
class BaseKitState extends ChangeNotifier {
  BaseKitState(ApiClient client, this.brandId) : api = BaseKitApi(client) {
    // Cambios de inventario hechos por otro usuario: recarga silenciosa, salvo que haya ediciones sin guardar.
    _unsubscribe = RealtimeService.instance.subscribe(['Inventory'], (e) {
      if (dirty || saving) return;
      load(silent: true);
    });
  }

  final BaseKitApi api;
  final String brandId;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;

  bool loading = false;
  bool saving = false;
  bool dirty = false;
  String? error;
  List<BaseKitItem> rows = [];

  /// Sube cada vez que las filas se reemplazan desde el servidor (carga/guardado): los campos de texto de las filas
  /// lo usan en su key para tomar el valor nuevo.
  int version = 0;

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

  List<String> get groups =>
      BaseKitLogic.distinctGroups(rows.map((r) => r.groupName));

  /// Hay filas sin unidad/grupo: no se puede guardar.
  bool get hasMissingGroup => rows.any((r) => r.groupName.trim().isEmpty);

  bool get canSave => dirty && !saving && !hasMissingGroup;

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      rows = await api.getBrandKit(brandId);
      dirty = false;
      version++;
    } catch (e, st) {
      debugPrint('BaseKitState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudo cargar el kit base.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Devuelve un mensaje de error de negocio (ítem repetido) o null.
  String? add(KitItemOption item, String groupName, int quantity) {
    final group = groupName.trim();
    if (group.isEmpty) return 'Indica la unidad o grupo del ítem.';
    if (rows.any((r) => r.itemId == item.id)) {
      return 'Ese ítem ya está en el kit.';
    }
    rows = [
      ...rows,
      BaseKitItem(
        itemId: item.id,
        itemName: item.name,
        category: item.category,
        groupName: group,
        quantity: BaseKitLogic.clampQuantity(quantity),
      ),
    ];
    dirty = true;
    notifyListeners();
    return null;
  }

  void remove(String itemId) {
    rows = rows.where((r) => r.itemId != itemId).toList();
    dirty = true;
    notifyListeners();
  }

  void setGroup(String itemId, String group) {
    rows = [
      for (final r in rows)
        r.itemId == itemId ? r.copyWith(groupName: group) : r,
    ];
    dirty = true;
    notifyListeners();
  }

  void setQuantity(String itemId, int quantity) {
    rows = [
      for (final r in rows)
        r.itemId == itemId
            ? r.copyWith(quantity: BaseKitLogic.clampQuantity(quantity))
            : r,
    ];
    dirty = true;
    notifyListeners();
  }

  /// Devuelve un mensaje de error o null si se guardó.
  Future<String?> save() async {
    saving = true;
    notifyListeners();
    try {
      rows = await api.setBrandKit(brandId, rows);
      dirty = false;
      version++;
      return null;
    } catch (e, st) {
      debugPrint('BaseKitState.save failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo guardar el kit base.';
    } finally {
      saving = false;
      notifyListeners();
    }
  }
}
