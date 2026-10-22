import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/asset.dart';
import 'package:toner_tecnico/models/asset_model.dart';
import 'package:toner_tecnico/models/meter_reading_asset.dart';
import 'package:toner_tecnico/models/rental_retirement.dart';

void main() {
  group('formatPrintsEsCo', () {
    test('separador de miles con punto', () {
      expect(formatPrintsEsCo(0), '0');
      expect(formatPrintsEsCo(999), '999');
      expect(formatPrintsEsCo(1000), '1.000');
      expect(formatPrintsEsCo(50000), '50.000');
      expect(formatPrintsEsCo(1234567), '1.234.567');
      expect(formatPrintsEsCo(-12500), '-12.500');
    });
  });

  group('RentalRetirementAlert.from', () {
    test('sin estado no muestra nada', () {
      expect(
        RentalRetirementAlert.from(status: null, printsUntilRetirement: 10),
        isNull,
      );
      expect(
        RentalRetirementAlert.from(status: null, printsUntilRetirement: null),
        isNull,
      );
      expect(
        RentalRetirementAlert.from(status: 'Otro', printsUntilRetirement: 10),
        isNull,
      );
    });

    test('Proxima: ámbar con "Faltan N impresiones"', () {
      final a = RentalRetirementAlert.from(
        status: 'Proxima',
        printsUntilRetirement: 42500,
      )!;
      expect(a.exceeded, isFalse);
      expect(a.label, 'Próxima a salir de alquiler');
      expect(a.detail, 'Faltan 42.500 impresiones');
    });

    test('Proxima con una sola impresión', () {
      final a = RentalRetirementAlert.from(
        status: 'Proxima',
        printsUntilRetirement: 1,
      )!;
      expect(a.detail, 'Falta 1 impresión');
    });

    test('Superada: rojo con "Superó el límite por N impresiones"', () {
      final a = RentalRetirementAlert.from(
        status: 'Superada',
        printsUntilRetirement: -3200,
      )!;
      expect(a.exceeded, isTrue);
      expect(a.label, 'Superó el límite de alquiler');
      expect(a.detail, 'Superó el límite por 3.200 impresiones');
    });

    test('sin conteo solo hay chip', () {
      final a = RentalRetirementAlert.from(
        status: 'Superada',
        printsUntilRetirement: null,
      )!;
      expect(a.detail, isNull);
    });
  });

  group('validación del formulario del modelo', () {
    test('umbral: vacío = sin límite; entero > 0', () {
      expect(validateRentalThreshold(''), isNull);
      expect(validateRentalThreshold('   '), isNull);
      expect(validateRentalThreshold(null), isNull);
      expect(validateRentalThreshold('200000'), isNull);
      expect(validateRentalThreshold('0'), isNotNull);
      expect(validateRentalThreshold('-5'), isNotNull);
      expect(validateRentalThreshold('abc'), isNotNull);
      expect(validateRentalThreshold('1.5'), isNotNull);
    });

    test('aviso: obligatorio, entero > 0', () {
      expect(validateRentalWarning('50000'), isNull);
      expect(validateRentalWarning(''), isNotNull);
      expect(validateRentalWarning('0'), isNotNull);
      expect(validateRentalWarning('x'), isNotNull);
    });

    test('parseRentalThreshold: vacío -> null', () {
      expect(parseRentalThreshold(''), isNull);
      expect(parseRentalThreshold(' 200000 '), 200000);
    });

    test('resumen del límite', () {
      expect(
        rentalThresholdSummary(null, 50000),
        'Salida de alquiler: sin límite',
      );
      expect(
        rentalThresholdSummary(200000, 50000),
        'Salida de alquiler: 200.000 impr. (aviso a 50.000)',
      );
    });
  });

  group('parseo tolerante a null', () {
    test('AssetModel sin los campos nuevos usa los valores por defecto', () {
      final m = AssetModel.fromJson({
        'id': 'm',
        'assetBrandId': 'b',
        'name': 'MP',
        'generalPrintThreshold': 1,
        'generalMonthsInterval': 2,
        'unitsPrintThreshold': 3,
        'unitsMonthsInterval': 4,
        'consumablesPrintThreshold': 5,
      });
      expect(m.rentalRetirementPrintThreshold, isNull);
      expect(m.rentalRetirementWarningPrints, 50000);
    });

    test('AssetModel con los campos nuevos', () {
      final m = AssetModel.fromJson({
        'id': 'm',
        'assetBrandId': 'b',
        'name': 'MP',
        'generalPrintThreshold': 1,
        'generalMonthsInterval': 2,
        'unitsPrintThreshold': 3,
        'unitsMonthsInterval': 4,
        'consumablesPrintThreshold': 5,
        'rentalRetirementPrintThreshold': 200000,
        'rentalRetirementWarningPrints': 30000,
      });
      expect(m.rentalRetirementPrintThreshold, 200000);
      expect(m.rentalRetirementWarningPrints, 30000);
    });

    test('MeterReadingAsset: null y valores', () {
      final base = {
        'assetId': 'a1',
        'assetBrandName': 'Ricoh',
        'model': 'MP2553',
        'serialNumber': 'S1',
      };
      final sin = MeterReadingAsset.fromJson(base);
      expect(sin.rentalRetirementStatus, isNull);
      expect(sin.printsUntilRetirement, isNull);
      expect(sin.rentalRetirementAlert, isNull);

      final con = MeterReadingAsset.fromJson({
        ...base,
        'rentalRetirementPrintThreshold': 200000,
        'rentalRetirementWarningPrints': 50000,
        'printsUntilRetirement': 40000,
        'rentalRetirementStatus': 'Proxima',
      });
      expect(con.rentalRetirementAlert!.detail, 'Faltan 40.000 impresiones');
    });

    test('Asset: null y valores', () {
      final base = {
        'id': 'x',
        'assetModelId': 'm',
        'assetBrandName': 'Ricoh',
        'model': 'MP',
        'serialNumber': 'S',
        'type': 'Impresora',
        'lifecycleStatus': 'Instalado',
        'createdAt': '2026-10-01T10:00:00Z',
      };
      expect(Asset.fromJson(base).rentalRetirementAlert, isNull);
      final a = Asset.fromJson({
        ...base,
        'printsUntilRetirement': -10,
        'rentalRetirementStatus': 'Superada',
      });
      expect(a.rentalRetirementAlert!.exceeded, isTrue);
      expect(
        a.rentalRetirementAlert!.detail,
        'Superó el límite por 10 impresiones',
      );
    });
  });
}
