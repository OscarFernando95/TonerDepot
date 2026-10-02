import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/base_kit.dart';
import '../state/base_kit_state.dart';
import '../theme/app_theme.dart';
import 'base_kit_add_form.dart';
import 'clay_icon_badge.dart';
import 'clay_surface.dart';
import 'quantity_stepper.dart';

/// Tarjeta "Kit base de consumibles de la marca" (AssetBrandDetailView.vue): lista editable + agregar + guardar.
/// Requiere un [BaseKitState] en el árbol.
class BaseKitBrandCard extends StatelessWidget {
  const BaseKitBrandCard({super.key});

  Future<void> _save(BuildContext context, BaseKitState state) async {
    final error = await state.save();
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? 'Kit base de la marca guardado.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<BaseKitState>();
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Row(
            children: [
              ClayIconBadge(
                icon: Icons.inventory_2_outlined,
                color: AppColors.signalBlue,
              ),
              SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Kit base de consumibles de la marca',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          const Text(
            'Es lo que se sugiere descontar en cada visita. Todos los modelos de la marca lo heredan y cada uno puede '
            'ajustarlo con el botón Kit.',
            style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(height: 12),
          if (state.loading && state.rows.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.error != null && state.rows.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      state.error!,
                      style: const TextStyle(color: AppColors.signalRed),
                    ),
                  ),
                  TextButton(
                    onPressed: state.load,
                    child: const Text('Reintentar'),
                  ),
                ],
              ),
            )
          else if (state.rows.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'La marca todavía no tiene kit base.',
                style: TextStyle(color: AppColors.inkSecondary),
              ),
            )
          else
            for (final row in state.rows)
              _BrandKitRow(
                key: ValueKey('${state.version}:${row.itemId}'),
                row: row,
                groups: state.groups,
                enabled: !state.saving,
              ),
          const Divider(height: 24),
          BaseKitAddForm(
            search: state.api.searchItems,
            groups: state.groups,
            enabled: !state.saving && !state.loading,
            onAdd: state.add,
          ),
          if (state.hasMissingGroup)
            const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text(
                'Todas las filas necesitan unidad o grupo para poder guardar.',
                style: TextStyle(color: AppColors.signalRed, fontSize: 12),
              ),
            ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: state.canSave ? () => _save(context, state) : null,
            child: state.saving
                ? const SizedBox(
                    height: 18,
                    width: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Guardar kit'),
          ),
        ],
      ),
    );
  }
}

class _BrandKitRow extends StatelessWidget {
  const _BrandKitRow({
    super.key,
    required this.row,
    required this.groups,
    required this.enabled,
  });

  final BaseKitItem row;
  final List<String> groups;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final state = context.read<BaseKitState>();
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        padding: const EdgeInsets.fromLTRB(12, 8, 4, 8),
        decoration: BoxDecoration(
          color: AppColors.claySurfaceRaised,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.neutralSoft),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    row.itemName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                IconButton(
                  tooltip: 'Quitar ${row.itemName} del kit',
                  visualDensity: VisualDensity.compact,
                  onPressed: enabled ? () => state.remove(row.itemId) : null,
                  icon: const Icon(
                    Icons.delete_outline,
                    color: AppColors.signalRed,
                  ),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: BaseKitGroupField(
                initialValue: row.groupName,
                suggestions: groups,
                enabled: enabled,
                onChanged: (v) => state.setGroup(row.itemId, v),
              ),
            ),
            Row(
              children: [
                const Text(
                  'Cantidad',
                  style: TextStyle(color: AppColors.inkSecondary),
                ),
                const SizedBox(width: 8),
                QuantityStepper(
                  value: row.quantity,
                  max: baseKitMaxQuantity,
                  onChanged: enabled
                      ? (v) => state.setQuantity(row.itemId, v)
                      : (_) {},
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
