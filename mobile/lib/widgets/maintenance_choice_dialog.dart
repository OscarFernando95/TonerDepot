import 'package:flutter/material.dart';

/// Espera a que termine la animación de salida de un diálogo recién cerrado.
///
/// Navigator.pop() resuelve el Future de showDialog ANTES de que la transición
/// de salida termine de verdad: si la petición que sigue se rechaza casi al
/// instante (ej. 409 por validación, sin tocar la base de datos), el
/// notifyListeners() reconstruye la pantalla mientras Flutter todavía desmonta
/// la ruta del diálogo y salta "'_dependents.isEmpty': is not true" (pantalla
/// roja — hallazgo 2026-10-12, "Pedir mantenimiento" sobre un activo con orden
/// ya abierta). Con una petición exitosa no se nota porque la ida y vuelta de
/// red ya cubre la animación. Úsala entre `Navigator.pop()` y cualquier cambio
/// de estado que dispare un rebuild, y revisa `context.mounted` después.
Future<void> waitForDialogExit() => Future<void>.delayed(_dialogExitDuration);

// Más que la transición por defecto de un diálogo (150 ms), con margen.
const _dialogExitDuration = Duration(milliseconds: 300);

/// Qué se va a hacer en un mantenimiento a demanda: cualquier combinación no
/// vacía de general, unidades e insumos, más el motivo opcional.
class MaintenanceChoice {
  const MaintenanceChoice({
    required this.general,
    required this.units,
    required this.consumables,
    this.reason,
  });

  final bool general;
  final bool units;
  final bool consumables;
  final String? reason;
}

/// Diálogo para elegir el tipo de mantenimiento — lo usan el staff al pedirlo
/// (AssetDetailScreen) y el técnico al iniciarlo por su cuenta
/// (MeterReadingsScreen). Devuelve null si cancela. El botón de confirmar
/// queda deshabilitado mientras no haya al menos un tipo marcado (el servidor
/// valida lo mismo).
Future<MaintenanceChoice?> showMaintenanceChoiceDialog(
  BuildContext context, {
  required String title,
  required String confirmLabel,
  String? description,
  bool initialGeneral = false,
}) => showDialog<MaintenanceChoice>(
  context: context,
  builder: (_) => _MaintenanceChoiceDialog(
    title: title,
    confirmLabel: confirmLabel,
    description: description,
    initialGeneral: initialGeneral,
  ),
);

// Dueño de su TextEditingController: se libera cuando Flutter desmonta el
// diálogo, DESPUÉS de la animación de salida. Disponerlo desde quien abre el
// diálogo (justo al resolver el Future) lo dejaba usado después de disposed
// mientras la salida todavía reconstruía el campo.
class _MaintenanceChoiceDialog extends StatefulWidget {
  const _MaintenanceChoiceDialog({
    required this.title,
    required this.confirmLabel,
    required this.description,
    required this.initialGeneral,
  });

  final String title;
  final String confirmLabel;
  final String? description;
  final bool initialGeneral;

  @override
  State<_MaintenanceChoiceDialog> createState() =>
      _MaintenanceChoiceDialogState();
}

class _MaintenanceChoiceDialogState extends State<_MaintenanceChoiceDialog> {
  late bool _general = widget.initialGeneral;
  bool _units = false;
  bool _consumables = false;
  final _reasonController = TextEditingController();

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  void _confirm() {
    final reason = _reasonController.text.trim();
    Navigator.of(context).pop(
      MaintenanceChoice(
        general: _general,
        units: _units,
        consumables: _consumables,
        reason: reason.isEmpty ? null : reason,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (widget.description != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text(widget.description!),
              ),
            CheckboxListTile(
              value: _general,
              onChanged: (v) => setState(() => _general = v ?? false),
              title: const Text('Mantenimiento general'),
              contentPadding: EdgeInsets.zero,
            ),
            CheckboxListTile(
              value: _units,
              onChanged: (v) => setState(() => _units = v ?? false),
              title: const Text('Mantenimiento de unidades'),
              contentPadding: EdgeInsets.zero,
            ),
            CheckboxListTile(
              value: _consumables,
              onChanged: (v) => setState(() => _consumables = v ?? false),
              title: const Text('Cambio de insumos'),
              contentPadding: EdgeInsets.zero,
            ),
            TextField(
              controller: _reasonController,
              maxLength: 500,
              minLines: 2,
              maxLines: 3,
              decoration: const InputDecoration(labelText: 'Motivo (opcional)'),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancelar'),
        ),
        FilledButton(
          onPressed: (_general || _units || _consumables) ? _confirm : null,
          child: Text(widget.confirmLabel),
        ),
      ],
    );
  }
}
