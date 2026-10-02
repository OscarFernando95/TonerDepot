// Espejos del módulo de Inventario (staff): InventoryItemDto, InventoryLocationDto, StockRowDto,
// InventoryMovementDto (backend/src/Toner.Application/Inventory/Dtos/InventoryDtos.cs) y la lógica pura de
// formularios/paginación que usa la pantalla. Sin precios: `unitCost` existe en el backend pero se ignora a
// propósito (decisión de producto: no se capturan ni muestran costos por ahora).

/// Categorías de ítem (valor del backend, etiqueta en español). Mismo orden que la web.
const inventoryCategories = <String, String>{
  'ConsumibleBase': 'Consumible base',
  'Repuesto': 'Repuesto',
  'Toner': 'Tóner',
};

String inventoryCategoryLabel(String category) =>
    inventoryCategories[category] ?? category;

/// Etiquetas de los tipos de movimiento.
const movementTypeLabels = <String, String>{
  'Entrada': 'Entrada',
  'TraspasoSalida': 'Traspaso (sale)',
  'TraspasoEntrada': 'Traspaso (entra)',
  'Consumo': 'Consumo',
  'Ajuste': 'Ajuste',
};

String movementTypeLabel(String type) => movementTypeLabels[type] ?? type;

class InventoryItem {
  const InventoryItem({
    required this.id,
    required this.name,
    required this.category,
    this.unit,
    required this.minimumStock,
    required this.isActive,
  });

  final String id;
  final String name;
  final String category;
  final String? unit;
  final int minimumStock;
  final bool isActive;

  factory InventoryItem.fromJson(Map<String, dynamic> json) => InventoryItem(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    category: json['category'] as String? ?? '',
    unit: json['unit'] as String?,
    minimumStock: (json['minimumStock'] as num?)?.toInt() ?? 0,
    isActive: json['isActive'] as bool? ?? true,
  );
}

class InventoryLocation {
  const InventoryLocation({
    required this.id,
    required this.kind,
    required this.name,
    this.zoneId,
    this.address,
    this.cityId,
    this.cityName,
  });

  final String id;

  /// Principal | Zona
  final String kind;
  final String name;
  final String? zoneId;
  final String? address;
  final String? cityId;
  final String? cityName;

  bool get isMain => kind == 'Principal';
  bool get isZone => kind == 'Zona';

  factory InventoryLocation.fromJson(Map<String, dynamic> json) =>
      InventoryLocation(
        id: json['id'] as String,
        kind: json['kind'] as String? ?? '',
        name: json['name'] as String? ?? '',
        zoneId: json['zoneId'] as String?,
        address: json['address'] as String?,
        cityId: json['cityId'] as String?,
        cityName: json['cityName'] as String?,
      );
}

class StockRow {
  const StockRow({
    required this.itemId,
    required this.itemName,
    required this.category,
    required this.locationId,
    required this.locationName,
    required this.quantity,
    required this.minimumStock,
    required this.isLow,
  });

  final String itemId;
  final String itemName;
  final String category;
  final String locationId;
  final String locationName;
  final int quantity;
  final int minimumStock;

  /// Saldo menor o igual al mínimo del ítem (con mínimo > 0), o negativo.
  final bool isLow;

  bool get isNegative => quantity < 0;

  /// Clave única de la fila (ítem + ubicación).
  String get key => '$itemId|$locationId';

  factory StockRow.fromJson(Map<String, dynamic> json) => StockRow(
    itemId: json['itemId'] as String,
    itemName: json['itemName'] as String? ?? '',
    category: json['category'] as String? ?? '',
    locationId: json['locationId'] as String,
    locationName: json['locationName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 0,
    minimumStock: (json['minimumStock'] as num?)?.toInt() ?? 0,
    isLow: json['isLow'] as bool? ?? false,
  );
}

class InventoryMovement {
  const InventoryMovement({
    required this.id,
    required this.itemId,
    required this.itemName,
    required this.locationId,
    required this.locationName,
    required this.type,
    required this.delta,
    this.notes,
    this.createdByUserName,
    required this.occurredAt,
  });

  final String id;
  final String itemId;
  final String itemName;
  final String locationId;
  final String locationName;

  /// Entrada | TraspasoSalida | TraspasoEntrada | Consumo | Ajuste
  final String type;
  final int delta;
  final String? notes;
  final String? createdByUserName;
  final DateTime occurredAt;

  String get typeLabel => movementTypeLabel(type);

  /// "+5" / "-3" (el menos usa el signo ASCII; el cero va sin signo).
  String get signedQuantity => formatSignedQuantity(delta);

