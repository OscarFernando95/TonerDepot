import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/client_location.dart';
import '../../models/contract_asset.dart';
import '../../services/api_client.dart';
import '../../services/asset_api.dart';
import '../../services/client_location_api.dart';
import '../../models/paged_result.dart';
import '../../state/contract_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../utils/date_only.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';

class ContractDetailScreen extends StatelessWidget {
  const ContractDetailScreen({super.key, required this.contractId});

  final String contractId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) =>
          ContractDetailState(ApiClient.instance, contractId)..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Contrato')),
        body: const _ContractDetailBody(),
      ),
    );
  }
}

class _ContractDetailBody extends StatelessWidget {
  const _ContractDetailBody();

  String _formatDate(String iso) => iso.split('T').first;

  Future<void> _showEditDialog(
    BuildContext context,
    ContractDetailState state,
  ) async {
    final contract = state.contract!;
    DateTime startDate = DateTime.parse(contract.startDate);
    DateTime? endDate = contract.endDate == null
        ? null
        : DateTime.parse(contract.endDate!);
    final printsController = TextEditingController(
      text: contract.includedPrintsPerMonth?.toString() ?? '',
    );
    final priceController = TextEditingController(
      text: contract.pricePerExtraPage?.toString() ?? '',
    );
    final notesController = TextEditingController(text: contract.notes ?? '');

    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Editar contrato'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Fecha de inicio'),
                    subtitle: Text(formatDateOnly(startDate)),
                    trailing: const Icon(
                      Icons.calendar_today_outlined,
                      size: 18,
                    ),
                    onTap: () async {
                      final picked = await showDatePicker(
                        context: dialogContext,
                        initialDate: startDate,
                        firstDate: DateTime(2020),
                        lastDate: DateTime(2100),
                      );
                      if (picked != null) {
                        setDialogState(() => startDate = picked);
                      }
                    },
                  ),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Fecha de fin'),
                    subtitle: Text(
                      endDate == null
                          ? 'Sin definir'
                          : formatDateOnly(endDate!),
                    ),
                    trailing: const Icon(
                      Icons.calendar_today_outlined,
                      size: 18,
                    ),
                    onTap: () async {
                      final picked = await showDatePicker(
                        context: dialogContext,
                        initialDate: endDate ?? startDate,
                        firstDate: startDate,
                        lastDate: DateTime(2100),
                      );
                      if (picked != null) {
                        setDialogState(() => endDate = picked);
                      }
                    },
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: printsController,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      labelText: 'Impresiones incluidas / mes',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: priceController,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: const InputDecoration(
                      labelText: 'Precio por página extra',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: notesController,
                    minLines: 2,
                    maxLines: 4,
                    decoration: const InputDecoration(labelText: 'Notas'),
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
              onPressed: () => Navigator.of(dialogContext).pop(true),
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );

    if (result == true && context.mounted) {
      final error = await state.updateContract(
        startDate: formatDateOnly(startDate),
        endDate: endDate == null ? null : formatDateOnly(endDate!),
        includedPrintsPerMonth: int.tryParse(printsController.text.trim()),
        pricePerExtraPage: double.tryParse(priceController.text.trim()),
        notes: notesController.text.trim().isEmpty
            ? null
            : notesController.text.trim(),
      );
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error ?? 'Contrato actualizado.')),
        );
      }
    }
  }

  Future<void> _showStatusDialog(
    BuildContext context,
    ContractDetailState state,
  ) async {
    String status = state.contract!.status;
    final result = await showDialog<String>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Cambiar estado'),
          content: DropdownButtonFormField<String>(
            initialValue: status,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Estado'),
            items: const [
              DropdownMenuItem(value: 'Activo', child: Text('Activo')),
              DropdownMenuItem(value: 'Vencido', child: Text('Vencido')),
              DropdownMenuItem(value: 'Cancelado', child: Text('Cancelado')),
            ],
            onChanged: (value) =>
                setDialogState(() => status = value ?? status),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(status),
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );
    if (result != null && context.mounted) {
      final error = await state.setStatus(result);
      if (context.mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error ?? 'Estado actualizado.')));
      }
    }
  }

  Future<void> _showAddAssetDialog(
    BuildContext context,
    ContractDetailState state,
  ) async {
    final contract = state.contract!;
    List<Asset> availableAssets = [];
    List<ClientLocation> locations = [];
    String? assetId;
    String? clientLocationId;
    bool loading = true;
    String? loadError;

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          if (loading) {
            Future.wait([
                  AssetApi(ApiClient.instance)
                      .listCatalog(page: 1, pageSize: 200),
                  ClientLocationApi(ApiClient.instance)
                      .listForClient(contract.clientId),
                ])
                .then((results) {
                  final page = results[0] as PagedResult<Asset>;
                  availableAssets = page.items
                      .where((a) => a.lifecycleStatus == 'EnBodega')
                      .toList();
                  locations = results[1] as List<ClientLocation>;
                  setDialogState(() => loading = false);
                })
                .catchError((e) {
                  setDialogState(() {
                    loading = false;
                    loadError = e is ApiException
                        ? e.message
                        : 'No se pudo cargar la información.';
                  });
                });
            return const AlertDialog(
              content: SizedBox(
                height: 120,
                child: Center(child: CircularProgressIndicator()),
              ),
            );
          }
          if (loadError != null) {
            return AlertDialog(
              content: Text(
                loadError!,
                style: const TextStyle(color: AppColors.signalRed),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(),
                  child: const Text('Cerrar'),
                ),
              ],
            );
          }
          return AlertDialog(
            title: const Text('Agregar activo'),
            content: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (availableAssets.isEmpty)
                      const Padding(
                        padding: EdgeInsets.only(bottom: 8),
                        child: Text(
                          'No hay activos en bodega disponibles.',
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                      ),
                    DropdownButtonFormField<String>(
                      initialValue: assetId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Activo (en bodega) *',
                      ),
                      items: [
                        for (final a in availableAssets)
                          DropdownMenuItem(
                            value: a.id,
                            child: Text(
                              '${a.assetBrandName} ${a.model} — ${a.serialNumber}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: (value) =>
                          setDialogState(() => assetId = value),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: clientLocationId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Sede *'),
                      items: [
                        for (final l in locations)
                          DropdownMenuItem(
                            value: l.id,
                            child: Text(
                              '${l.name} — ${l.cityName}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: (value) =>
                          setDialogState(() => clientLocationId = value),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: assetId == null || clientLocationId == null
                    ? null
                    : () async {
                        Navigator.of(dialogContext).pop();
                        final error = await state.addAsset(
                          assetId: assetId!,
                          clientLocationId: clientLocationId!,
                        );
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(error ?? 'Activo agregado.'),
                            ),
                          );
                        }
                      },
                child: const Text('Agregar'),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _confirmEndAsset(
    BuildContext context,
    ContractDetailState state,
    ContractAsset contractAsset,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('¿Finalizar este vínculo?'),
        content: Text(
          '${contractAsset.assetBrandName} ${contractAsset.assetModel} dejará de estar asociado a este contrato.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('No'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Sí, finalizar'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      final error = await state.endAssetAssociation(contractAsset.id);
      if (context.mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error ?? 'Vínculo finalizado.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ContractDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.contract == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.contract == null) {
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
        final contract = state.contract!;
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              ClaySurface(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            contract.clientName,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                        StatusChip.contract(contract.status),
                      ],
                    ),
                    Text(
                      'Vigencia: ${_formatDate(contract.startDate)}${contract.endDate != null ? ' a ${_formatDate(contract.endDate!)}' : ' (sin fecha de fin)'}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    if (contract.includedPrintsPerMonth != null)
                      Text(
                        'Impresiones incluidas/mes: ${contract.includedPrintsPerMonth}',
                        style: const TextStyle(color: AppColors.inkSecondary)
                            .merge(AppTextStyles.tabularNumber),
                      ),
                    if (contract.pricePerExtraPage != null)
                      Text(
                        'Precio por página extra: ${contract.pricePerExtraPage}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (contract.notes != null && contract.notes!.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          contract.notes!,
                          style: const TextStyle(
                            fontSize: 12,
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: state.busyWithAction
                                ? null
                                : () => _showEditDialog(context, state),
                            icon: const Icon(Icons.edit_outlined, size: 18),
                            label: const Text('Editar'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: FilledButton.icon(
                            onPressed: state.busyWithAction
                                ? null
                                : () => _showStatusDialog(context, state),
                            icon: const Icon(Icons.sync_alt, size: 18),
                            label: const Text('Cambiar estado'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Activos asociados',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  TextButton.icon(
                    onPressed: state.busyWithAction
                        ? null
                        : () => _showAddAssetDialog(context, state),
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('Agregar'),
                  ),
                ],
              ),
              if (state.contractAssets.isEmpty)
                const ClaySurface(
                  child: Text(
                    'Sin activos asociados.',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                )
              else
                for (final ca in state.contractAssets)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: ClaySurface(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  '${ca.assetBrandName} ${ca.assetModel}',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              StatusChip.activeState(ca.isActive),
                            ],
                          ),
                          Text(
                            'Serie: ${ca.assetSerialNumber}',
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                            ),
                          ),
                          Text(
                            'Vigencia: ${_formatDate(ca.startDate)}${ca.endDate != null ? ' a ${_formatDate(ca.endDate!)}' : ''}',
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                              fontSize: 12,
                            ),
                          ),
                          if (ca.area != null)
                            Text(
                              'Área: ${ca.area}',
                              style: const TextStyle(
                                color: AppColors.inkSecondary,
                                fontSize: 12,
                              ),
                            ),
                          if (ca.averageMonthlyPrints != null)
                            Text(
                              'Promedio mensual: ${ca.averageMonthlyPrints!.toStringAsFixed(0)} impr.',
                              style: const TextStyle(
                                color: AppColors.inkSecondary,
                                fontSize: 12,
                              ),
                            ),
                          if (ca.isActive) ...[
                            const SizedBox(height: 8),
                            OutlinedButton.icon(
                              onPressed: state.busyWithAction
                                  ? null
                                  : () => _confirmEndAsset(context, state, ca),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: AppColors.signalRed,
                              ),
                              icon: const Icon(Icons.link_off, size: 16),
                              label: const Text('Finalizar vínculo'),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
            ],
          ),
        );
      },
    );
  }
}
