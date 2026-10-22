import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/base_kit.dart';
import 'package:toner_tecnico/models/inventory_admin.dart';
import 'package:toner_tecnico/utils/grams_display.dart';

void main() {
  group('formatGrams', () {
    test('en gramos muestra el saldo con separador de miles', () {
      expect(formatGrams(250, 500, mode: GramsMode.grams), '250 g');
      expect(formatGrams(1250, 500, mode: GramsMode.grams), '1.250 g');
    });

    test('en kg y bolsas separa bolsas completas y resto', () {
      expect(
        formatGrams(1250, 500, mode: GramsMode.kgBags),
        '1,25 kg (2 bolsas + 250 g)',
      );
      expect(formatGrams(500, 500, mode: GramsMode.kgBags), '0,5 kg (1 bolsa)');
      expect(formatGrams(180, 500, mode: GramsMode.kgBags), '0,18 kg (180 g)');
    });

    test('sin tamaño de bolsa solo muestra kilos', () {
      expect(formatGrams(750, null, mode: GramsMode.kgBags), '0,75 kg');
    });

    test('mantiene el signo en saldos negativos', () {
      expect(
        formatGrams(-180, 500, mode: GramsMode.kgBags),
        '-0,18 kg (-180 g)',
      );
    });
  });

  group('formatQuantity', () {
    test('deja intacto lo que no es revelador', () {
      expect(formatQuantity(7, null, mode: GramsMode.kgBags), '7');
    });
  });

  group('formatSignedQuantity', () {
    test('conserva el formato de unidades', () {
      expect(formatSignedQuantity(5), '+5');
      expect(formatSignedQuantity(-3), '-3');
    });
  });

  group('ItemDraft', () {
    test('un revelador exige los gramos por bolsa y fija la unidad en g', () {
      const sinBolsa = ItemDraft(name: 'Revelador X', category: 'Revelador');
      expect(sinBolsa.validate(), isNotNull);

      const conBolsa = ItemDraft(
        name: 'Revelador X',
        category: 'Revelador',
        unit: 'und',
        gramsPerBag: 500,
      );
      expect(conBolsa.validate(), isNull);
      final json = conBolsa.toJson(editing: false);
      expect(json['unit'], 'g');
      expect(json['gramsPerBag'], 500);
    });

    test('otras categorías nunca mandan gramos por bolsa', () {
      const item = ItemDraft(name: 'Fusor', gramsPerBag: 500);
      expect(item.toJson(editing: false)['gramsPerBag'], isNull);
    });
  });

  group('BaseKitLogic.clampQuantity', () {
    test('unidades hasta 100 y gramos hasta 10.000', () {
      expect(BaseKitLogic.clampQuantity(150), baseKitMaxQuantity);
      expect(BaseKitLogic.clampQuantity(180, grams: true), 180);
      expect(BaseKitLogic.clampQuantity(50000, grams: true), baseKitMaxGrams);
    });
  });
}