  factory InventoryMovement.fromJson(Map<String, dynamic> json) =>
      InventoryMovement(
        id: json['id'] as String,
        itemId: json['itemId'] as String? ?? '',
        itemName: json['itemName'] as String? ?? '',
        locationId: json['locationId'] as String? ?? '',
        locationName: json['locationName'] as String? ?? '',
        type: json['type'] as String? ?? '',
        delta: (json['delta'] as num?)?.toInt() ?? 0,
        notes: json['notes'] as String?,
        createdByUserName: json['createdByUserName'] as String?,
        occurredAt:
            DateTime.tryParse(json['occurredAt'] as String? ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      );
}

String formatSignedQuantity(int delta) => delta > 0 ? '+$delta' : '$delta';

// ── Formularios ─────────────────────────────────────────────────────────────────────────────────

/// Límites del backend (validators de FluentValidation).
const inventoryMaxQuantity = 100000;
const inventoryItemNameMax = 150;
const inventoryUnitMax = 30;
const inventoryNotesMax = 500;
const inventoryLocationNameMax = 150;
const inventoryLocationAddressMax = 300;

/// Formulario de ítem del catálogo. Sin precio.
class ItemDraft {
  const ItemDraft({
    this.name = '',
    this.category = 'ConsumibleBase',
    this.unit = '',
    this.minimumStock = 0,
    this.isActive = true,
  });

  factory ItemDraft.fromItem(InventoryItem item) => ItemDraft(
    name: item.name,
    category: item.category,
    unit: item.unit ?? '',
    minimumStock: item.minimumStock,
    isActive: item.isActive,
  );

  final String name;
  final String category;
  final String unit;
  final int minimumStock;
  final bool isActive;

  /// Mensaje de error o null si es válido.
  String? validate() {
    final trimmed = name.trim();
    if (trimmed.isEmpty) return 'El nombre es obligatorio.';
    if (trimmed.length > inventoryItemNameMax) {
      return 'El nombre no puede pasar de $inventoryItemNameMax caracteres.';
    }
    if (!inventoryCategories.containsKey(category)) {
      return 'Elige una categoría válida.';
    }
    if (unit.trim().length > inventoryUnitMax) {
      return 'La unidad no puede pasar de $inventoryUnitMax caracteres.';
    }
    if (minimumStock < 0) return 'El stock mínimo no puede ser negativo.';
    return null;
  }

  /// Cuerpo de POST/PUT /inventory/items. `isActive` solo viaja al editar. Nunca incluye `unitCost`.
  Map<String, dynamic> toJson({required bool editing}) => {
    'name': name.trim(),
    'category': category,
    'unit': unit.trim().isEmpty ? null : unit.trim(),
    'minimumStock': minimumStock,
    if (editing) 'isActive': isActive,
  };
}

enum InventoryOperation { entry, transfer, adjust }

extension InventoryOperationText on InventoryOperation {
  String get title => switch (this) {
    InventoryOperation.entry => 'Registrar entrada',
    InventoryOperation.transfer => 'Traspasar entre ubicaciones',
    InventoryOperation.adjust => 'Ajustar saldo',
  };
}

/// Formulario de entrada / traspaso / ajuste. [locationId] es el origen en un traspaso.
class OperationDraft {
  const OperationDraft({
    required this.kind,
    this.itemId = '',
    this.locationId = '',
    this.toLocationId = '',
    this.quantity = 1,
    this.delta = 1,
    this.notes = '',
  });

  final InventoryOperation kind;
  final String itemId;
  final String locationId;
  final String toLocationId;
  final int quantity;
  final int delta;
  final String notes;

  OperationDraft copyWith({
    String? itemId,
    String? locationId,
    String? toLocationId,
    int? quantity,
    int? delta,
    String? notes,
  }) => OperationDraft(
    kind: kind,
    itemId: itemId ?? this.itemId,
    locationId: locationId ?? this.locationId,
    toLocationId: toLocationId ?? this.toLocationId,
    quantity: quantity ?? this.quantity,
    delta: delta ?? this.delta,
    notes: notes ?? this.notes,
  );

  /// Mensaje de error o null si es válido (mismas reglas que la web y el backend).
  String? validate() {
    if (itemId.isEmpty) return 'Elige un ítem.';
    if (locationId.isEmpty) {
      return kind == InventoryOperation.transfer
          ? 'Elige la ubicación de origen.'
          : 'Elige la ubicación.';
    }
    if (notes.trim().length > inventoryNotesMax) {
      return 'Las notas no pueden pasar de $inventoryNotesMax caracteres.';
    }
    switch (kind) {
      case InventoryOperation.transfer:
        if (toLocationId.isEmpty) return 'Elige la ubicación de destino.';
        if (toLocationId == locationId) {
          return 'El origen y el destino deben ser distintos.';
        }
        return _validQuantity(quantity);
      case InventoryOperation.adjust:
        if (delta == 0) return 'El ajuste no puede ser 0.';
        if (delta.abs() > inventoryMaxQuantity) {
          return 'El ajuste no puede pasar de $inventoryMaxQuantity.';
        }
        if (notes.trim().isEmpty) return 'El motivo del ajuste es obligatorio.';
        return null;
      case InventoryOperation.entry:
        return _validQuantity(quantity);
    }
  }

