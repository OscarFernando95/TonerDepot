import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../theme/app_theme.dart';
import '../models/maintenance_order.dart';
import '../models/pending_installation.dart';
import '../models/service_ticket.dart';
import '../services/device_capture.dart';
import '../services/technician_api.dart';
import 'animated_gradient_border.dart';
import 'clay_date_field.dart';

/// Hoja modal de check-out. Reglas de campos obligatorios espejo de
/// checkoutFormValid en MyWorkView.vue:
///   - Si "resolved" está apagado: no se exige nada, es un cierre parcial.
///   - Instalación + resolved: área, lectura de contador y "mantenimiento de
///     unidades realizado" (sí/no) son obligatorios.
///   - Orden + resolved: lectura de contador es obligatoria.
///   - Ticket: nada es obligatorio; si es de un cliente externo (sin activo
///     catalogado), se ofrecen los campos opcionales de marca/modelo/contador.
/// Lo que devuelve la hoja: los datos del cierre y la foto "después" (obligatoria al resolver un ticket u
/// orden) y, en una orden, la foto del contador que respalda la lectura. Las fotos se suben en
/// MyWorkState.checkOut, no acá.
class CheckoutSubmission {
  const CheckoutSubmission(this.request, this.photo, [this.counterPhoto]);

  final CheckOutRequest request;
  final XFile? photo;
  final XFile? counterPhoto;
}

class CheckoutSheet extends StatefulWidget {
  const CheckoutSheet({
    super.key,
    this.activeTicket,
    this.activeOrder,
    this.activeInstallation,
  });

  final ServiceTicket? activeTicket;
  final MaintenanceOrder? activeOrder;
  final PendingInstallation? activeInstallation;

  static Future<CheckoutSubmission?> show(
    BuildContext context, {
    ServiceTicket? activeTicket,
    MaintenanceOrder? activeOrder,
    PendingInstallation? activeInstallation,
  }) {
    return showModalBottomSheet<CheckoutSubmission>(
      context: context,
      isScrollControlled: true,
      // Opaco (clay), no glass: es un formulario de datos de cierre de
      // visita, no chrome decorativo — la regla del sistema es que texto
      // crítico nunca vive sobre una superficie translúcida.
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(
          topLeft: Radius.circular(24),
          topRight: Radius.circular(24),
        ),
      ),
      builder: (_) => CheckoutSheet(
        activeTicket: activeTicket,
        activeOrder: activeOrder,
        activeInstallation: activeInstallation,
      ),
    );
  }

  @override
  State<CheckoutSheet> createState() => _CheckoutSheetState();
}

class _CheckoutSheetState extends State<CheckoutSheet> {
  bool _resolved = true;
  final _notesController = TextEditingController();
  final _counterController = TextEditingController();
  final _areaController = TextEditingController();
  final _consumablesController = TextEditingController();
  final _externalBrandController = TextEditingController();
  final _externalModelController = TextEditingController();
  final _externalCounterController = TextEditingController();
  DateTime? _counterDate;
  bool _generalMaintenanceDone = false;
  // null = sin elegir aún; true = "mantenimiento de unidades realizado"
  // (revela el campo de insumos existentes); false = "insumos nuevos".
  bool? _unitsMaintenanceDone;
  XFile? _photo;
  XFile? _counterPhoto;

  // Foto del resultado: obligatoria al resolver un ticket u orden (no una instalación).
  bool get _requiresPhoto =>
      _resolved && (widget.activeTicket != null || _hasOrder);

  // Foto del contador: respalda la lectura obligatoria al completar una orden (las instalaciones no llevan fotos).
  bool get _requiresCounterPhoto => _resolved && _hasOrder;

  // Tickets de clientes sin contrato (equipo sin catalogar): la foto del contador es opcional.
  bool get _offersOptionalCounterPhoto => _isExternalTicket;

  bool get _isExternalTicket => widget.activeTicket?.isExternal ?? false;
  bool get _hasOrder => widget.activeOrder != null;
  bool get _hasInstallation => widget.activeInstallation != null;

  // Restringe el selector de fecha a la vigencia del contrato cuando se conoce (instalaciones) — evita
  // que el técnico elija una fecha que el backend va a rechazar de todos modos. Fuera de ese caso
  // (órdenes, tickets) no hay contrato a la mano en el cliente, así que se deja el rango histórico
  // amplio de siempre.
  DateTime get _firstCounterDate =>
      widget.activeInstallation?.contractStartDate ?? DateTime(2020);
  DateTime get _lastCounterDate =>
      widget.activeInstallation?.contractEndDate ??
      DateTime.now().add(const Duration(days: 1));

  bool get _isValid {
    if (!_resolved) return true;
    if (_hasInstallation) {
      return _areaController.text.trim().isNotEmpty &&
          int.tryParse(_counterController.text.trim()) != null &&
          _unitsMaintenanceDone != null;
    }
    if (_hasOrder) {
      return int.tryParse(_counterController.text.trim()) != null &&
          _photo != null &&
          _counterPhoto != null;
    }
    return !_requiresPhoto || _photo != null;
  }

