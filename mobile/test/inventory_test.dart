import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/inventory.dart';
import 'package:toner_tecnico/models/paged_result.dart';
import 'package:toner_tecnico/models/technician_status.dart';
import 'package:toner_tecnico/services/technician_api.dart';

VisitKit _kit() => VisitKit.fromJson({
  'assetId': 'a1',
  'locationId': 'l1',
  'locationName': 'Zona Norte',
  'usesMainWarehouse': false,
  'items': [
    {
      'itemId': 'fusor',
      'itemName': 'Fusor',
      'category': 'ConsumibleBase',
      'groupName': 'Unidad fusora',
      'quantity': 1,
      'stock': 5,
    },
    {
      'itemId': 'presor',
      'itemName': 'Presor',
      'category': 'ConsumibleBase',
      'groupName': 'Unidad fusora',
      'quantity': 2,
      'stock': 1,
    },
    {
      'itemId': 'cilindro',
      'itemName': 'Cilindro',
      'category': 'ConsumibleBase',
      'groupName': 'Unidad de imagen',
      'quantity': 1,
      'stock': 3,
    },
  ],
});

void main() {
  group('parseo', () {
    test('VisitKit.fromJson y agrupación por groupName', () {
      final kit = _kit();
      expect(kit.locationName, 'Zona Norte');
      expect(kit.items, hasLength(3));
      final groups = kit.itemsByGroup;
      expect(groups.keys, ['Unidad fusora', 'Unidad de imagen']);
      expect(groups['Unidad fusora']!.map((i) => i.itemId), [
        'fusor',
        'presor',
      ]);
    });

    test('VisitKit.fromJson tolera campos ausentes', () {
      final kit = VisitKit.fromJson({});
      expect(kit.items, isEmpty);
      expect(kit.assetId, isNull);
      expect(kit.usesMainWarehouse, isFalse);
      expect(kit.locationName, '');
    });

    test('PartOption.fromJson con unit ausente', () {
      final p = PartOption.fromJson({
        'itemId': 'i',
        'name': 'Tóner TN-1',
        'category': 'Toner',
        'stock': 4,
      });
      expect(p.unit, isNull);
      expect(p.stock, 4);
      expect(p.category, 'Toner');
    });

    test('TonerEntry.fromJson con opcionales ausentes y presentes', () {
      final e = TonerEntry.fromJson({
        'movementId': 'm',
        'itemId': 'i',
        'itemName': 'Tóner',
        'quantity': 2,
        'occurredAt': '2026-10-01T15:30:00Z',
      });
      expect(e.occurredAt.isUtc, isTrue);
      expect(e.counterValue, isNull);
      expect(e.stockWarning, isNull);
      final w = TonerEntry.fromJson({
        'movementId': 'm',
        'itemId': 'i',
        'itemName': 'Tóner',
        'quantity': 1,
        'occurredAt': '2026-10-01T15:30:00Z',
        'counterValue': 1200,
        'stockWarning': 'quedó en -1',
      });
      expect(w.counterValue, 1200);
      expect(w.stockWarning, 'quedó en -1');
    });

    test('TechnicianSelfStatus lee stockWarnings (y tolera su ausencia)', () {
      final base = {
        'technicianId': 't',
        'status': 'Disponible',
        'activeServiceTicketId': null,
        'activeMaintenanceOrderId': null,
        'activeAssetInstallationId': null,
        'checkedInAt': null,
      };
      expect(TechnicianSelfStatus.fromJson(base).stockWarnings, isEmpty);
      expect(
        TechnicianSelfStatus.fromJson({
          ...base,
          'stockWarnings': ['Stock insuficiente de X'],
        }).stockWarnings,
        ['Stock insuficiente de X'],
      );
    });

    test('PagedResult conserva nextCursor', () {
      final page = PagedResult<int>.fromJson({
        'items': <Map<String, dynamic>>[{}, {}],
        'pageSize': 2,
        'hasMore': true,
        'nextCursor': 'abc',
      }, (j) => 0);
      expect(page.nextCursor, 'abc');
    });
  });

  group('mergeUsedParts', () {
    test('suma duplicados, descarta cantidades <= 0 y conserva el orden', () {
      final merged = mergeUsedParts(const [
        UsedPart(itemId: 'a', quantity: 1),
        UsedPart(itemId: 'b', quantity: 2),
        UsedPart(itemId: 'a', quantity: 3),
        UsedPart(itemId: 'c', quantity: 0),
      ]);
      expect(merged, const [
        UsedPart(itemId: 'a', quantity: 4),
        UsedPart(itemId: 'b', quantity: 2),
      ]);
    });
  });

  group('PartsSelection', () {
    test('kit: todo marcado con cantidades por defecto', () {
      final s = PartsSelection()..setKit(_kit());
      expect(s.toUsedParts(), const [
        UsedPart(itemId: 'fusor', quantity: 1),
        UsedPart(itemId: 'presor', quantity: 2),
        UsedPart(itemId: 'cilindro', quantity: 1),
      ]);
    });

    test('desmarcar excluye la pieza; cantidad editable', () {
      final s = PartsSelection()..setKit(_kit());
      s.toggle('presor', false);
      s.setQuantity('fusor', 3);
      expect(s.toUsedParts(), const [
        UsedPart(itemId: 'fusor', quantity: 3),
        UsedPart(itemId: 'cilindro', quantity: 1),
      ]);
    });

    test('extra que repite una pieza del kit se fusiona sumando', () {
      final s = PartsSelection()..setKit(_kit());
      s.addExtra(
        const PartOption(
          itemId: 'fusor',
          name: 'Fusor',
          category: 'Repuesto',
          stock: 5,
        ),
        quantity: 2,
      );
      s.addExtra(
        const PartOption(
          itemId: 'rodillo',
          name: 'Rodillo',
          category: 'Repuesto',
          stock: 0,
        ),
      );
      s.addExtra(
        const PartOption(
          itemId: 'rodillo',
          name: 'Rodillo',
          category: 'Repuesto',
          stock: 0,
        ),
      );
      expect(s.extras, hasLength(2));
      expect(
        s.toUsedParts().firstWhere((p) => p.itemId == 'fusor').quantity,
        3,
      );
      expect(
        s.toUsedParts().firstWhere((p) => p.itemId == 'rodillo').quantity,
        2,
      );
      s.removeExtra('rodillo');
      expect(s.toUsedParts().any((p) => p.itemId == 'rodillo'), isFalse);
    });

    test('sin kit (ticket) solo salen los extras; sin nada, lista vacía', () {
      final s = PartsSelection();
      expect(s.toUsedParts(), isEmpty);
      s.addExtra(
        const PartOption(
          itemId: 'x',
          name: 'X',
          category: 'Repuesto',
          stock: 1,
        ),
      );
      expect(s.toUsedParts(), const [UsedPart(itemId: 'x', quantity: 1)]);
    });

    test('kitItemShort: solo si está marcada y el saldo no alcanza', () {
      final s = PartsSelection()..setKit(_kit());
      final presor = _kit().items[1]; // cantidad 2, stock 1
      expect(s.kitItemShort(presor), isTrue);
      s.setQuantity('presor', 1);
      expect(s.kitItemShort(presor), isFalse);
      s.setQuantity('presor', 2);
      s.toggle('presor', false);
      expect(s.kitItemShort(presor), isFalse);
    });
  });

  group('CheckOutRequest.toJson', () {
    test('envía parts solo si hay piezas', () {
      expect(
        CheckOutRequest(resolved: true).toJson().containsKey('parts'),
        isFalse,
      );
      final json = CheckOutRequest(
        resolved: true,
        parts: const [UsedPart(itemId: 'a', quantity: 2)],
      ).toJson();
      expect(json['parts'], [
        {'itemId': 'a', 'quantity': 2},
      ]);
    });
  });

  group('resolveTonerOccurredAt', () {
    final now = DateTime(2026, 10, 1, 14, 5);
    test('sin fecha o con hoy: el momento del registro, en UTC', () {
      expect(resolveTonerOccurredAt(null, now), now.toUtc());
      expect(resolveTonerOccurredAt(DateTime(2026, 10, 1), now), now.toUtc());
      expect(resolveTonerOccurredAt(null, now).isUtc, isTrue);
    });
    test('día pasado: esa fecha con la hora actual', () {
      final r = resolveTonerOccurredAt(DateTime(2026, 9, 28), now);
      expect(r, DateTime(2026, 9, 28, 14, 5).toUtc());
      expect(r.isUtc, isTrue);
    });
  });
}
