import 'package:flutter/foundation.dart';

/// Espejos de VisitKitDto / VisitKitItemDto / PartOptionDto / TonerEntryDto / UsedPartRequest
/// (backend/src/Toner.Application/Inventory/Dtos/InventoryDtos.cs, sección "Consumo en visitas y tóner").
/// El parseo tolera campos opcionales ausentes.

/// Pieza usada en una visita: viaja en `parts` del check-out.
class UsedPart {
  const UsedPart({required this.itemId, required this.quantity});

  final String itemId;
  final int quantity;

  Map<String, dynamic> toJson() => {'itemId': itemId, 'quantity': quantity};

  @override
  bool operator ==(Object other) =>
      other is UsedPart && other.itemId == itemId && other.quantity == quantity;

  @override
  int get hashCode => Object.hash(itemId, quantity);
}

class VisitKitItem {
  const VisitKitItem({
    required this.itemId,
    required this.itemName,
    required this.category,
    required this.groupName,
    required this.quantity,
    required this.stock,
  });

  final String itemId;
  final String itemName;
  final String category;
  final String groupName;

  /// Cantidad por defecto del kit.
  final int quantity;

  /// Saldo en la ubicación de inventario de la zona del equipo.
  final int stock;

  factory VisitKitItem.fromJson(Map<String, dynamic> json) => VisitKitItem(
    itemId: json['itemId'] as String,
    itemName: json['itemName'] as String? ?? '',
    category: json['category'] as String? ?? '',
    groupName: json['groupName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 1,
    stock: (json['stock'] as num?)?.toInt() ?? 0,
  );
}

class VisitKit {
  const VisitKit({
    this.assetId,
    this.locationId,
    this.locationName = '',
    this.usesMainWarehouse = false,
    this.items = const [],
  });

  final String? assetId;
  final String? locationId;
  final String locationName;

  /// El municipio del equipo no tiene zona (o no está catalogado): se descuenta de la bodega principal.
  final bool usesMainWarehouse;
  final List<VisitKitItem> items;

  factory VisitKit.fromJson(Map<String, dynamic> json) => VisitKit(
    assetId: json['assetId'] as String?,
    locationId: json['locationId'] as String?,
    locationName: json['locationName'] as String? ?? '',
    usesMainWarehouse: json['usesMainWarehouse'] as bool? ?? false,
    items: (json['items'] as List<dynamic>? ?? [])
        .map((e) => VisitKitItem.fromJson(e as Map<String, dynamic>))
        .toList(),
  );

  /// Piezas del kit agrupadas por `groupName`, respetando el orden de aparición. Es lógica pesada: quien la use
  /// en un widget debe cachearla (no llamarla por render).
  Map<String, List<VisitKitItem>> get itemsByGroup {
    final groups = <String, List<VisitKitItem>>{};
    for (final item in items) {
      groups.putIfAbsent(item.groupName, () => []).add(item);
    }
    return groups;
  }
}

class PartOption {
  const PartOption({
    required this.itemId,
    required this.name,
    required this.category,
    this.unit,
    required this.stock,
  });

  final String itemId;
  final String name;

  /// ConsumibleBase | Repuesto | Toner
  final String category;
  final String? unit;
  final int stock;

  factory PartOption.fromJson(Map<String, dynamic> json) => PartOption(
    itemId: json['itemId'] as String,
    name: json['name'] as String? ?? '',
    category: json['category'] as String? ?? '',
    unit: json['unit'] as String?,
    stock: (json['stock'] as num?)?.toInt() ?? 0,
  );
}

class TonerEntry {
  const TonerEntry({
    required this.movementId,
    required this.itemId,
    required this.itemName,
    required this.quantity,
    required this.occurredAt,
    this.counterValue,
    this.notes,
    this.registeredBy,
    this.stockWarning,
  });

  final String movementId;
  final String itemId;
  final String itemName;
  final int quantity;
  final DateTime occurredAt;
  final int? counterValue;
  final String? notes;
  final String? registeredBy;

  /// Si este registro dejó el stock de la zona en negativo.
  final String? stockWarning;

  factory TonerEntry.fromJson(Map<String, dynamic> json) => TonerEntry(
    movementId: json['movementId'] as String? ?? '',
    itemId: json['itemId'] as String? ?? '',
    itemName: json['itemName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 1,
    occurredAt:
        DateTime.tryParse(json['occurredAt'] as String? ?? '') ??
        DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    counterValue: (json['counterValue'] as num?)?.toInt(),
    notes: json['notes'] as String?,
    registeredBy: json['registeredBy'] as String?,
    stockWarning: json['stockWarning'] as String?,
  );
}

/// Repuesto agregado a mano en el check-out (fuera del kit).
class ExtraPart {
  ExtraPart({
    required this.itemId,
    required this.name,
    required this.stock,
    this.quantity = 1,
  });

