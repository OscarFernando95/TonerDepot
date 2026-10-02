import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/toner_analytics.dart';
import 'package:toner_tecnico/widgets/toner_analytics_monthly_chart.dart';

void main() {
  group('TonerFilter.toQueryParams', () {
    test('rango como inicio y fin de día, sin zona horaria', () {
      final params = TonerFilter(
        from: DateTime(2026, 3, 1),
        to: DateTime(2026, 9, 30),
      ).toQueryParams();
      expect(params, {
        'from': '2026-03-01T00:00:00',
        'to': '2026-09-30T23:59:59',
      });
    });

    test('omite los filtros vacíos o nulos e incluye los que tienen valor', () {
      final params = TonerFilter(
        from: DateTime(2026, 1, 5),
        zoneId: 'z1',
        clientId: '',
        brandId: 'b1',
        modelId: null,
      ).toQueryParams();
      expect(params.keys, unorderedEquals(['from', 'zoneId', 'brandId']));
      expect(params['zoneId'], 'z1');
    });

    test('rango por defecto: hace 6 meses hasta hoy', () {
      final f = TonerFilter.defaultRange(DateTime(2026, 10, 1, 15, 30));
      expect(f.from, DateTime(2026, 4, 1));
      expect(f.to, DateTime(2026, 10, 1));
    });

    test('rango por defecto cruzando el año', () {
      final f = TonerFilter.defaultRange(DateTime(2026, 2, 10));
      expect(f.from, DateTime(2025, 8, 10));
    });

    test('copyWith conserva lo no indicado y permite limpiar con null', () {
      final f = const TonerFilter(
        zoneId: 'z',
        clientId: 'c',
        brandId: 'b',
        modelId: 'm',
      );
      final g = f.copyWith(brandId: null, modelId: null);
      expect(g.zoneId, 'z');
      expect(g.clientId, 'c');
      expect(g.brandId, isNull);
      expect(g.modelId, isNull);
      expect(g.hasDimensionFilters, isTrue);
      expect(const TonerFilter().hasDimensionFilters, isFalse);
      expect(f.copyWith(), f);
    });
  });

  group('TonerAnalytics.classifyVsModel', () {
    test('menos de 85 es peor, más de 115 es mejor, entre medio neutro', () {
      expect(TonerAnalytics.classifyVsModel(84.9), VsModelLevel.worse);
      expect(TonerAnalytics.classifyVsModel(0), VsModelLevel.worse);
      expect(TonerAnalytics.classifyVsModel(85), VsModelLevel.neutral);
      expect(TonerAnalytics.classifyVsModel(100), VsModelLevel.neutral);
      expect(TonerAnalytics.classifyVsModel(115), VsModelLevel.neutral);
      expect(TonerAnalytics.classifyVsModel(115.1), VsModelLevel.better);
    });

    test('sin dato no hay comparación', () {
      expect(TonerAnalytics.classifyVsModel(null), VsModelLevel.none);
    });
  });

  group('formato', () {
    test('monthLabel: mes abreviado y año de 2 dígitos', () {
      expect(TonerAnalytics.monthLabel('2026-01'), 'ene 26');
      expect(TonerAnalytics.monthLabel('2025-12'), 'dic 25');
      expect(TonerAnalytics.monthLabel('2026-09'), 'sep 26');
    });

    test('monthLabel tolera valores inválidos', () {
      expect(TonerAnalytics.monthLabel('basura'), 'basura');
      expect(TonerAnalytics.monthLabel('2026-13'), '2026-13');
      expect(TonerAnalytics.monthLabel('x-y'), 'x-y');
    });

    test('formatNumber al estilo es-CO con un decimal como máximo', () {
      expect(TonerAnalytics.formatNumber(null), '—');
      expect(TonerAnalytics.formatNumber(0), '0');
      expect(TonerAnalytics.formatNumber(12), '12');
      expect(TonerAnalytics.formatNumber(112.54), '112,5');
      expect(TonerAnalytics.formatNumber(112.96), '113');
      expect(TonerAnalytics.formatNumber(1234), '1.234');
      expect(TonerAnalytics.formatNumber(1234567.8), '1.234.567,8');
      expect(TonerAnalytics.formatPercent(63), '63%');
    });

    test('formatDate y parseInstant', () {
      expect(TonerAnalytics.formatDate(null), '—');
      expect(TonerAnalytics.formatDate(DateTime(2026, 3, 7)), '07/03/2026');
      expect(TonerAnalytics.parseInstant(null), isNull);
      expect(TonerAnalytics.parseInstant(''), isNull);
      expect(
        TonerAnalytics.parseInstant('2026-03-07T10:00:00Z'),
        DateTime.utc(2026, 3, 7, 10),
      );
      // Sin zona: se interpreta como UTC.
      expect(
        TonerAnalytics.parseInstant('2026-03-07T10:00:00'),
        DateTime.utc(2026, 3, 7, 10),
      );
      expect(
        TonerAnalytics.parseInstant('2026-03-07T10:00:00-05:00'),
        DateTime.utc(2026, 3, 7, 15),
      );
    });

    test('barFraction nunca deja en cero un valor positivo', () {
      expect(TonerAnalytics.barFraction(0, 10), 0);
      expect(TonerAnalytics.barFraction(10, 10), 1);
      expect(TonerAnalytics.barFraction(5, 10), 0.5);
      expect(TonerAnalytics.barFraction(1, 1000), 0.04);
      expect(TonerAnalytics.barFraction(3, 0), 0);
    });

    test('etiqueta semántica del gráfico', () {
      expect(
        TonerAnalytics.chartSemanticLabel(const []),
        contains('Sin datos'),
      );
      expect(
        TonerAnalytics.chartSemanticLabel(const [
          TonerMonth(month: '2026-01', units: 5),
          TonerMonth(month: '2026-02', units: 8),
        ]),
        'Tóner usado por mes. ene 26: 5. feb 26: 8.',
      );
    });
  });

  group('parseo', () {
    test('resumen completo', () {
      final s = TonerSummary.fromJson({
        'totalUnits': 20,
        'changedByTechnicianUnits': 15,
        'deliveredToUserUnits': 5,
        'machines': 4,
        'measuredUnits': 10,
        'avgPagesPerUnit': 3500.5,
        'monthly': [
          {'month': '2026-01', 'units': 7},
        ],
        'byClient': [
          {
            'name': 'ACME',
            'machines': 2,
            'totalUnits': 9,
            'avgPagesPerUnit': null,
          },
        ],
        'byZone': <dynamic>[],
        'byModel': <dynamic>[],
      });
      expect(s.totalUnits, 20);
      expect(s.avgPagesPerUnit, 3500.5);
      expect(s.monthly.single.units, 7);
      expect(s.byClient.single.avgPagesPerUnit, isNull);
      expect(s.byZone, isEmpty);
    });

    test('máquina con enteros y decimales mezclados y nulos', () {
      final r = TonerMachineRow.fromJson({
        'assetId': 'a',
        'brand': 'HP',
        'model': 'M404',
        'serialNumber': 'S1',
        'clientName': null,
        'totalUnits': 3,
        'changedByTechnicianUnits': 2,
        'deliveredToUserUnits': 1,
        'measuredUnits': 2,
        'avgPagesPerUnit': 3000,
        'pagesInRange': 9000,
        'vsModelPercent': 84,
        'lastEventAt': '2026-03-07T10:00:00Z',
        'lastCounter': 120000,
      });
      expect(r.avgPagesPerUnit, 3000.0);
      expect(r.pagesInRange, 9000);
      expect(r.clientName, isNull);
      expect(r.lastEventAt, DateTime.utc(2026, 3, 7, 10));
      expect(
        TonerAnalytics.classifyVsModel(r.vsModelPercent),
        VsModelLevel.worse,
      );
    });

    test('página de máquinas usa totalCount', () {
      final p = TonerMachinesPage.fromJson({
        'items': <dynamic>[],
        'page': 2,
        'pageSize': 15,
        'totalCount': 31,
        'hasMore': true,
      });
      expect(p.totalCount, 31);
      expect(p.page, 2);
      expect(p.hasMore, isTrue);
    });
  });

  testWidgets(
    'el gráfico mensual expone la etiqueta semántica con los valores',
    (tester) async {
      final handle = tester.ensureSemantics();
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Padding(
              padding: EdgeInsets.all(16),
              child: TonerAnalyticsMonthlyChart(
                monthly: [
                  TonerMonth(month: '2026-01', units: 5),
                  TonerMonth(month: '2026-02', units: 0),
                  TonerMonth(month: '2026-03', units: 8),
                ],
              ),
            ),
          ),
        ),
      );
      expect(
        find.bySemanticsLabel(
          'Tóner usado por mes. ene 26: 5. feb 26: 0. mar 26: 8.',
        ),
        findsOneWidget,
      );
      expect(find.text('mar 26'), findsOneWidget);
      handle.dispose();
    },
  );
}