  static String? _validQuantity(int quantity) {
    if (quantity < 1) return 'La cantidad debe ser al menos 1.';
    if (quantity > inventoryMaxQuantity) {
      return 'La cantidad no puede pasar de $inventoryMaxQuantity.';
    }
    return null;
  }

  bool get isValid => validate() == null;

  /// Cuerpo del POST correspondiente a [kind].
  Map<String, dynamic> toJson() {
    final trimmedNotes = notes.trim();
    final optionalNotes = trimmedNotes.isEmpty ? null : trimmedNotes;
    switch (kind) {
      case InventoryOperation.entry:
        return {
          'locationId': locationId,
          'itemId': itemId,
          'quantity': quantity,
          'notes': optionalNotes,
        };
      case InventoryOperation.transfer:
        return {
          'itemId': itemId,
          'fromLocationId': locationId,
          'toLocationId': toLocationId,
          'quantity': quantity,
          'notes': optionalNotes,
        };
      case InventoryOperation.adjust:
        return {
          'locationId': locationId,
          'itemId': itemId,
          'delta': delta,
          'notes': trimmedNotes,
        };
    }
  }
}

/// Formulario de la sede principal.
class MainLocationDraft {
  const MainLocationDraft({this.name = '', this.address = '', this.cityId});

  factory MainLocationDraft.fromLocation(InventoryLocation? location) =>
      MainLocationDraft(
        name: location?.name ?? '',
        address: location?.address ?? '',
        cityId: location?.cityId,
      );

  final String name;
  final String address;
  final String? cityId;

  String? validate() {
    final trimmed = name.trim();
    if (trimmed.isEmpty) return 'El nombre es obligatorio.';
    if (trimmed.length > inventoryLocationNameMax) {
      return 'El nombre no puede pasar de $inventoryLocationNameMax caracteres.';
    }
    if (address.trim().length > inventoryLocationAddressMax) {
      return 'La dirección no puede pasar de $inventoryLocationAddressMax caracteres.';
    }
    return null;
  }

  Map<String, dynamic> toJson() => {
    'name': name.trim(),
    'address': address.trim().isEmpty ? null : address.trim(),
    'cityId': cityId,
  };
}

// ── Consultas y paginación ──────────────────────────────────────────────────────────────────────

/// Query de GET /inventory/stock. Omite los filtros vacíos (igual que la web: `|| undefined`).
Map<String, dynamic> buildStockQuery({
  String? locationId,
  String? category,
  bool onlyLow = false,
  int page = 1,
  int pageSize = 20,
}) => {
  if (locationId != null && locationId.isNotEmpty) 'locationId': locationId,
  if (category != null && category.isNotEmpty) 'category': category,
  if (onlyLow) 'onlyLow': true,
  'page': page,
  'pageSize': pageSize,
};

/// Query de GET /inventory/items.
Map<String, dynamic> buildItemsQuery({
  String? search,
  String? category,
  bool activeOnly = false,
  int page = 1,
  int pageSize = 20,
}) {
  final trimmed = search?.trim();
  return {
    if (trimmed != null && trimmed.isNotEmpty) 'search': trimmed,
    if (category != null && category.isNotEmpty) 'category': category,
    if (activeOnly) 'activeOnly': true,
    'page': page,
    'pageSize': pageSize,
  };
}

/// Query de GET /inventory/movements (cursor/keyset).
Map<String, dynamic> buildMovementsQuery({
  String? locationId,
  String? cursor,
  int pageSize = 25,
}) => {
  if (locationId != null && locationId.isNotEmpty) 'locationId': locationId,
  if (cursor != null && cursor.isNotEmpty) 'cursor': cursor,
  'pageSize': pageSize,
};

const maxPageSize = 200;

/// Tamaño de página para recargar en silencio lo ya cargado de golpe (página 1 con todo lo visible), con tope del
/// servidor. Nunca menor que [base].
int silentReloadPageSize(int loaded, int base) =>
    loaded <= base ? base : (loaded > maxPageSize ? maxPageSize : loaded);

/// Une una página nueva a la lista existente evitando duplicados por [keyOf] (un registro nuevo puede correr los
/// límites entre páginas). Conserva el orden: primero lo existente, luego lo nuevo que no estaba.
List<T> mergePage<T>(
  List<T> existing,
  List<T> incoming,
  String Function(T) keyOf,
) {
  final seen = {for (final e in existing) keyOf(e)};
  return [
    ...existing,
    for (final e in incoming)
      if (seen.add(keyOf(e))) e,
  ];
}

/// Cursor a usar para la página siguiente: solo si el servidor dice que hay más y entregó cursor.
String? nextCursorOf({required bool hasMore, String? nextCursor}) =>
    hasMore && nextCursor != null && nextCursor.isNotEmpty ? nextCursor : null;
