import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/base_kit.dart';

ModelKitItem _eff(
  String id, {
  String group = 'Unidad',
  int qty = 1,
  String source = 'Marca',
  bool excluded = false,
}) => ModelKitItem(
  itemId: id,
  itemName: 'Item $id',
  groupName: group,
  quantity: qty,
  source: source,
  excluded: excluded,
);

BaseKitItem _brand(String id, {String group = 'Unidad', int qty = 1}) =>
    BaseKitItem(
      itemId: id,
      itemName: 'Item $id',
      groupName: group,
      quantity: qty,
    );

void main() {
  group('BaseKitLogic.buildModelRows', () {
    test('marca como de la marca solo lo que la marca tiene', () {
      final rows = BaseKitLogic.buildModelRows(
        [_eff('a'), _eff('b', source: 'Modelo')],
        [_brand('a', group: 'Fusor', qty: 2)],
      );
      expect(rows[0].fromBrand, isTrue);
      expect(rows[0].brandGroup, 'Fusor');
      expect(rows[0].brandQuantity, 2);
      expect(rows[1].fromBrand, isFalse);
    });
  });

  group('BaseKitLogic.buildOverrides', () {
    test('lo heredado sin tocar no genera ajuste', () {
      final rows = BaseKitLogic.buildModelRows(
        [_eff('a', group: 'Fusor', qty: 2)],
        [_brand('a', group: 'Fusor', qty: 2)],
      );
      expect(BaseKitLogic.buildOverrides(rows), isEmpty);
    });

    test('excluir una fila de la marca manda solo excluded', () {
      final rows = BaseKitLogic.buildModelRows(
        [_eff('a', group: 'Fusor', qty: 2)],
        [_brand('a', group: 'Fusor', qty: 2)],
      ).map((r) => r.copyWith(excluded: true)).toList();
      expect(BaseKitLogic.buildOverrides(rows), [
        {'itemId': 'a', 'excluded': true, 'groupName': null, 'quantity': null},
      ]);
    });

    test('grupo y cantidad solo se mandan si difieren de la marca', () {
      final rows = BaseKitLogic.buildModelRows(
        [
          _eff('a', group: 'Fusor', qty: 5),
          _eff('b', group: 'Rodillo', qty: 1),
        ],
        [
          _brand('a', group: 'Fusor', qty: 2),
          _brand('b', group: 'Cilindro', qty: 1),
        ],
      );
      expect(BaseKitLogic.buildOverrides(rows), [
        {'itemId': 'a', 'excluded': false, 'groupName': null, 'quantity': 5},
        {
          'itemId': 'b',
          'excluded': false,
          'groupName': 'Rodillo',
          'quantity': null,
        },
      ]);
    });

    test('comparar el grupo ignora espacios alrededor', () {
      final rows = BaseKitLogic.buildModelRows(
        [_eff('a', group: ' Fusor ', qty: 2)],
        [_brand('a', group: 'Fusor', qty: 2)],
      );
      expect(BaseKitLogic.buildOverrides(rows), isEmpty);
    });

    test('un ítem solo del modelo manda siempre grupo y cantidad', () {
      final rows = BaseKitLogic.buildModelRows([
        _eff('z', group: ' Kit extra ', qty: 3, source: 'Modelo'),
      ], const []);
      expect(BaseKitLogic.buildOverrides(rows), [
        {
          'itemId': 'z',
          'excluded': false,
          'groupName': 'Kit extra',
          'quantity': 3,
        },
      ]);
    });

    test('mezcla: heredado, ajustado, excluido y solo-modelo', () {
      final rows = BaseKitLogic.buildModelRows(
        [
          _eff('a'),
          _eff('b', qty: 4),
          _eff('c', excluded: true),
          _eff('d', group: 'Otro', qty: 2, source: 'Modelo'),
        ],
        [_brand('a'), _brand('b'), _brand('c')],
      );
      final overrides = BaseKitLogic.buildOverrides(rows);
      expect(overrides.map((o) => o['itemId']), ['b', 'c', 'd']);
      expect(overrides[1]['excluded'], true);
      expect(overrides[2]['groupName'], 'Otro');
    });

    test('la cantidad enviada se acota a 1..100', () {
      final rows = [
        const ModelKitRow(
          itemId: 'z',
          itemName: 'Z',
          groupName: 'G',
          quantity: 500,
          excluded: false,
          fromBrand: false,
        ),
      ];
      expect(BaseKitLogic.buildOverrides(rows).single['quantity'], 100);
    });
  });

  group('ModelKitRow.origin', () {
    ModelKitRow row({
      bool fromBrand = true,
      String group = 'G',
      int qty = 1,
      bool excluded = false,
    }) => ModelKitRow(
      itemId: 'a',
      itemName: 'A',
      groupName: group,
      quantity: qty,
      excluded: excluded,
      fromBrand: fromBrand,
      brandGroup: 'G',
      brandQuantity: 1,
    );

    test('Heredado, Ajustado y Solo este modelo', () {
      expect(row().origin, ModelKitOrigin.inherited);
      expect(row(qty: 2).origin, ModelKitOrigin.adjusted);
      expect(row(group: 'X').origin, ModelKitOrigin.adjusted);
      expect(row(excluded: true).origin, ModelKitOrigin.adjusted);
      expect(row(fromBrand: false).origin, ModelKitOrigin.modelOnly);
      expect(ModelKitOrigin.modelOnly.label, 'Solo este modelo');
    });

    test('grupo vacío solo bloquea si la fila está incluida', () {
      expect(row(group: ' ').hasMissingGroup, isTrue);
      expect(row(group: ' ', excluded: true).hasMissingGroup, isFalse);
    });
  });

  group('kit de la marca', () {
    test('buildBrandItems recorta el grupo y acota la cantidad', () {
      final items = BaseKitLogic.buildBrandItems([
        _brand('a', group: ' Fusor ', qty: 0),
      ]);
      expect(items, [
        {'itemId': 'a', 'groupName': 'Fusor', 'quantity': 1},
      ]);
    });

    test('distinctGroups conserva el orden y descarta vacíos y repetidos', () {
      expect(BaseKitLogic.distinctGroups(['A', ' ', 'B', 'A ', 'a']), [
        'A',
        'B',
        'a',
      ]);
    });

    test(
      'suggestGroups filtra sin distinguir mayúsculas y no repite lo escrito',
      () {
        const groups = ['Fusor', 'Rodillo', 'Fuser kit'];
        expect(BaseKitLogic.suggestGroups(groups, 'fu'), [
          'Fusor',
          'Fuser kit',
        ]);
        expect(BaseKitLogic.suggestGroups(groups, 'fusor'), isEmpty);
        expect(BaseKitLogic.suggestGroups(groups, ''), groups);
        expect(BaseKitLogic.suggestGroups(groups, '', limit: 2), hasLength(2));
      },
    );

    test('fromJson tolera campos faltantes', () {
      final item = ModelKitItem.fromJson({'itemId': 'x', 'quantity': 2});
      expect(item.source, 'Marca');
      expect(item.excluded, isFalse);
      expect(item.groupName, '');
    });
  });
}