  final String itemId;
  final String name;
  final int stock;
  int quantity;
}

/// Suma las cantidades de las piezas repetidas (mismo `itemId`) y descarta las de cantidad <= 0. Conserva el
/// orden de primera aparición.
List<UsedPart> mergeUsedParts(Iterable<UsedPart> parts) {
  final totals = <String, int>{};
  for (final p in parts) {
    if (p.quantity <= 0) continue;
    totals.update(p.itemId, (q) => q + p.quantity, ifAbsent: () => p.quantity);
  }
  return [
    for (final e in totals.entries) UsedPart(itemId: e.key, quantity: e.value),
  ];
}

/// Cuándo se entregó/cambió el tóner. Sin fecha elegida, o si es hoy: el momento del registro. Un día pasado
/// conserva la hora actual (el selector solo da fecha). Siempre en UTC para el backend.
DateTime resolveTonerOccurredAt(DateTime? picked, DateTime now) {
  if (picked == null ||
      (picked.year == now.year &&
          picked.month == now.month &&
          picked.day == now.day)) {
    return now.toUtc();
  }
  return DateTime(
    picked.year,
    picked.month,
    picked.day,
    now.hour,
    now.minute,
  ).toUtc();
}

/// Selección de "Piezas cambiadas" del check-out: el kit base (todo marcado por defecto, con su cantidad
/// editable) más los repuestos extra. Nunca bloquea el cierre; `toUsedParts` produce la lista para `parts`.
class PartsSelection extends ChangeNotifier {
  VisitKit? _kit;
  final Map<String, bool> _checked = {};
  final Map<String, int> _quantities = {};
  final List<ExtraPart> _extras = [];

  VisitKit? get kit => _kit;
  List<ExtraPart> get extras => List.unmodifiable(_extras);

  /// Carga el kit: todo marcado, con la cantidad por defecto.
  void setKit(VisitKit kit) {
    _kit = kit;
    _checked.clear();
    _quantities.clear();
    for (final item in kit.items) {
      _checked[item.itemId] = true;
      _quantities[item.itemId] = item.quantity;
    }
    notifyListeners();
  }

  bool isChecked(String itemId) => _checked[itemId] ?? false;

  int quantityOf(String itemId) => _quantities[itemId] ?? 0;

  void toggle(String itemId, bool value) {
    _checked[itemId] = value;
    notifyListeners();
  }

  void setQuantity(String itemId, int quantity) {
    _quantities[itemId] = quantity < 1 ? 1 : quantity;
    notifyListeners();
  }

  void addExtra(PartOption option, {int quantity = 1}) {
    for (final e in _extras) {
      if (e.itemId == option.itemId) {
        e.quantity += quantity;
        notifyListeners();
        return;
      }
    }
    _extras.add(
      ExtraPart(
        itemId: option.itemId,
        name: option.name,
        stock: option.stock,
        quantity: quantity,
      ),
    );
    notifyListeners();
  }

  void setExtraQuantity(String itemId, int quantity) {
    for (final e in _extras) {
      if (e.itemId == itemId) e.quantity = quantity < 1 ? 1 : quantity;
    }
    notifyListeners();
  }

  void removeExtra(String itemId) {
    _extras.removeWhere((e) => e.itemId == itemId);
    notifyListeners();
  }

  /// ¿La cantidad pedida de una pieza del kit supera el saldo de la zona? Solo para resaltar; nunca bloquea.
  bool kitItemShort(VisitKitItem item) =>
      isChecked(item.itemId) && item.stock < quantityOf(item.itemId);

  /// Piezas del kit marcadas + extras, fusionando repetidas (suma de cantidades).
  List<UsedPart> toUsedParts() {
    final kitParts = [
      for (final item in _kit?.items ?? const <VisitKitItem>[])
        if (isChecked(item.itemId))
          UsedPart(itemId: item.itemId, quantity: quantityOf(item.itemId)),
    ];
    final extraParts = [
      for (final e in _extras) UsedPart(itemId: e.itemId, quantity: e.quantity),
    ];
    return mergeUsedParts([...kitParts, ...extraParts]);
  }
}
