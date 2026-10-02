import 'package:flutter/material.dart';

import '../models/inventory.dart';
import '../models/meter_reading_asset.dart';
import '../services/api_client.dart';
import '../services/inventory_api.dart';
import '../services/realtime_service.dart';
import '../theme/app_theme.dart';
import 'clay_date_field.dart';
import 'clay_segmented_control.dart';
import 'part_picker_sheet.dart';
import 'quantity_stepper.dart';

String _formatDateTime(DateTime utc) {
  final d = utc.toLocal();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${two(d.day)}/${two(d.month)}/${d.year} ${two(d.hour)}:${two(d.minute)}';
}

/// Formulario de tóner por máquina: ítem de tóner, cantidad, entregado al usuario vs. cambiado por el técnico,
/// fecha (por defecto el momento del registro), contador (obligatorio) y notas opcionales; debajo, los últimos registros de la
/// máquina. Devuelve el [TonerEntry] registrado (con su `stockWarning`, si lo hubo).
class TonerSheet extends StatefulWidget {
  const TonerSheet({super.key, required this.api, required this.asset});

  final InventoryApi api;
  final MeterReadingAsset asset;

  static Future<TonerEntry?> show(
    BuildContext context, {
    required InventoryApi api,
    required MeterReadingAsset asset,
  }) {
    return showModalBottomSheet<TonerEntry>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => TonerSheet(api: api, asset: asset),
    );
  }

  @override
  State<TonerSheet> createState() => _TonerSheetState();
}

class _TonerSheetState extends State<TonerSheet> {
  final _counterController = TextEditingController();
  final _notesController = TextEditingController();
  PartOption? _item;
  int _quantity = 1;
  bool _deliveredToUser = false;
  // null = "el momento del registro" (se resuelve al enviar).
  DateTime? _date;
  bool _submitting = false;
  String? _error;

  bool _historyLoading = true;
  String? _historyError;
  List<TonerEntry> _history = [];
  late final VoidCallback _unsubscribe;

  @override
  void initState() {
    super.initState();
    _loadHistory();
    // Cualquier movimiento de inventario (de otro dispositivo o de la web) refresca el historial en silencio.
    _unsubscribe = RealtimeService.instance.subscribe(['Inventory'], (_) {
      if (!_submitting) _loadHistory(silent: true);
    });
  }

