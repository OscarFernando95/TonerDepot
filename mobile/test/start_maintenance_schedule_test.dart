import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/maintenance_thread_text.dart';
import 'package:toner_tecnico/widgets/maintenance_choice_dialog.dart';

Future<void> _open(
  WidgetTester tester,
  void Function(MaintenanceChoice?) onResult, {
  bool allowSchedule = true,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () async => onResult(
                await showMaintenanceChoiceDialog(
                  context,
                  title: 'Iniciar mantenimiento',
                  confirmLabel: 'Iniciar',
                  allowSchedule: allowSchedule,
                ),
              ),
              child: const Text('abrir'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('abrir'));
  await tester.pumpAndSettle();
}

void main() {
  group('lógica pura del mantenimiento propio programado', () {
    final local = DateTime(2026, 10, 30, 10, 0);
    final now = DateTime(2026, 10, 22, 9);

    test('plannedAt viaja en ISO UTC y sin fecha no viaja', () {
      expect(startMaintenancePlannedAtField(null), isNull);
      expect(
        startMaintenancePlannedAtField(local),
        local.toUtc().toIso8601String(),
      );
      expect(startMaintenancePlannedAtField(local)!.endsWith('Z'), isTrue);
    });

    test('el cuerpo trae lo de siempre y plannedAt solo cuando hay fecha', () {
      final without = startMaintenanceBody(
        assetId: 'a1',
        includesGeneral: true,
        includesUnits: false,
        includesConsumables: true,
        reason: 'Revisión',
      );
      expect(without, {
        'assetId': 'a1',
        'includesGeneral': true,
        'includesUnits': false,
        'includesConsumables': true,
        'reason': 'Revisión',
      });
      expect(without.containsKey('plannedAt'), isFalse);

      final withDate = startMaintenanceBody(
        assetId: 'a1',
        includesGeneral: true,
        includesUnits: false,
        includesConsumables: false,
        plannedAt: local,
      );
      expect(withDate['plannedAt'], local.toUtc().toIso8601String());
      expect(withDate.containsKey('reason'), isFalse);
    });

    test('botón y mensaje según haya fecha o no', () {
      expect(
        startMaintenanceConfirmLabel(null, defaultLabel: 'Iniciar'),
        'Iniciar',
      );
      expect(
        startMaintenanceConfirmLabel(local, defaultLabel: 'Iniciar'),
        'Programar mantenimiento',
      );
      expect(
        startMaintenanceDoneMessage(null),
        'Mantenimiento listo. Haz el check-in en Mi trabajo.',
      );
      expect(
        startMaintenanceDoneMessage(local, now: now),
        'Mantenimiento programado para ${formatDateTimeEsCo(local, now: now)}. '
        'El cliente verá la fecha.',
      );
      expect(
        startMaintenanceDoneMessage(local, now: now),
        contains('vie 30 oct, 10:00'),
      );
    });

    test('el rango válido sigue siendo futuro y hasta 365 días', () {
      expect(
        validatePlannedAt(now.subtract(const Duration(hours: 1)), now: now),
        isNotNull,
      );
      expect(
        validatePlannedAt(now.add(const Duration(days: 366)), now: now),
        isNotNull,
      );
      expect(validatePlannedAt(local, now: now), isNull);
    });
  });

  group('diálogo', () {
    testWidgets('sin allowSchedule no aparece la pregunta (staff)', (
      tester,
    ) async {
      await _open(tester, (_) {}, allowSchedule: false);
      expect(find.text('¿Cuándo lo harás?'), findsNothing);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
    });

    testWidgets('por defecto es "Ahora" y devuelve plannedAt null', (
      tester,
    ) async {
      MaintenanceChoice? result;
      await _open(tester, (r) => result = r);

      expect(find.text('¿Cuándo lo harás?'), findsOneWidget);
      expect(find.text('Ahora'), findsOneWidget);
      expect(find.text('Programarlo para otra fecha'), findsOneWidget);

      await tester.tap(find.text('Mantenimiento general'));
      await tester.pump();
      expect(find.widgetWithText(FilledButton, 'Iniciar'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Iniciar'));
      await tester.pumpAndSettle();

      expect(result, isNotNull);
      expect(result!.general, isTrue);
      expect(result!.plannedAt, isNull);
    });

    testWidgets(
      'programar abre el selector, cambia el botón y devuelve la fecha',
      (tester) async {
        MaintenanceChoice? result;
        await _open(tester, (r) => result = r);

        await tester.tap(find.text('Mantenimiento general'));
        await tester.tap(find.text('Programarlo para otra fecha'));
        await tester.pumpAndSettle();

        // Selector de día y luego de hora, con los valores por defecto (dentro de una hora).
        expect(find.byType(DatePickerDialog), findsOneWidget);
        await tester.tap(find.text('OK'));
        await tester.pumpAndSettle();
        expect(find.byType(TimePickerDialog), findsOneWidget);
        await tester.tap(find.text('OK'));
        await tester.pumpAndSettle();

        expect(
          find.widgetWithText(FilledButton, 'Programar mantenimiento'),
          findsOneWidget,
        );
        expect(find.textContaining('Cambiar'), findsOneWidget);

        await tester.tap(
          find.widgetWithText(FilledButton, 'Programar mantenimiento'),
        );
        await tester.pumpAndSettle();

        expect(result, isNotNull);
        expect(result!.plannedAt, isNotNull);
        expect(result!.plannedAt!.isAfter(DateTime.now()), isTrue);
      },
    );

    testWidgets('cancelar el selector deja "Ahora"', (tester) async {
      await _open(tester, (_) {});
      await tester.tap(find.text('Programarlo para otra fecha'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(FilledButton, 'Iniciar'), findsOneWidget);
      await tester.tap(find.text('Cancelar'));
      await tester.pumpAndSettle();
    });
  });
}
