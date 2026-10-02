import 'package:flutter/material.dart';

import '../models/inventory.dart';
import '../services/api_client.dart';
import '../services/inventory_api.dart';
import '../theme/app_theme.dart';
import 'part_picker_sheet.dart';
import 'quantity_stepper.dart';

/// Sección "Piezas cambiadas" del check-out de tickets y órdenes. Con [loadKit] (orden que incluye consumibles)
/// carga el kit base del equipo como checklist, todo marcado; siempre ofrece "Agregar repuesto". Solo informa
/// el saldo de la zona (en rojo si no alcanza): nunca bloquea el cierre. Lo que el técnico deja marcado queda
/// en [selection].
class PartsSection extends StatefulWidget {
  const PartsSection({
    super.key,
    required this.selection,
    required this.api,
    required this.assetId,
    required this.loadKit,
  });

  final PartsSelection selection;
  final InventoryApi api;
  final String? assetId;
  final bool loadKit;

  @override
  State<PartsSection> createState() => _PartsSectionState();
}

class _PartsSectionState extends State<PartsSection> {
  bool _loading = false;
  String? _error;
  // Derivado del kit una sola vez (no por render).
  Map<String, List<VisitKitItem>> _groups = {};

  @override
  void initState() {
    super.initState();
    if (widget.loadKit) _loadKit();
  }

  Future<void> _loadKit() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final kit = await widget.api.getKit(widget.assetId);
      if (!mounted) return;
      widget.selection.setKit(kit);
      setState(() {
        _groups = kit.itemsByGroup;
        _loading = false;
      });
    } catch (e, st) {
      debugPrint('PartsSection._loadKit failed: $e\n$st');
      if (!mounted) return;
      setState(() {
        _error = e is ApiException
            ? e.message
            : 'No se pudo cargar el kit de piezas.';
        _loading = false;
      });
    }
  }

  Future<void> _addPart() async {
    final option = await PartPickerSheet.show(
      context,
      api: widget.api,
      assetId: widget.assetId,
    );
    if (option != null) widget.selection.addExtra(option);
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.selection,
      builder: (context, _) {
        final selection = widget.selection;
        final kit = selection.kit;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Divider(height: 24, color: AppColors.neutralSoft),
            const Text(
              'Piezas cambiadas',
              style: TextStyle(fontWeight: FontWeight.w600),
            ),
            if (widget.loadKit) ...[
              if (_loading)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 12),
                  child: Center(child: CircularProgressIndicator()),
                ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _error!,
                          style: const TextStyle(
                            color: AppColors.signalRed,
                            fontSize: 12,
                          ),
                        ),
                      ),
                      TextButton(
                        onPressed: _loadKit,
                        child: const Text('Reintentar'),
                      ),
                    ],
                  ),
                ),
              if (kit != null) ...[
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    'Se descuenta de: ${kit.locationName}'
                    '${kit.usesMainWarehouse ? ' (el municipio del equipo no tiene zona: bodega principal)' : ''}',
                    style: const TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ),
                  ),
                ),
                if (kit.items.isEmpty)
                  const Padding(
                    padding: EdgeInsets.only(top: 4),
                    child: Text(
                      'Este modelo no tiene kit base definido.',
                      style: TextStyle(
                        color: AppColors.inkSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ),
                for (final group in _groups.entries) ...[
                  Padding(
                    padding: const EdgeInsets.only(top: 10),
                    child: Text(
                      group.key.isEmpty ? 'Otras piezas' : group.key,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 12,
                        letterSpacing: 0.4,
                        color: AppColors.inkSecondary,
                      ),
                    ),
                  ),
                  for (final item in group.value) _kitRow(selection, item),
                ],
              ],
            ],
            if (selection.extras.isNotEmpty) ...[
              const Padding(
                padding: EdgeInsets.only(top: 10),
                child: Text(
                  'Repuestos agregados',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 12,
                    letterSpacing: 0.4,
                    color: AppColors.inkSecondary,
                  ),
                ),
              ),
              for (final extra in selection.extras) _extraRow(selection, extra),
            ],
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                onPressed: _addPart,
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Agregar repuesto'),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _stockLabel(int stock, {required bool short}) => Text(
    'Stock: $stock',
    style: AppTextStyles.tabularNumber.copyWith(
      fontSize: 12,
      fontWeight: short ? FontWeight.w700 : FontWeight.w400,
      color: short ? AppColors.signalRed : AppColors.inkSecondary,
    ),
  );

  Widget _kitRow(PartsSelection selection, VisitKitItem item) {
    final checked = selection.isChecked(item.itemId);
    final short = selection.kitItemShort(item);
    return Row(
      children: [
        Checkbox(
          value: checked,
          visualDensity: VisualDensity.compact,
          onChanged: (v) => selection.toggle(item.itemId, v ?? false),
        ),
        Expanded(
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () => selection.toggle(item.itemId, !checked),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.itemName),
                _stockLabel(item.stock, short: short),
                if (short)
                  const Text(
                    'Stock insuficiente (igual se registra)',
                    style: TextStyle(color: AppColors.signalRed, fontSize: 11),
                  ),
              ],
            ),
          ),
        ),
        if (checked)
          QuantityStepper(
            value: selection.quantityOf(item.itemId),
            onChanged: (q) => selection.setQuantity(item.itemId, q),
          ),
      ],
    );
  }

  Widget _extraRow(PartsSelection selection, ExtraPart extra) {
    final short = extra.stock < extra.quantity;
    return Row(
      children: [
        IconButton(
          visualDensity: VisualDensity.compact,
          tooltip: 'Quitar',
          onPressed: () => selection.removeExtra(extra.itemId),
          icon: const Icon(Icons.close, size: 20),
        ),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(extra.name),
              _stockLabel(extra.stock, short: short),
            ],
          ),
        ),
        QuantityStepper(
          value: extra.quantity,
          onChanged: (q) => selection.setExtraQuantity(extra.itemId, q),
        ),
      ],
    );
  }
}
