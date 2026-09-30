import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/city.dart';
import '../../services/api_client.dart';
import '../../state/technician_coverage_state.dart';
import '../../state/technician_linked_assets_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

/// Detalle de gestión de un técnico — cobertura geográfica y activos
/// vinculados (ver TechnicianLinkedAssetsState: gobierna qué ve el técnico
/// en "Lectura de contadores"). `extra` del push (ver router/app_router.dart)
/// trae el nombre para no tener que volver a listar técnicos solo para el
/// título del AppBar.
class TechnicianCoverageScreen extends StatelessWidget {
  const TechnicianCoverageScreen({
    super.key,
    required this.technicianId,
    this.technicianName,
  });

  final String technicianId;
  final String? technicianName;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => TechnicianCoverageState(ApiClient.instance, technicianId)..load()),
        ChangeNotifierProvider(create: (_) => TechnicianLinkedAssetsState(ApiClient.instance, technicianId)..load()),
      ],
      child: DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(
            title: Text(technicianName ?? 'Técnico'),
            bottom: const TabBar(
              tabs: [
                Tab(text: 'Cobertura'),
                Tab(text: 'Activos vinculados'),
              ],
            ),
          ),
          body: const TabBarView(
            children: [_CoverageBody(), _LinkedAssetsBody()],
          ),
        ),
      ),
    );
  }
}

class _CoverageBody extends StatelessWidget {
  const _CoverageBody();

