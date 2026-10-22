import 'package:flutter/material.dart';

import '../models/maintenance_thread_text.dart';
import '../theme/app_theme.dart';
import 'maintenance_schedule_picker.dart';

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
/// vacía de general, unidades e insumos, más el motivo opcional y, solo para el técnico que inicia el suyo, la
/// fecha en que lo hará.
class MaintenanceChoice {
  const MaintenanceChoice({
    required this.general,
    required this.units,
    required this.consumables,
    this.reason,
    this.plannedAt,
  });

  final bool general;
  final bool units;
  final bool consumables;
  final String? reason;

  /// Instante local elegido para hacerlo; null = "ahora" (comportamiento de siempre).
  final DateTime? plannedAt;
}

/// Diálogo para elegir el tipo de mantenimiento — lo usan el staff al pedirlo
/// (AssetDetailScreen) y el técnico al iniciarlo por su cuenta
/// (MeterReadingsScreen). Devuelve null si cancela. El botón de confirmar
/// queda deshabilitado mientras no haya al menos un tipo marcado (el servidor
/// valida lo mismo). Con [allowSchedule] (técnico iniciando el suyo) añade "¿Cuándo lo harás?": ahora, o
/// programarlo para otra fecha con [pickMaintenanceSchedule].
Future<MaintenanceChoice?> showMaintenanceChoiceDialog(
  BuildContext context, {
  required String title,
  required String confirmLabel,
  String? description,
  bool initialGeneral = false,
  bool allowSchedule = false,
}) => showDialog<MaintenanceChoice>(
  context: context,
  builder: (_) => _MaintenanceChoiceDialog(
    title: title,
    confirmLabel: confirmLabel,
    description: description,
    initialGeneral: initialGeneral,
    allowSchedule: allowSchedule,
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
    required this.allowSchedule,
  });

  final String title;
  final String confirmLabel;
  final String? description;
  final bool initialGeneral;
  final bool allowSchedule;

  @override
  State<_MaintenanceChoiceDialog> createState() =>
      _MaintenanceChoiceDialogState();
}

class _MaintenanceChoiceDialogState extends State<_MaintenanceChoiceDialog> {
  late bool _general = widget.initialGeneral;
  bool _units = false;
  bool _consumables = false;

  /// null = "Ahora".
  DateTime? _plannedAt;
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
        plannedAt: _plannedAt,
      ),
    );
  }

  void _chooseNow() => setState(() => _plannedAt = null);

  /// Abre el selector de día y hora; si el técnico cancela o la fecha no sirve, queda lo que había.
  Future<void> _chooseLater() async {
    final picked = await pickMaintenanceSchedule(context, current: _plannedAt);
    if (picked == null || !mounted) return;
    setState(() => _plannedAt = picked);
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
            if (widget.allowSchedule) ...[
              const SizedBox(height: 4),
              Text(
                '¿Cuándo lo harás?',
                style: Theme.of(context).textTheme.titleSmall
                    ?.copyWith(fontWeight: FontWeight.w700),
              ),
              _WhenOption(
                label: 'Ahora',
                selected: _plannedAt == null,
                onTap: _chooseNow,
              ),
              _WhenOption(
                label: 'Programarlo para otra fecha',
                detail: _plannedAt == null
                    ? null
                    : '${formatDateTimeEsCo(_plannedAt!)} · Cambiar',
                selected: _plannedAt != null,
                onTap: _chooseLater,
              ),
              const SizedBox(height: 8),
            ],
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
          child: Text(
            startMaintenanceConfirmLabel(
              _plannedAt,
              defaultLabel: widget.confirmLabel,
            ),
          ),
        ),
      ],
    );
  }
}

/// Opción excluyente "Ahora" / "Programarlo para otra fecha" (>= 48 dp de alto).
class _WhenOption extends StatelessWidget {
  const _WhenOption({
    required this.label,
    required this.selected,
    required this.onTap,
    this.detail,
  });

  final String label;
  final String? detail;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      inMutuallyExclusiveGroup: true,
      checked: selected,
      button: true,
      label: detail == null ? label : '$label, $detail',
      excludeSemantics: true,
      onTap: onTap,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48),
          child: Row(
            children: [
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_off,
                color: selected ? AppColors.signalBlue : AppColors.inkSecondary,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(label),
                    if (detail != null)
                      Text(
                        detail!,
                        style: const TextStyle(
                          color: AppColors.signalBlue,
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
