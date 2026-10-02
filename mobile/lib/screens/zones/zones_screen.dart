import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/city.dart';
import '../../models/zone.dart';
import '../../services/api_client.dart';
import '../../state/zones_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

/// Gestión de zonas: una zona agrupa municipios y se asigna a los técnicos
/// (su cobertura se deriva de ellas). Solo Staff.
class ZonesScreen extends StatelessWidget {
  const ZonesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => ZonesState(ApiClient.instance)..load(),
      child: Scaffold(
        backgroundColor: Colors.transparent,
        floatingActionButton: Builder(
          builder: (context) => FloatingActionButton.extended(
            onPressed: () => _createZone(context),
            icon: const Icon(Icons.add),
            label: const Text('Nueva zona'),
          ),
        ),
        body: const _ZonesList(),
      ),
    );
  }
}

void _snack(BuildContext context, String message) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
}

/// Pide un nombre de zona. null = cancelado.
Future<String?> _askName(BuildContext context, {required String title, String initial = ''}) {
  final controller = TextEditingController(text: initial);
  return showDialog<String>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (dialogContext, setDialogState) => AlertDialog(
        title: Text(title),
        content: TextField(
          controller: controller,
          autofocus: true,
          maxLength: 100,
          onChanged: (_) => setDialogState(() {}),
          decoration: const InputDecoration(labelText: 'Nombre *'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Cancelar')),
          FilledButton(
            onPressed: controller.text.trim().isEmpty
                ? null
                : () => Navigator.of(dialogContext).pop(controller.text.trim()),
            child: const Text('Guardar'),
          ),
        ],
      ),
    ),
  );
}

Future<void> _createZone(BuildContext context) async {
  final state = context.read<ZonesState>();
  final name = await _askName(context, title: 'Nueva zona');
  if (name == null || !context.mounted) return;
  final error = await state.create(name);
  if (context.mounted) _snack(context, error ?? 'Zona creada.');
}

Future<void> _renameZone(BuildContext context, Zone zone) async {
  final state = context.read<ZonesState>();
  final name = await _askName(context, title: 'Renombrar zona', initial: zone.name);
  if (name == null || name == zone.name || !context.mounted) return;
  final error = await state.rename(zone.id, name);
  if (context.mounted) _snack(context, error ?? 'Zona renombrada.');
}

Future<void> _deleteZone(BuildContext context, Zone zone) async {
  final state = context.read<ZonesState>();
  final hasTechnicians = zone.technicianCount > 0;
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: const Text('Eliminar zona'),
      content: Text(
        hasTechnicians
            ? 'La zona "${zone.name}" tiene ${zone.technicianCount} técnico(s) asignado(s) y no se puede eliminar. '
                  'Quítala primero de sus técnicos.'
            : '¿Eliminar la zona "${zone.name}"? Sus ${zone.cities.length} municipio(s) quedarán sin zona.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: Text(hasTechnicians ? 'Entendido' : 'Cancelar'),
        ),
        if (!hasTechnicians)
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.signalRed),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Eliminar'),
          ),
      ],
    ),
  );
  if (confirmed != true || !context.mounted) return;
  final error = await state.delete(zone.id);
  if (context.mounted) _snack(context, error ?? 'Zona eliminada.');
}

Future<void> _editCities(BuildContext context, Zone zone) async {
  final state = context.read<ZonesState>();
  List<City> cities;
  try {
    cities = await state.loadCities();
  } catch (e, st) {
    debugPrint('ZonesScreen._editCities loadCities failed: $e\n$st');
    if (context.mounted) {
      _snack(context, e is ApiException ? e.message : 'No se pudieron cargar los municipios.');
    }
    return;
  }
  if (!context.mounted) return;
  final selected = await showModalBottomSheet<Set<String>>(
    context: context,
    isScrollControlled: true,
    backgroundColor: AppColors.claySurface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
    ),
    builder: (sheetContext) => _CityPickerSheet(
      zone: zone,
      cities: cities,
      otherZoneByCity: ZoneCityPicking.otherZoneByCity(state.zones, zone.id),
    ),
  );
  if (selected == null || !context.mounted) return;
  final error = await state.setCities(zone.id, selected.toList());
  if (context.mounted) _snack(context, error ?? 'Municipios actualizados.');
}

class _ZonesList extends StatelessWidget {
  const _ZonesList();

