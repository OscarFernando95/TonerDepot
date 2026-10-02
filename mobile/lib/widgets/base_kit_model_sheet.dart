import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/base_kit.dart';
import '../services/api_client.dart';
import '../state/base_kit_model_state.dart';
import '../theme/app_theme.dart';
import 'base_kit_add_form.dart';
import 'quantity_stepper.dart';

/// Hoja "Kit — {modelo}": kit efectivo del modelo (heredado de la marca + ajustes). Se puede excluir/incluir cada
/// ítem, ajustar unidad y cantidad, y agregar piezas solo para este modelo. Al guardar solo se mandan los ajustes.
class BaseKitModelSheet extends StatelessWidget {
  const BaseKitModelSheet({
    super.key,
    required this.brandId,
    required this.modelId,
    required this.modelName,
  });

  final String brandId;
  final String modelId;
  final String modelName;

  /// Devuelve true si se guardó.
  static Future<bool?> show(
    BuildContext context, {
    required String brandId,
    required String modelId,
    required String modelName,
  }) {
    return showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => BaseKitModelSheet(
        brandId: brandId,
        modelId: modelId,
        modelName: modelName,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) =>
          BaseKitModelState(ApiClient.instance, brandId, modelId)..load(),
      child: _SheetBody(modelName: modelName),
    );
  }
}

class _SheetBody extends StatelessWidget {
  const _SheetBody({required this.modelName});

  final String modelName;

  Future<void> _save(BuildContext context, BaseKitModelState state) async {
    final error = await state.save();
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(error ?? 'Kit del modelo guardado.')),
    );
    if (error == null) Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<BaseKitModelState>();
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 8, 0),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    'Kit base — $modelName',
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                IconButton(
                  tooltip: 'Cerrar',
                  onPressed: () => Navigator.of(context).pop(false),
                  icon: const Icon(Icons.close),
                ),
              ],
            ),
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 0, 20, 8),
            child: Text(
              'Hereda el kit de la marca. Desmarca lo que este modelo no usa, ajusta cantidades o agrega piezas solo '
              'para este modelo.',
              style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
          ),
          Expanded(child: _content(context, state)),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
            child: Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: state.saving
                        ? null
                        : () => Navigator.of(context).pop(false),
                    child: const Text('Cancelar'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: state.canSave
                        ? () => _save(context, state)
                        : null,
                    child: state.saving
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Guardar'),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _content(BuildContext context, BaseKitModelState state) {
    if (state.loading) return const Center(child: CircularProgressIndicator());
    if (state.error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                state.error!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.signalRed),
              ),
              TextButton(
                onPressed: state.load,
                child: const Text('Reintentar'),
              ),
            ],
          ),
        ),
      );
    }
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
      children: [
        if (state.rows.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Text(
              'Ni la marca ni el modelo tienen kit base.',
              style: TextStyle(color: AppColors.inkSecondary),
            ),
          ),
        for (final row in state.rows)
          _ModelKitRowTile(
            key: ValueKey(row.itemId),
            row: row,
            groups: state.groups,
          ),
        if (state.hasMissingGroup)
          const Padding(
            padding: EdgeInsets.only(bottom: 8),
            child: Text(
              'Todas las filas incluidas necesitan unidad o grupo para poder guardar.',
              style: TextStyle(color: AppColors.signalRed, fontSize: 12),
            ),
          ),
        const Divider(height: 24),
        BaseKitAddForm(
          search: state.api.searchItems,
          groups: state.groups,
          enabled: !state.saving,
          itemHint: 'Agregar solo a este modelo',
          onAdd: state.addModelOnly,
        ),
      ],
    );
  }
}

class _ModelKitRowTile extends StatelessWidget {
  const _ModelKitRowTile({super.key, required this.row, required this.groups});

  final ModelKitRow row;
  final List<String> groups;

  @override
  Widget build(BuildContext context) {
    final state = context.read<BaseKitModelState>();
    final origin = row.origin;
    final adjustedColor = origin == ModelKitOrigin.inherited
        ? AppColors.neutral
        : AppColors.signalAmber;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        padding: const EdgeInsets.fromLTRB(4, 8, 8, 8),
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
                Checkbox(
                  value: !row.excluded,
                  onChanged: (v) => state.setIncluded(row.itemId, v ?? false),
                ),
                Expanded(
                  child: Text(
                    row.itemName,
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      decoration: row.excluded
                          ? TextDecoration.lineThrough
                          : null,
                      color: row.excluded ? AppColors.inkSecondary : null,
                    ),
                  ),
                ),
                if (!row.fromBrand)
                  IconButton(
                    tooltip: 'Quitar ${row.itemName} del modelo',
                    visualDensity: VisualDensity.compact,
                    onPressed: () => state.removeModelOnly(row.itemId),
                    icon: const Icon(
                      Icons.delete_outline,
                      color: AppColors.signalRed,
                    ),
                  ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.only(left: 12),
              child: Wrap(
                spacing: 8,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 3,
                    ),
                    decoration: BoxDecoration(
                      color: adjustedColor.withValues(alpha: 0.14),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      origin.label,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: origin == ModelKitOrigin.inherited
                            ? AppColors.inkSecondary
                            : const Color(0xFF8A6410),
                      ),
                    ),
                  ),
                  if (row.excluded)
                    const Text(
                      'No se usa en este modelo',
                      style: TextStyle(
                        color: AppColors.inkSecondary,
                        fontSize: 11,
                      ),
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 0, 0),
              child: BaseKitGroupField(
                initialValue: row.groupName,
                suggestions: groups,
                enabled: !row.excluded,
                onChanged: (v) => state.setGroup(row.itemId, v),
              ),
            ),
            Padding(
              padding: const EdgeInsets.only(left: 12),
              child: Row(
                children: [
                  const Text(
                    'Cantidad',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                  const SizedBox(width: 8),
                  QuantityStepper(
                    value: row.quantity,
                    max: baseKitMaxQuantity,
                    onChanged: row.excluded
                        ? (_) {}
                        : (v) => state.setQuantity(row.itemId, v),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
