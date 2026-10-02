import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/inventory_admin.dart';

void main() {
  group('parseo', () {
    test('InventoryItem.fromJson ignora unitCost y tolera faltantes', () {
      final item = InventoryItem.fromJson({
        'id': 'i1',
        'name': 'Fusor',
        'category': 'Repuesto',
        'unitCost': 12.5,
        'minimumStock': 3,
      });
      expect(item.name, 'Fusor');
      expect(item.unit, isNull);
      expect(item.minimumStock, 3);
      expect(item.isActive, isTrue);
      expect(inventoryCategoryLabel(item.category), 'Repuesto');
    });

    test('InventoryLocation.fromJson distingue Principal y Zona', () {
      final main = InventoryLocation.fromJson({
        'id': 'l1',
        'kind': 'Principal',
        'name': 'Bodega',
        'address': 'Calle 1',
        'cityId': 'c1',
        'cityName': 'Bogotá',
      });
      final zone = InventoryLocation.fromJson({
        'id': 'l2',
        'kind': 'Zona',
        'name': 'Norte',
        'zoneId': 'z1',
      });
      expect(main.isMain, isTrue);
      expect(main.cityName, 'Bogotá');
      expect(zone.isZone, isTrue);
      expect(zone.address, isNull);
    });

    test('StockRow.fromJson: negativo, bajo y clave', () {
      final row = StockRow.fromJson({
        'itemId': 'i1',
        'itemName': 'Tóner negro',
        'category': 'Toner',
        'locationId': 'l1',
        'locationName': 'Bodega',
        'quantity': -2,
        'minimumStock': 5,
        'isLow': true,
      });
      expect(row.isNegative, isTrue);
      expect(row.isLow, isTrue);
      expect(row.key, 'i1|l1');
      expect(inventoryCategoryLabel(row.category), 'Tóner');
    });

    test('InventoryMovement.fromJson: etiqueta y cantidad con signo', () {
      final json = {
        'id': 'm1',
        'itemId': 'i1',
        'itemName': 'Fusor',
        'locationId': 'l1',
        'locationName': 'Bodega',
        'type': 'TraspasoSalida',
        'delta': -4,
        'notes': 'a Norte',
        'createdByUserName': 'Ana',
        'occurredAt': '2026-09-28T14:30:00Z',
      };
      final out = InventoryMovement.fromJson(json);
      expect(out.typeLabel, 'Traspaso (sale)');
      expect(out.signedQuantity, '-4');
      expect(out.occurredAt.toUtc().hour, 14);
      final inn = InventoryMovement.fromJson({
        ...json,
        'type': 'TraspasoEntrada',
        'delta': 4,
      });
      expect(inn.typeLabel, 'Traspaso (entra)');
      expect(inn.signedQuantity, '+4');
      expect(movementTypeLabel('Consumo'), 'Consumo');
      expect(movementTypeLabel('Ajuste'), 'Ajuste');
      expect(movementTypeLabel('Entrada'), 'Entrada');
      expect(formatSignedQuantity(0), '0');
    });

    test('InventoryMovement.fromJson tolera fecha inválida', () {
      final m = InventoryMovement.fromJson({'id': 'm', 'occurredAt': 'x'});
      expect(m.occurredAt.millisecondsSinceEpoch, 0);
    });
  });

  group('ItemDraft', () {
    test('exige nombre y categoría válida', () {
      expect(const ItemDraft().validate(), isNotNull);
      expect(const ItemDraft(name: '  ').validate(), isNotNull);
      expect(const ItemDraft(name: 'Fusor').validate(), isNull);
      expect(
        const ItemDraft(name: 'Fusor', category: 'Otra').validate(),
        isNotNull,
      );
      expect(
        const ItemDraft(name: 'Fusor', minimumStock: -1).validate(),
        isNotNull,
      );
      expect(ItemDraft(name: 'x' * 151).validate(), isNotNull);
      expect(ItemDraft(name: 'Fusor', unit: 'u' * 31).validate(), isNotNull);
    });

    test('toJson nunca envía precio; isActive solo al editar', () {
      const draft = ItemDraft(
        name: ' Fusor ',
        category: 'Repuesto',
        unit: '  ',
        minimumStock: 2,
        isActive: false,
      );
      final create = draft.toJson(editing: false);
      expect(create, {
        'name': 'Fusor',
        'category': 'Repuesto',
        'unit': null,
        'minimumStock': 2,
      });
      final edit = draft.toJson(editing: true);
      expect(edit['isActive'], false);
      expect(create.containsKey('unitCost'), isFalse);
      expect(edit.containsKey('unitCost'), isFalse);
    });

    test('fromItem copia los campos', () {
      final d = ItemDraft.fromItem(
        const InventoryItem(
          id: 'i',
          name: 'Cinta',
          category: 'ConsumibleBase',
          unit: 'und',
          minimumStock: 4,
          isActive: false,
        ),
      );
      expect(d.unit, 'und');
      expect(d.isActive, isFalse);
      expect(d.minimumStock, 4);
    });
  });

  group('OperationDraft', () {
    const entry = OperationDraft(
      kind: InventoryOperation.entry,
      itemId: 'i1',
      locationId: 'l1',
    );

    test('entrada: ítem, ubicación y cantidad 1..100000', () {
      expect(entry.isValid, isTrue);
      expect(
        const OperationDraft(
          kind: InventoryOperation.entry,
          locationId: 'l1',
        ).validate(),
        isNotNull,
      );
      expect(entry.copyWith(locationId: '').validate(), isNotNull);
      expect(entry.copyWith(quantity: 0).validate(), isNotNull);
      expect(entry.copyWith(quantity: 100001).validate(), isNotNull);
      expect(entry.copyWith(quantity: 100000).isValid, isTrue);
    });

    test('traspaso: origen != destino', () {
      const t = OperationDraft(
        kind: InventoryOperation.transfer,
        itemId: 'i1',
        locationId: 'l1',
      );
      expect(t.validate(), 'Elige la ubicación de destino.');
      expect(
        t.copyWith(toLocationId: 'l1').validate(),
        'El origen y el destino deben ser distintos.',
      );
      expect(t.copyWith(toLocationId: 'l2').isValid, isTrue);
      expect(t.copyWith(toLocationId: 'l2', quantity: 0).isValid, isFalse);
    });

    test('ajuste exige motivo y delta distinto de 0', () {
      const a = OperationDraft(
        kind: InventoryOperation.adjust,
        itemId: 'i1',
        locationId: 'l1',
      );
      expect(a.validate(), 'El motivo del ajuste es obligatorio.');
      expect(a.copyWith(notes: '   ').isValid, isFalse);
      expect(a.copyWith(notes: 'Conteo físico').isValid, isTrue);
      expect(a.copyWith(notes: 'Merma', delta: -3).isValid, isTrue);
      expect(a.copyWith(notes: 'x', delta: 0).validate(), isNotNull);
      expect(a.copyWith(notes: 'x', delta: -100001).validate(), isNotNull);
    });

    test('notas de más de 500 caracteres no son válidas', () {
      expect(entry.copyWith(notes: 'n' * 501).isValid, isFalse);
      expect(entry.copyWith(notes: 'n' * 500).isValid, isTrue);
    });

    test('toJson por tipo de operación', () {
      expect(entry.copyWith(quantity: 5, notes: ' ').toJson(), {
        'locationId': 'l1',
        'itemId': 'i1',
        'quantity': 5,
        'notes': null,
      });
      const t = OperationDraft(
        kind: InventoryOperation.transfer,
        itemId: 'i1',
        locationId: 'l1',
        toLocationId: 'l2',
        quantity: 3,
        notes: 'a zona',
      );
      expect(t.toJson(), {
        'itemId': 'i1',
        'fromLocationId': 'l1',
        'toLocationId': 'l2',
        'quantity': 3,
        'notes': 'a zona',
      });
      const a = OperationDraft(
        kind: InventoryOperation.adjust,
        itemId: 'i1',
        locationId: 'l1',
        delta: -2,
        notes: ' Merma ',
      );
      expect(a.toJson(), {
        'locationId': 'l1',
        'itemId': 'i1',
        'delta': -2,
        'notes': 'Merma',
      });
    });
  });

  group('MainLocationDraft', () {
    test('exige nombre y recorta los campos', () {
      expect(const MainLocationDraft().validate(), isNotNull);
      expect(const MainLocationDraft(name: 'Sede').validate(), isNull);
      expect(
        MainLocationDraft(name: 'Sede', address: 'a' * 301).validate(),
        isNotNull,
      );
      expect(
        const MainLocationDraft(
          name: ' Sede ',
          address: ' ',
          cityId: 'c1',
        ).toJson(),
        {'name': 'Sede', 'address': null, 'cityId': 'c1'},
      );
    });

    test('fromLocation(null) arma un formulario vacío', () {
      final d = MainLocationDraft.fromLocation(null);
      expect(d.name, '');
      expect(d.cityId, isNull);
    });
  });

  group('consultas', () {
    test('buildStockQuery omite filtros vacíos', () {
      expect(buildStockQuery(), {'page': 1, 'pageSize': 20});
      expect(
        buildStockQuery(
          locationId: 'l1',
          category: 'Toner',
          onlyLow: true,
          page: 3,
          pageSize: 40,
        ),
        {
          'locationId': 'l1',
          'category': 'Toner',
          'onlyLow': true,
          'page': 3,
          'pageSize': 40,
        },
      );
      expect(buildStockQuery(locationId: '', category: ''), {
        'page': 1,
        'pageSize': 20,
      });
    });

    test('buildItemsQuery recorta la búsqueda y soporta activeOnly', () {
      expect(buildItemsQuery(search: '  fus '), {
        'search': 'fus',
        'page': 1,
        'pageSize': 20,
      });
      expect(buildItemsQuery(search: '   '), {'page': 1, 'pageSize': 20});
      expect(buildItemsQuery(search: 'x', activeOnly: true, pageSize: 20), {
        'search': 'x',
        'activeOnly': true,
        'page': 1,
        'pageSize': 20,
      });
    });

    test('buildMovementsQuery incluye el cursor solo si existe', () {
      expect(buildMovementsQuery(), {'pageSize': 25});
      expect(buildMovementsQuery(locationId: 'l1', cursor: 'abc'), {
        'locationId': 'l1',
        'cursor': 'abc',
        'pageSize': 25,
      });
      expect(buildMovementsQuery(cursor: ''), {'pageSize': 25});
    });
  });

  group('paginación', () {
    test('nextCursorOf solo devuelve cursor si hay más', () {
      expect(nextCursorOf(hasMore: true, nextCursor: 'c2'), 'c2');
      expect(nextCursorOf(hasMore: false, nextCursor: 'c2'), isNull);
      expect(nextCursorOf(hasMore: true, nextCursor: null), isNull);
      expect(nextCursorOf(hasMore: true, nextCursor: ''), isNull);
    });

    test('mergePage conserva el orden y descarta duplicados', () {
      final merged = mergePage<String>(
        ['a', 'b', 'c'],
        ['c', 'd', 'a', 'e'],
        (s) => s,
      );
      expect(merged, ['a', 'b', 'c', 'd', 'e']);
      expect(mergePage<String>([], ['x', 'x'], (s) => s), ['x']);
    });

    test('silentReloadPageSize trae todo lo visible con tope del servidor', () {
      expect(silentReloadPageSize(0, 20), 20);
      expect(silentReloadPageSize(20, 20), 20);
      expect(silentReloadPageSize(60, 20), 60);
      expect(silentReloadPageSize(500, 20), maxPageSize);
    });
  });
}
