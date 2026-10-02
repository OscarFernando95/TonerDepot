/// Kit base de consumibles (espejo de BaseKitItemDto / ModelKitItemDto / ModelKitOverrideRequest de
/// backend/.../Inventory/Dtos/InventoryDtos.cs y de AssetBrandDetailView.vue). Se define por marca; cada modelo lo
/// hereda y lo puede ajustar. Sin precios ni costos.
library;

const baseKitMinQuantity = 1;
const baseKitMaxQuantity = 100;
const baseKitMaxGroupLength = 100;

/// Fila del kit de una marca (también lo que devuelve GET /asset-brands/{id}/base-items).
class BaseKitItem {
  final String itemId;
  final String itemName;
  final String category;
  final String groupName;
  final int quantity;

  const BaseKitItem({
    required this.itemId,
    required this.itemName,
    this.category = '',
    required this.groupName,
    required this.quantity,
  });

  factory BaseKitItem.fromJson(Map<String, dynamic> json) => BaseKitItem(
    itemId: json['itemId'] as String,
    itemName: json['itemName'] as String? ?? '',
    category: json['category'] as String? ?? '',
    groupName: json['groupName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 1,
  );

  BaseKitItem copyWith({String? groupName, int? quantity}) => BaseKitItem(
    itemId: itemId,
    itemName: itemName,
    category: category,
    groupName: groupName ?? this.groupName,
    quantity: quantity ?? this.quantity,
  );
}

/// Fila del kit efectivo de un modelo tal como lo devuelve el servidor.
class ModelKitItem extends BaseKitItem {
  /// "Marca" (heredado tal cual) | "Modelo" (ajustado o agregado por el modelo).
  final String source;
  final bool excluded;

  const ModelKitItem({
    required super.itemId,
    required super.itemName,
    super.category,
    required super.groupName,
    required super.quantity,
    required this.source,
    required this.excluded,
  });

  factory ModelKitItem.fromJson(Map<String, dynamic> json) => ModelKitItem(
    itemId: json['itemId'] as String,
    itemName: json['itemName'] as String? ?? '',
    category: json['category'] as String? ?? '',
    groupName: json['groupName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 1,
    source: json['source'] as String? ?? 'Marca',
    excluded: json['excluded'] as bool? ?? false,
  );
}

/// Ítem del catálogo de inventario ofrecido al armar un kit (GET /inventory/items). Modelo propio y mínimo: el
/// módulo de Inventario tiene el suyo y no se toca desde acá.
class KitItemOption {
  final String id;
  final String name;
  final String category;
  final String? unit;

  const KitItemOption({
    required this.id,
    required this.name,
    required this.category,
    this.unit,
  });

  factory KitItemOption.fromJson(Map<String, dynamic> json) => KitItemOption(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    category: json['category'] as String? ?? '',
    unit: json['unit'] as String?,
  );
}

/// Etiqueta de origen de una fila del kit del modelo.
enum ModelKitOrigin {
  inherited('Heredado'),
  adjusted('Ajustado'),
  modelOnly('Solo este modelo');

  const ModelKitOrigin(this.label);
  final String label;
}

/// Fila editable del kit de un modelo: el estado actual + lo que dice la marca (para saber qué cambió).
class ModelKitRow {
  final String itemId;
  final String itemName;
  final String category;
  final String groupName;
  final int quantity;
  final bool excluded;

  /// La marca tiene este ítem en su kit.
  final bool fromBrand;
  final String brandGroup;
  final int brandQuantity;

  const ModelKitRow({
    required this.itemId,
    required this.itemName,
    this.category = '',
    required this.groupName,
    required this.quantity,
    required this.excluded,
    required this.fromBrand,
    this.brandGroup = '',
    this.brandQuantity = 1,
  });

  ModelKitRow copyWith({String? groupName, int? quantity, bool? excluded}) =>
      ModelKitRow(
        itemId: itemId,
        itemName: itemName,
        category: category,
        groupName: groupName ?? this.groupName,
        quantity: quantity ?? this.quantity,
        excluded: excluded ?? this.excluded,
        fromBrand: fromBrand,
        brandGroup: brandGroup,
        brandQuantity: brandQuantity,
      );

