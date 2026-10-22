import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/widgets/maintenance_choice_dialog.dart';

/// Abre el diálogo desde un botón y deja el resultado en [result] cuando se cierra.
Future<void> _open(
  WidgetTester tester,
  void Function(MaintenanceChoice?) onResult, {
  bool initialGeneral = false,
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
                  description: 'Marca lo que vas a hacer.',
                  initialGeneral: initialGeneral,
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

FilledButton _confirm(WidgetTester tester) =>
    tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Iniciar'));

void main() {
  testWidgets('sin ningún tipo marcado no se puede confirmar', (tester) async {
    await _open(tester, (_) {});

    expect(_confirm(tester).onPressed, isNull);

    // Se cierra el diálogo: desmontarlo abierto al terminar el test contamina al siguiente.
    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();
  });

  testWidgets(
    'al marcar un tipo se habilita y devuelve exactamente lo elegido',
    (tester) async {
      MaintenanceChoice? result;
      await _open(tester, (r) => result = r);

      await tester.tap(find.text('Cambio de insumos'));
      await tester.enterText(find.byType(TextField), '  Cambio de tóner  ');
      await tester.pump();
      expect(_confirm(tester).onPressed, isNotNull);

      await tester.tap(find.widgetWithText(FilledButton, 'Iniciar'));
      await tester.pumpAndSettle();

      expect(result, isNotNull);
      expect(result!.general, isFalse);
      expect(result!.units, isFalse);
      expect(result!.consumables, isTrue);
      expect(result!.reason, 'Cambio de tóner');
    },
  );

  testWidgets('un motivo en blanco se devuelve como null', (tester) async {
    MaintenanceChoice? result;
    await _open(tester, (r) => result = r);

    await tester.tap(find.text('Mantenimiento de unidades'));
    await tester.enterText(find.byType(TextField), '   ');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Iniciar'));
    await tester.pumpAndSettle();

    expect(result!.units, isTrue);
    expect(result!.reason, isNull);
  });

  testWidgets('cancelar devuelve null', (tester) async {
    MaintenanceChoice? result = const MaintenanceChoice(
      general: true,
      units: true,
      consumables: true,
    );
    await _open(tester, (r) => result = r);

    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();

    expect(result, isNull);
  });

  testWidgets('el staff arranca con general marcado (initialGeneral)', (
    tester,
  ) async {
    await _open(tester, (_) {}, initialGeneral: true);

    expect(_confirm(tester).onPressed, isNotNull);

    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();
  });
}
