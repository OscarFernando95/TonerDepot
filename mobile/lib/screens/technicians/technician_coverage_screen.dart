import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/city.dart';
import '../../services/api_client.dart';
import '../../state/technician_coverage_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';

/// Cobertura geográfica de un técnico — agregar/quitar ciudades. `extra` del
/// push (ver router/app_router.dart) trae el nombre para no tener que
/// volver a listar técnicos solo para el título del AppBar.
class TechnicianCoverageScreen extends StatelessWidget {
  const TechnicianCoverageScreen({super.key, required this.technicianId, this.technicianName});

  final String technicianId;
  final String? technicianName;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TechnicianCoverageState(ApiClient.instance, technicianId)..load(),
      child: Scaffold(
        appBar: AppBar(title: Text(technicianName ?? 'Cobertura')),
        body: const _CoverageBody(),
      ),
    );
  }
}

class _CoverageBody extends StatelessWidget {
  const _CoverageBody();

  Future<void> _showAddCityDialog(BuildContext context, TechnicianCoverageState state) async {
    String? cityId;
    final selected = await showDialog<String>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Agregar ciudad'),
          // SizedBox+isExpanded: sin esto, el Dropdown se dimensiona por el
          // ancho intrínseco de sus items ("Ciudad — Departamento") en vez
          // del ancho acotado del diálogo, y desborda.
          content: SizedBox(
            width: double.maxFinite,
            child: DropdownButtonFormField<String>(
              initialValue: cityId,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Ciudad'),
              items: [
                for (final City city in state.availableCities)
                  DropdownMenuItem(
                    value: city.id,
                    child: Text('${city.name} — ${city.stateOrProvince}', overflow: TextOverflow.ellipsis),
                  ),
              ],
              onChanged: (value) => setDialogState(() => cityId = value),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Cancelar')),
            FilledButton(
              onPressed: cityId == null ? null : () => Navigator.of(dialogContext).pop(cityId),
              child: const Text('Agregar'),
            ),
          ],
        ),
      ),
    );
    if (selected != null && context.mounted) {
      final error = await state.addCity(selected);
      if (error != null && context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
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
                onPressed: state.busyWithAction || state.availableCities.isEmpty ? null : () => _showAddCityDialog(context, state),
                icon: const Icon(Icons.add_location_alt_outlined, size: 18),
                label: const Text('Agregar ciudad de cobertura'),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: state.coverage.isEmpty
                    ? const Center(child: Text('Sin ciudades de cobertura todavía.', style: TextStyle(color: AppColors.inkSecondary)))
                    : ListView.builder(
                        itemCount: state.coverage.length,
                        itemBuilder: (context, index) {
                          final item = state.coverage[index];
                          return ClayCard(
                            child: Row(
                              children: [
                                const Icon(Icons.location_city_outlined, color: AppColors.signalBlue),
                                const SizedBox(width: 12),
                                Expanded(child: Text(item.cityName, style: const TextStyle(fontWeight: FontWeight.w600))),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, color: AppColors.signalRed),
                                  onPressed: state.busyWithAction ? null : () => state.removeCoverage(item.id),
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