  bool get groupDiffersFromBrand => groupName.trim() != brandGroup.trim();
  bool get quantityDiffersFromBrand => quantity != brandQuantity;

  /// Una fila de la marca sin tocar no genera ajuste en el servidor.
  bool get isAdjusted =>
      fromBrand &&
      (excluded || groupDiffersFromBrand || quantityDiffersFromBrand);

  ModelKitOrigin get origin {
    if (!fromBrand) return ModelKitOrigin.modelOnly;
    return isAdjusted ? ModelKitOrigin.adjusted : ModelKitOrigin.inherited;
  }

  /// Falta unidad/grupo en una fila que cuenta para el kit (las excluidas no se validan).
  bool get hasMissingGroup => !excluded && groupName.trim().isEmpty;
}

/// Lógica pura del editor de kits (sin red ni widgets) — se prueba en test/base_kit_test.dart.
class BaseKitLogic {
  const BaseKitLogic._();

  static int clampQuantity(int value) =>
      value.clamp(baseKitMinQuantity, baseKitMaxQuantity);

  /// Une el kit efectivo del modelo con el de su marca. Una fila es "de la marca" si la marca tiene ese ítem.
  static List<ModelKitRow> buildModelRows(
    List<ModelKitItem> effective,
    List<BaseKitItem> brandKit,
  ) {
    final brandById = {for (final b in brandKit) b.itemId: b};
    return [
      for (final k in effective)
        ModelKitRow(
          itemId: k.itemId,
          itemName: k.itemName,
          category: k.category,
          groupName: k.groupName,
          quantity: k.quantity,
          excluded: k.excluded,
          fromBrand: brandById.containsKey(k.itemId),
          brandGroup: brandById[k.itemId]?.groupName ?? '',
          brandQuantity: brandById[k.itemId]?.quantity ?? 1,
        ),
    ];
  }

  /// Ajustes a enviar en PUT .../models/{id}/base-items. Solo van las filas que cambian algo: lo heredado sin tocar no
  /// genera ajuste. De una fila de la marca, grupo/cantidad solo se mandan si difieren de los de la marca (null =
  /// heredar); una fila agregada solo para el modelo manda siempre ambos.
  static List<Map<String, dynamic>> buildOverrides(List<ModelKitRow> rows) {
    final result = <Map<String, dynamic>>[];
    for (final r in rows) {
      if (r.fromBrand && !r.isAdjusted) continue;
      final group = r.groupName.trim();
      final sendGroup =
          !r.fromBrand || (group.isNotEmpty && r.groupDiffersFromBrand);
      final sendQuantity = !r.fromBrand || r.quantityDiffersFromBrand;
      result.add({
        'itemId': r.itemId,
        'excluded': r.excluded,
        'groupName': sendGroup ? group : null,
        'quantity': sendQuantity ? clampQuantity(r.quantity) : null,
      });
    }
    return result;
  }

  /// Cuerpo de las filas del PUT .../asset-brands/{id}/base-items.
  static List<Map<String, dynamic>> buildBrandItems(List<BaseKitItem> rows) => [
    for (final r in rows)
      {
        'itemId': r.itemId,
        'groupName': r.groupName.trim(),
        'quantity': clampQuantity(r.quantity),
      },
  ];

  /// Grupos distintos ya usados (en orden de aparición), para sugerir al escribir.
  static List<String> distinctGroups(Iterable<String> groups) {
    final seen = <String>{};
    final result = <String>[];
    for (final g in groups) {
      final t = g.trim();
      if (t.isNotEmpty && seen.add(t)) result.add(t);
    }
    return result;
  }

  /// Sugerencias para lo que se está escribiendo: coincidencia parcial sin distinguir mayúsculas, sin repetir el
  /// valor ya escrito tal cual.
  static List<String> suggestGroups(
    List<String> groups,
    String query, {
    int limit = 6,
  }) {
    final q = query.trim().toLowerCase();
    return groups
        .where((g) {
          final lower = g.toLowerCase();
          return lower != q && (q.isEmpty || lower.contains(q));
        })
        .take(limit)
        .toList();
  }
}