  // Cascada Departamento→Ciudad con autocompletar (Autocomplete, nativo de
  // Flutter — sin dependencias nuevas): se escribe la inicial del
  // departamento para filtrarlo, se elige, y el mismo autocompletar de
  // Municipio queda acotado a las ciudades de ese departamento.
  Future<void> _showAddCityDialog(BuildContext context, TechnicianCoverageState state) async {
    String? departmentName;
    City? selectedCity;
    final selected = await showDialog<String>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          final departments = {for (final c in state.availableCities) c.stateOrProvince}.toList()..sort();
          final citiesInDepartment = state.availableCities.where((c) => c.stateOrProvince == departmentName).toList()
            ..sort((a, b) => a.name.compareTo(b.name));
          return AlertDialog(
            title: const Text('Agregar ciudad de cobertura'),
            // SizedBox: sin esto, el diálogo se dimensiona por el ancho
            // intrínseco del contenido en vez del ancho acotado del diálogo.
            content: SizedBox(
              width: double.maxFinite,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Autocomplete<String>(
                    optionsBuilder: (textEditingValue) {
                      final q = textEditingValue.text.trim().toLowerCase();
                      if (q.isEmpty) return departments;
                      return departments.where((d) => d.toLowerCase().contains(q));
                    },
                    onSelected: (value) => setDialogState(() {
                      departmentName = value;
                      selectedCity = null;
                    }),
                    fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
                      return TextField(
                        controller: controller,
                        focusNode: focusNode,
                        decoration: const InputDecoration(labelText: 'Departamento', prefixIcon: Icon(Icons.search)),
                      );
                    },
                  ),
                  const SizedBox(height: 12),
                  // key: fuerza a Flutter a recrear el widget (y su controlador de texto
                  // interno) cuando cambia el departamento — si no, el texto ya escrito
                  // de un departamento anterior se quedaría pegado.
                  Autocomplete<City>(
                    key: ValueKey(departmentName),
                    displayStringForOption: (city) => city.name,
                    optionsBuilder: (textEditingValue) {
                      if (departmentName == null) return const Iterable<City>.empty();
                      final q = textEditingValue.text.trim().toLowerCase();
                      if (q.isEmpty) return citiesInDepartment;
                      return citiesInDepartment.where((c) => c.name.toLowerCase().contains(q));
                    },
                    onSelected: (city) => setDialogState(() => selectedCity = city),
                    fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
                      return TextField(
                        controller: controller,
                        focusNode: focusNode,
                        enabled: departmentName != null,
                        decoration: const InputDecoration(labelText: 'Municipio', prefixIcon: Icon(Icons.search)),
                      );
                    },
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Cancelar')),
              FilledButton(
                onPressed: selectedCity == null ? null : () => Navigator.of(dialogContext).pop(selectedCity!.id),
                child: const Text('Agregar'),
              ),
            ],
          );
        },
      ),
    );
    if (selected != null && context.mounted) {
      final error = await state.addCity(selected);
      if (error != null && context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TechnicianCoverageState>(
      builder: (context, state, _) {
        if (state.loading && state.allCities.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.allCities.isEmpty) {
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
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FilledButton.icon(
                onPressed: state.busyWithAction || state.availableCities.isEmpty
                    ? null
                    : () => _showAddCityDialog(context, state),
                icon: const Icon(Icons.add_location_alt_outlined, size: 18),
                label: const Text('Agregar ciudad de cobertura'),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: state.coverage.isEmpty
                    ? const Center(
                        child: Text(
                          'Sin ciudades de cobertura todavía.',
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                      )
                    : ListView.builder(
                        itemCount: state.coverage.length,
                        itemBuilder: (context, index) {
                          final item = state.coverage[index];
                          return ClayCard(
                            child: Row(
                              children: [
                                const ClayIconBadge(
                                  icon: Icons.location_city_outlined,
                                  color: AppColors.signalBlue,
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    item.cityName,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(
                                    Icons.delete_outline,
                                    color: AppColors.signalRed,
                                  ),
                                  onPressed: state.busyWithAction
                                      ? null
                                      : () => state.removeCoverage(item.id),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _LinkedAssetsBody extends StatelessWidget {
  const _LinkedAssetsBody();

  Future<void> _showLinkAssetSheet(BuildContext context, TechnicianLinkedAssetsState state) async {
    final searchController = TextEditingController();
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(),
      builder: (sheetContext) {
        return DraggableScrollableSheet(
          initialChildSize: 0.85,
          minChildSize: 0.5,
          maxChildSize: 0.95,
          expand: false,
          builder: (sheetContext, scrollController) {
            return ChangeNotifierProvider.value(
              value: state,
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.of(sheetContext).viewInsets.bottom + MediaQuery.of(sheetContext).padding.bottom + 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('Vincular activo', style: Theme.of(sheetContext).textTheme.titleMedium),
                    const SizedBox(height: 12),
                    TextField(
                      controller: searchController,
                      decoration: const InputDecoration(
                        labelText: 'Buscar por ciudad, cliente, sede, serie o modelo',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: state.setSearchQuery,
                    ),
                    const SizedBox(height: 12),
                    Consumer<TechnicianLinkedAssetsState>(
                      builder: (context, state, _) {
                        final available = state.availableAssets;
                        return Row(
                          children: [
                            Expanded(
                              child: Text(
                                '${available.length} activo(s) disponibles',
                                style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                              ),
                            ),
                            TextButton.icon(
                              onPressed: state.busyWithAction || available.isEmpty
                                  ? null
                                  : () async {
                                      final error = await state.linkAllVisible();
                                      if (error != null && sheetContext.mounted) {
                                        ScaffoldMessenger.of(sheetContext).showSnackBar(SnackBar(content: Text(error)));
                                      }
                                    },
                              icon: const Icon(Icons.playlist_add_check, size: 18),
                              label: const Text('Vincular todos'),
                            ),
                          ],
                        );
                      },
                    ),
                    Expanded(
                      child: Consumer<TechnicianLinkedAssetsState>(
                        builder: (context, state, _) {
                          final available = state.availableAssets;
                          if (available.isEmpty) {
                            return const Center(
                              child: Text('No hay activos instalados disponibles para vincular.',
                                  style: TextStyle(color: AppColors.inkSecondary)),
                            );
                          }
                          return ListView.builder(
                            controller: scrollController,
                            itemCount: available.length,
                            itemBuilder: (context, index) {
                              final Asset asset = available[index];
                              return ClayCard(
                                padding: EdgeInsets.zero,
                                child: ListTile(
                                  leading: const ClayIconBadge(icon: Icons.print_outlined, color: AppColors.signalBlue),
                                  title: Text('${asset.assetBrandName} ${asset.model}'),
                                  subtitle: Text(
                                    '${asset.currentClientName ?? 'Sin cliente'}'
                                    '${asset.area != null && asset.area!.isNotEmpty ? ' · ${asset.area}' : ''}\n'
                                    'Serie: ${asset.serialNumber}'
                                    '${asset.cityName != null ? ' · ${asset.cityName}' : ''}',
                                    style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                  ),
                                  isThreeLine: true,
                                  contentPadding: const EdgeInsets.fromLTRB(16, 4, 8, 4),
                                  trailing: IconButton(
                                    icon: const Icon(Icons.add_circle_outline, color: AppColors.signalBlue),
                                    onPressed: state.busyWithAction
                                        ? null
                                        : () async {
                                            final error = await state.linkAsset(asset.id);
                                            if (error != null && sheetContext.mounted) {
                                              ScaffoldMessenger.of(sheetContext).showSnackBar(SnackBar(content: Text(error)));
                                            }
                                          },
                                  ),
                                ),
                              );
                            },
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TechnicianLinkedAssetsState>(
      builder: (context, state, _) {
        if (state.loading && state.linked.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.linked.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
            ),
          );
        }
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FilledButton.icon(
                onPressed: state.busyWithAction ? null : () => _showLinkAssetSheet(context, state),
                icon: const Icon(Icons.link, size: 18),
                label: const Text('Vincular activo'),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: state.linked.isEmpty
                    ? const Center(
                        child: Text('Este técnico no tiene activos vinculados todavía.\nNo verá equipos en "Lectura de contadores".',
                            textAlign: TextAlign.center, style: TextStyle(color: AppColors.inkSecondary)),
                      )
                    : ListView.builder(
                        itemCount: state.linked.length,
                        itemBuilder: (context, index) {
                          final item = state.linked[index];
                          return ClayCard(
                            child: Row(
                              children: [
                                const ClayIconBadge(icon: Icons.print_outlined, color: AppColors.signalBlue),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('${item.assetBrandName} ${item.model}', style: const TextStyle(fontWeight: FontWeight.w600)),
                                      Text(
                                        '${item.clientName ?? 'Sin cliente'}'
                                        '${item.area != null && item.area!.isNotEmpty ? ' · ${item.area}' : ''}\n'
                                        'Serie: ${item.serialNumber}'
                                        '${item.cityName != null ? ' · ${item.cityName}' : ''}',
                                        style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                      ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, color: AppColors.signalRed),
                                  onPressed: state.busyWithAction ? null : () => state.unlinkAsset(item.id),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}
