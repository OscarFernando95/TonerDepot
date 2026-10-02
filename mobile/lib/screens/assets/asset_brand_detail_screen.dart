import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset_model.dart';
import '../../services/api_client.dart';
import '../../state/asset_brand_detail_state.dart';
import '../../state/base_kit_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/base_kit_brand_card.dart';
import '../../widgets/base_kit_model_sheet.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

class AssetBrandDetailScreen extends StatelessWidget {
  const AssetBrandDetailScreen({
    super.key,
    required this.brandId,
    this.brandName,
  });

  final String brandId;
  final String? brandName;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) =>
              AssetBrandDetailState(ApiClient.instance, brandId)..load(),
        ),
        // Kit base de consumibles de la marca (solo Staff, igual que esta pantalla).
        ChangeNotifierProvider(
          create: (_) => BaseKitState(ApiClient.instance, brandId)..load(),
        ),
      ],
      child: Scaffold(
        // Insignia de cabecera en el AppBar — este detalle no tiene una
        // ClaySurface de encabezado propia (el cuerpo es directamente la
        // lista de modelos), mismo ícono que asset_brands_list_screen.dart.
        appBar: AppBar(
          title: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const ClayIconBadge(
                icon: Icons.category_outlined,
                color: AppColors.signalBlue,
                size: 28,
                iconSize: 14,
              ),
              const SizedBox(width: 10),
              Flexible(
                child: Text(
                  brandName ?? 'Marca',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
        body: const _BrandDetailBody(),
      ),
    );
  }
}

class _BrandDetailBody extends StatelessWidget {
  const _BrandDetailBody();

  Future<void> _showModelDialog(
    BuildContext context,
    AssetBrandDetailState state, {
    AssetModel? existing,
  }) async {
    final nameController = TextEditingController(text: existing?.name ?? '');
    final generalPrintController = TextEditingController(
      text: existing?.generalPrintThreshold.toString() ?? '',
    );
    final generalMonthsController = TextEditingController(
      text: existing?.generalMonthsInterval.toString() ?? '',
    );
    final unitsPrintController = TextEditingController(
      text: existing?.unitsPrintThreshold.toString() ?? '',
    );
    final unitsMonthsController = TextEditingController(
      text: existing?.unitsMonthsInterval.toString() ?? '',
    );
    final consumablesController = TextEditingController(
      text: existing?.consumablesPrintThreshold.toString() ?? '',
    );

    bool isValid() =>
        nameController.text.trim().isNotEmpty &&
        int.tryParse(generalPrintController.text.trim()) != null &&
        int.tryParse(generalMonthsController.text.trim()) != null &&
        int.tryParse(unitsPrintController.text.trim()) != null &&
        int.tryParse(unitsMonthsController.text.trim()) != null &&
        int.tryParse(consumablesController.text.trim()) != null;

    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text(existing == null ? 'Nuevo modelo' : 'Editar modelo'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: nameController,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(labelText: 'Nombre *'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: generalPrintController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral mantenimiento general (impresiones) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: generalMonthsController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Intervalo mantenimiento general (meses) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: unitsPrintController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral unidades (impresiones) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: unitsMonthsController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Intervalo unidades (meses) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: consumablesController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral insumos (impresiones) *',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: isValid()
                  ? () => Navigator.of(dialogContext).pop(true)
                  : null,
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );

    if (result == true && context.mounted) {
      final params = (
        name: nameController.text.trim(),
        generalPrintThreshold: int.parse(generalPrintController.text.trim()),
        generalMonthsInterval: int.parse(generalMonthsController.text.trim()),
        unitsPrintThreshold: int.parse(unitsPrintController.text.trim()),
        unitsMonthsInterval: int.parse(unitsMonthsController.text.trim()),
        consumablesPrintThreshold: int.parse(consumablesController.text.trim()),
      );
      final error = existing == null
          ? await state.addModel(
              name: params.name,
              generalPrintThreshold: params.generalPrintThreshold,
              generalMonthsInterval: params.generalMonthsInterval,
              unitsPrintThreshold: params.unitsPrintThreshold,
              unitsMonthsInterval: params.unitsMonthsInterval,
              consumablesPrintThreshold: params.consumablesPrintThreshold,
            )
          : await state.updateModel(
              existing.id,
              name: params.name,
              generalPrintThreshold: params.generalPrintThreshold,
              generalMonthsInterval: params.generalMonthsInterval,
              unitsPrintThreshold: params.unitsPrintThreshold,
              unitsMonthsInterval: params.unitsMonthsInterval,
              consumablesPrintThreshold: params.consumablesPrintThreshold,
            );
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error ?? 'Modelo guardado.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<AssetBrandDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.models.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.models.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                state.error!,
                style: const TextStyle(color: AppColors.signalRed),
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            FilledButton.icon(
              onPressed: state.busyWithAction
                  ? null
                  : () => _showModelDialog(context, state),
              icon: const Icon(Icons.add, size: 18),
              label: const Text('Agregar modelo'),
            ),
            const SizedBox(height: 16),
            if (state.models.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(
                  child: Text(
                    'Sin modelos registrados.',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                ),
              )
            else
              for (final model in state.models)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: ClayCard(
                    onTap: state.busyWithAction
                        ? null
                        : () =>
                              _showModelDialog(context, state, existing: model),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                model.name,
                                style: const TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 15,
                                ),
                              ),
                            ),
                            TextButton.icon(
                              onPressed: () => BaseKitModelSheet.show(
                                context,
                                brandId: state.brandId,
                                modelId: model.id,
                                modelName: model.name,
                              ),
                              icon: const Icon(
                                Icons.inventory_2_outlined,
                                size: 16,
                              ),
                              label: const Text('Kit'),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'General: ${model.generalPrintThreshold} impr. / ${model.generalMonthsInterval} meses',
                          style: const TextStyle(
                            color: AppColors.inkSecondary,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          'Unidades: ${model.unitsPrintThreshold} impr. / ${model.unitsMonthsInterval} meses',
                          style: const TextStyle(
                            color: AppColors.inkSecondary,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          'Insumos: ${model.consumablesPrintThreshold} impr.',
                          style: const TextStyle(
                            color: AppColors.inkSecondary,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
            const SizedBox(height: 8),
            const BaseKitBrandCard(),
            const SizedBox(height: 16),
          ],
        );
      },
    );
  }
}