  @override
  void dispose() {
    _notesController.dispose();
    _counterController.dispose();
    _areaController.dispose();
    _consumablesController.dispose();
    _externalBrandController.dispose();
    _externalModelController.dispose();
    _externalCounterController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_isValid) return;
    Navigator.of(context).pop(
      CheckoutSubmission(
        CheckOutRequest(
          resolved: _resolved,
          notes: _notesController.text.trim().isEmpty
              ? null
              : _notesController.text.trim(),
          area: _hasInstallation && _areaController.text.trim().isNotEmpty
              ? _areaController.text.trim()
              : null,
          initialCounterValue: int.tryParse(_counterController.text.trim()),
          initialCounterDate: _counterDate,
          generalMaintenanceDone: _hasInstallation
              ? _generalMaintenanceDone
              : null,
          unitsMaintenanceDone: _hasInstallation ? _unitsMaintenanceDone : null,
          existingConsumablesPrints:
              _hasInstallation && _unitsMaintenanceDone == true
              ? int.tryParse(_consumablesController.text.trim())
              : null,
          externalAssetBrand:
              _isExternalTicket &&
                  _externalBrandController.text.trim().isNotEmpty
              ? _externalBrandController.text.trim()
              : null,
          externalAssetModel:
              _isExternalTicket &&
                  _externalModelController.text.trim().isNotEmpty
              ? _externalModelController.text.trim()
              : null,
          externalAssetCounter: _isExternalTicket
              ? int.tryParse(_externalCounterController.text.trim())
              : null,
        ),
        _requiresPhoto ? _photo : null,
        (_requiresCounterPhoto || _offersOptionalCounterPhoto)
            ? _counterPhoto
            : null,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      // viewInsets.bottom cubre el teclado; padding.bottom cubre la barra de
      // navegación/gestos del teléfono (los showModalBottomSheet no la
      // respetan solos) — sin esto último, "Confirmar check-out" queda
      // tapado en celulares con navegación por gestos.
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom:
            MediaQuery.of(context).viewInsets.bottom +
            MediaQuery.of(context).padding.bottom +
            16,
      ),
      child: StatefulBuilder(
        builder: (context, setModalState) {
          return SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Check-out',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('¿Quedó terminado?'),
                  subtitle: Text(
                    _resolved
                        ? 'Se marcará como resuelto/completado.'
                        : 'Cierre parcial — puedes retomarlo más tarde.',
                  ),
                  value: _resolved,
                  onChanged: (v) => setModalState(() => _resolved = v),
                ),
                if (_requiresPhoto) ...[
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      OutlinedButton.icon(
                        onPressed: () async {
                          final photo = await DeviceCapture.takePhoto();
                          if (photo != null) {
                            setModalState(() => _photo = photo);
                          }
                        },
                        icon: const Icon(Icons.photo_camera_outlined, size: 18),
                        label: Text(
                          _photo == null
                              ? 'Foto del resultado *'
                              : 'Cambiar foto',
                        ),
                      ),
                      if (_photo != null) ...[
                        const SizedBox(width: 12),
                        const Icon(
                          Icons.check_circle,
                          color: AppColors.signalBlue,
                        ),
                        const SizedBox(width: 4),
                        const Text('Foto lista'),
                      ],
                    ],
                  ),
                  const SizedBox(height: 8),
                ],
                TextField(
                  controller: _notesController,
                  minLines: 2,
                  maxLines: 4,
                  decoration: const InputDecoration(
                    labelText: 'Notas (opcional)',
                  ),
                ),
                if (_hasInstallation) ...[
                  const SizedBox(height: 12),
                  TextField(
                    controller: _areaController,
                    onChanged: (_) => setModalState(() {}),
                    decoration: InputDecoration(
                      labelText: _resolved
                          ? 'Área de instalación *'
                          : 'Área (opcional)',
                    ),
                  ),
                ],
                if (_hasOrder || _hasInstallation) ...[
                  const SizedBox(height: 12),
                  TextField(
                    controller: _counterController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setModalState(() {}),
                    decoration: InputDecoration(
                      labelText: _resolved
                          ? 'Lectura de contador *'
                          : 'Lectura de contador (opcional)',
                    ),
                  ),
                  if (_requiresCounterPhoto) ...[
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        OutlinedButton.icon(
                          onPressed: () async {
                            final photo = await DeviceCapture.takePhoto();
                            if (photo != null) {
                              setModalState(() => _counterPhoto = photo);
                            }
                          },
                          icon: const Icon(
                            Icons.photo_camera_outlined,
                            size: 18,
                          ),
                          label: Text(
                            _counterPhoto == null
                                ? 'Foto del contador *'
                                : 'Cambiar foto',
                          ),
                        ),
                        if (_counterPhoto != null) ...[
                          const SizedBox(width: 12),
                          const Icon(
                            Icons.check_circle,
                            color: AppColors.signalBlue,
                          ),
                          const SizedBox(width: 4),
                          const Text('Foto lista'),
                        ],
                      ],
                    ),
                  ],
                  const SizedBox(height: 8),
                  if (_hasInstallation &&
                      widget.activeInstallation?.contractStartDate != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Text(
                        widget.activeInstallation!.contractEndDate != null
                            ? 'Debe estar dentro del contrato: '
                                  '${_firstCounterDate.toIso8601String().split('T').first} a '
                                  '${_lastCounterDate.toIso8601String().split('T').first}'
                            : 'Debe ser a partir del ${_firstCounterDate.toIso8601String().split('T').first} (inicio del contrato)',
                        style: const TextStyle(
                          color: AppColors.inkSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ClayDateField(
                    label: 'Fecha de la lectura (opcional)',
                    value: _counterDate == null
                        ? 'Sin definir'
                        : _counterDate!.toIso8601String().split('T').first,
                    onTap: () async {
                      final firstDate = _firstCounterDate;
                      final lastDate = _lastCounterDate;
                      var initialDate = _counterDate ?? DateTime.now();
                      if (initialDate.isBefore(firstDate)) {
                        initialDate = firstDate;
                      }
                      if (initialDate.isAfter(lastDate)) initialDate = lastDate;
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: initialDate,
                        firstDate: firstDate,
                        lastDate: lastDate,
                      );
                      if (picked != null) {
                        setModalState(() => _counterDate = picked);
                      }
                    },
                  ),
                ],
                if (_hasInstallation) ...[
                  const Divider(height: 24, color: AppColors.neutralSoft),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('¿Mantenimiento general realizado?'),
                    value: _generalMaintenanceDone,
                    onChanged: (v) =>
                        setModalState(() => _generalMaintenanceDone = v),
                  ),
                  Text(
                    '¿Mantenimiento de unidades realizado, o se dejaron insumos nuevos? *',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      Expanded(
                        child: RadioListTile<bool>(
                          contentPadding: EdgeInsets.zero,
                          title: const Text(
                            'Unidades',
                            style: TextStyle(fontSize: 13),
                          ),
                          value: true,
                          // ignore: deprecated_member_use
                          groupValue: _unitsMaintenanceDone,
                          // ignore: deprecated_member_use
                          onChanged: (v) =>
                              setModalState(() => _unitsMaintenanceDone = v),
                        ),
                      ),
                      Expanded(
                        child: RadioListTile<bool>(
                          contentPadding: EdgeInsets.zero,
                          title: const Text(
                            'Insumos nuevos',
                            style: TextStyle(fontSize: 13),
                          ),
                          value: false,
                          // ignore: deprecated_member_use
                          groupValue: _unitsMaintenanceDone,
                          // ignore: deprecated_member_use
                          onChanged: (v) =>
                              setModalState(() => _unitsMaintenanceDone = v),
                        ),
                      ),
                    ],
                  ),
                  if (_unitsMaintenanceDone == true) ...[
                    const SizedBox(height: 8),
                    TextField(
                      controller: _consumablesController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Impresiones de insumos existentes',
                      ),
                    ),
                  ],
                ],
                if (_isExternalTicket) ...[
                  const Divider(height: 24, color: AppColors.neutralSoft),
                  const Text(
                    'Equipo del cliente (no catalogado) — opcional',
                    style: TextStyle(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _externalBrandController,
                    decoration: const InputDecoration(labelText: 'Marca'),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _externalModelController,
                    decoration: const InputDecoration(labelText: 'Modelo'),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _externalCounterController,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(labelText: 'Contador'),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      OutlinedButton.icon(
                        onPressed: () async {
                          final photo = await DeviceCapture.takePhoto();
                          if (photo != null) {
                            setModalState(() => _counterPhoto = photo);
                          }
                        },
                        icon: const Icon(Icons.photo_camera_outlined, size: 18),
                        label: Text(
                          _counterPhoto == null
                              ? 'Foto del contador (opcional)'
                              : 'Cambiar foto',
                        ),
                      ),
                      if (_counterPhoto != null) ...[
                        const SizedBox(width: 12),
                        const Icon(
                          Icons.check_circle,
                          color: AppColors.signalBlue,
                        ),
                        const SizedBox(width: 4),
                        const Text('Foto lista'),
                      ],
                    ],
                  ),
                ],
                const SizedBox(height: 16),
                AnimatedGradientBorder(
                  child: SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: _isValid ? _submit : null,
                      child: const Text('Confirmar check-out'),
                    ),
                  ),
                ),
                if (!_isValid)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _hasInstallation
                          ? 'Completa área, lectura de contador y tipo de mantenimiento para cerrar esta instalación.'
                          : 'Completa la lectura y la foto del contador para cerrar esta orden.',
                      style: const TextStyle(
                        color: AppColors.signalRed,
                        fontSize: 12,
                      ),
                    ),
                  ),
                const SizedBox(height: 8),
              ],
            ),
          );
        },
      ),
    );
  }
}