  @override
  void dispose() {
    _unsubscribe();
    _counterController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _loadHistory({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _historyLoading = true;
        _historyError = null;
      });
    }
    try {
      final page = await widget.api.listToner(widget.asset.assetId);
      if (!mounted) return;
      setState(() {
        _history = page.items;
        _historyLoading = false;
      });
    } catch (e, st) {
      debugPrint('TonerSheet._loadHistory failed: $e\n$st');
      if (!mounted) return;
      setState(() {
        if (!silent) {
          _historyError = e is ApiException
              ? e.message
              : 'No se pudo cargar el historial.';
        }
        _historyLoading = false;
      });
    }
  }

  Future<void> _pickItem() async {
    final option = await PartPickerSheet.show(
      context,
      api: widget.api,
      assetId: widget.asset.assetId,
      category: 'Toner',
      title: 'Elegir tóner',
    );
    if (option != null && mounted) setState(() => _item = option);
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? now,
      firstDate: DateTime(2020),
      lastDate: now,
    );
    if (picked != null && mounted) setState(() => _date = picked);
  }

  String get _dateLabel {
    final d = _date;
    if (d == null) return 'Ahora (al registrar)';
    final now = DateTime.now();
    if (d.year == now.year && d.month == now.month && d.day == now.day) {
      return 'Hoy (al registrar)';
    }
    return '${d.day}/${d.month}/${d.year}';
  }

  Future<void> _submit() async {
    final item = _item;
    if (item == null || _submitting) return;
    final counterText = _counterController.text.trim();
    // Obligatorio: con el contador de cada cambio se mide cuánto dura un tóner en esa máquina.
    final counter = int.tryParse(counterText);
    if (counter == null) {
      setState(
        () => _error = counterText.isEmpty
            ? 'El contador de la máquina es obligatorio.'
            : 'El contador debe ser un número entero.',
      );
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final entry = await widget.api.registerToner(
        assetId: widget.asset.assetId,
        itemId: item.itemId,
        quantity: _quantity,
        deliveredToUser: _deliveredToUser,
        // La hora se fija al enviar, no al abrir el formulario.
        occurredAt: resolveTonerOccurredAt(_date, DateTime.now()),
        counterValue: counter,
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
      );
      if (!mounted) return;
      Navigator.of(context).pop(entry);
    } catch (e, st) {
      debugPrint('TonerSheet._submit failed: $e\n$st');
      if (!mounted) return;
      setState(() {
        _error = e is ApiException
            ? e.message
            : 'No se pudo registrar el tóner.';
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final asset = widget.asset;
    return Padding(
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom: media.viewInsets.bottom + media.padding.bottom + 16,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Tóner', style: Theme.of(context).textTheme.titleLarge),
            Text(
              '${asset.assetBrandName} ${asset.model} · Serie: ${asset.serialNumber}',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _submitting ? null : _pickItem,
              icon: const Icon(Icons.search, size: 18),
              label: Text(
                _item == null
                    ? 'Elegir tóner *'
                    : '${_item!.name} (stock: ${_item!.stock})',
                overflow: TextOverflow.ellipsis,
              ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Text('Cantidad'),
                const Spacer(),
                QuantityStepper(
                  value: _quantity,
                  onChanged: (q) => setState(() => _quantity = q),
                ),
              ],
            ),
            const SizedBox(height: 4),
            ClaySegmentedControl<bool>(
              selected: _deliveredToUser,
              onChanged: (v) => setState(() => _deliveredToUser = v),
              segments: const [
                ClaySegment(value: false, label: 'Cambiado por mí'),
                ClaySegment(value: true, label: 'Entregado al usuario'),
              ],
            ),
            const SizedBox(height: 12),
            ClayDateField(label: 'Fecha', value: _dateLabel, onTap: _pickDate),
            const SizedBox(height: 12),
            TextField(
              controller: _counterController,
              keyboardType: TextInputType.number,
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(
                labelText: 'Contador de la máquina *',
              ),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _notesController,
              minLines: 1,
              maxLines: 3,
              decoration: const InputDecoration(labelText: 'Notas (opcional)'),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  _error!,
                  style: const TextStyle(
                    color: AppColors.signalRed,
                    fontSize: 12,
                  ),
                ),
              ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed:
                    _item == null ||
                        _submitting ||
                        _counterController.text.trim().isEmpty
                    ? null
                    : _submit,
                child: Text(_submitting ? 'Registrando…' : 'Registrar tóner'),
              ),
            ),
            const Divider(height: 28, color: AppColors.neutralSoft),
            const Text(
              'Últimos registros',
              style: TextStyle(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 4),
            _buildHistory(),
          ],
        ),
      ),
    );
  }

  Widget _buildHistory() {
    if (_historyLoading && _history.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 12),
        child: Center(child: CircularProgressIndicator()),
      );
    }
    if (_historyError != null && _history.isEmpty) {
      return Row(
        children: [
          Expanded(
            child: Text(
              _historyError!,
              style: const TextStyle(color: AppColors.signalRed, fontSize: 12),
            ),
          ),
          TextButton(onPressed: _loadHistory, child: const Text('Reintentar')),
        ],
      );
    }
    if (_history.isEmpty) {
      return const Text(
        'Sin registros de tóner en esta máquina.',
        style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
      );
    }
    return Column(
      children: [
        for (final e in _history)
          ListTile(
            contentPadding: EdgeInsets.zero,
            dense: true,
            title: Text('${e.quantity} × ${e.itemName}'),
            subtitle: Text(
              [
                _formatDateTime(e.occurredAt),
                if (e.registeredBy != null) e.registeredBy!,
                if (e.counterValue != null) 'Contador: ${e.counterValue}',
                if (e.notes != null && e.notes!.isNotEmpty) e.notes!,
              ].join(' · '),
              style: const TextStyle(fontSize: 12),
            ),
          ),
      ],
    );
  }
}