  @override
  Widget build(BuildContext context) {
    return Consumer<ZonesState>(
      builder: (context, state, _) {
        if (state.loading && state.zones.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.zones.isEmpty) {
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
        return RefreshIndicator(
          onRefresh: state.load,
          child: state.zones.isEmpty
              ? ListView(
                  children: const [
                    SizedBox(height: 120),
                    Center(
                      child: Padding(
                        padding: EdgeInsets.symmetric(horizontal: 24),
                        child: Text(
                          'Todavía no hay zonas.\nCrea una con "Nueva zona" y agrégale municipios.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                      ),
                    ),
                  ],
                )
              : ListView.builder(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                  itemCount: state.zones.length,
                  itemBuilder: (context, index) => _ZoneCard(zone: state.zones[index], busy: state.busyWithAction),
                ),
        );
      },
    );
  }
}

class _ZoneCard extends StatelessWidget {
  const _ZoneCard({required this.zone, required this.busy});

  final Zone zone;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const ClayIconBadge(icon: Icons.map_outlined, color: AppColors.signalBlue),
              const SizedBox(width: 10),
              Expanded(
                child: Text(zone.name, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
              ),
              Text(
                '${zone.technicianCount} técnico(s)',
                style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            zone.cities.isEmpty
                ? 'Sin municipios'
                : '${zone.cities.length} municipio(s): ${zone.cities.map((c) => c.name).join(', ')}',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(height: 4),
          Wrap(
            children: [
              TextButton.icon(
                onPressed: busy ? null : () => _editCities(context, zone),
                icon: const Icon(Icons.location_city_outlined, size: 18),
                label: const Text('Municipios'),
              ),
              TextButton.icon(
                onPressed: busy ? null : () => _renameZone(context, zone),
                icon: const Icon(Icons.edit_outlined, size: 18),
                label: const Text('Renombrar'),
              ),
              TextButton.icon(
                onPressed: busy ? null : () => _deleteZone(context, zone),
                style: TextButton.styleFrom(foregroundColor: AppColors.signalRed),
                icon: const Icon(Icons.delete_outline, size: 18),
                label: const Text('Eliminar'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Selector multi-selección con búsqueda de los municipios de una zona. Los
/// que hoy están en otra zona muestran su nombre: al guardar se mueven acá.
class _CityPickerSheet extends StatefulWidget {
  const _CityPickerSheet({required this.zone, required this.cities, required this.otherZoneByCity});

  final Zone zone;
  final List<City> cities;
  final Map<String, String> otherZoneByCity;

  @override
  State<_CityPickerSheet> createState() => _CityPickerSheetState();
}

class _CityPickerSheetState extends State<_CityPickerSheet> {
  late final Set<String> _selected = {for (final c in widget.zone.cities) c.id};
  late final List<City> _sorted = [...widget.cities]
    ..sort((a, b) {
      final byDept = a.stateOrProvince.compareTo(b.stateOrProvince);
      return byDept != 0 ? byDept : a.name.compareTo(b.name);
    });
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final visible = ZoneCityPicking.filter<City>(
      _sorted,
      _query,
      name: (c) => c.name,
      department: (c) => c.stateOrProvince,
    );
    final moved = ZoneCityPicking.movedCount(_selected, widget.otherZoneByCity);
    return DraggableScrollableSheet(
      initialChildSize: 0.9,
      minChildSize: 0.5,
      maxChildSize: 0.95,
      expand: false,
      builder: (context, scrollController) => Padding(
        padding: EdgeInsets.fromLTRB(
          16,
          16,
          16,
          MediaQuery.of(context).viewInsets.bottom + MediaQuery.of(context).padding.bottom + 16,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Municipios de ${widget.zone.name}', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            TextField(
              onChanged: (v) => setState(() => _query = v),
              decoration: const InputDecoration(
                labelText: 'Buscar municipio o departamento',
                prefixIcon: Icon(Icons.search),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              '${_selected.length} seleccionado(s)'
              '${moved > 0 ? ' · $moved se moverá(n) desde otra zona' : ''}',
              style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
            Expanded(
              child: visible.isEmpty
                  ? const Center(
                      child: Text('Sin resultados.', style: TextStyle(color: AppColors.inkSecondary)),
                    )
                  : ListView.builder(
                      controller: scrollController,
                      itemCount: visible.length,
                      itemBuilder: (context, index) {
                        final city = visible[index];
                        final other = widget.otherZoneByCity[city.id];
                        return CheckboxListTile(
                          dense: true,
                          contentPadding: EdgeInsets.zero,
                          controlAffinity: ListTileControlAffinity.leading,
                          value: _selected.contains(city.id),
                          title: Text(city.name),
                          subtitle: Text(
                            other == null ? city.stateOrProvince : '${city.stateOrProvince} · hoy en "$other"',
                            style: TextStyle(
                              fontSize: 12,
                              color: other == null ? AppColors.inkSecondary : AppColors.signalAmber,
                            ),
                          ),
                          onChanged: (checked) => setState(() {
                            if (checked == true) {
                              _selected.add(city.id);
                            } else {
                              _selected.remove(city.id);
                            }
                          }),
                        );
                      },
                    ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.of(context).pop(),
                    child: const Text('Cancelar'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: () => Navigator.of(context).pop(Set<String>.of(_selected)),
                    child: const Text('Guardar'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
