import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../models/city.dart';
import '../../models/inventory_admin.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/inventory_state.dart';
import '../../theme/app_theme.dart';
import '../../utils/date_only.dart';
import '../../widgets/clay_choice_chip.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/department_city_picker.dart';
import '../../widgets/list_filter_dropdown.dart';

/// Inventario (espejo de InventoryView.vue): existencias, movimientos, catálogo y ubicaciones. Staff; la sede
/// principal solo la edita el Administrador. Sin precios (decisión de producto).
class InventoryScreen extends StatelessWidget {
  const InventoryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => InventoryState(ApiClient.instance)..loadAll(),
      child: const _InventoryBody(),
    );
  }
}

void _snack(BuildContext context, String message) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
}

class _InventoryBody extends StatefulWidget {
  const _InventoryBody();

  @override
  State<_InventoryBody> createState() => _InventoryBodyState();
}

class _InventoryBodyState extends State<_InventoryBody>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 4, vsync: this);

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        TabBar(
          controller: _tabs,
          isScrollable: true,
          tabAlignment: TabAlignment.start,
          labelColor: AppColors.signalBlue,
          unselectedLabelColor: AppColors.inkSecondary,
          indicatorColor: AppColors.signalBlue,
          tabs: const [
            Tab(text: 'Existencias'),
            Tab(text: 'Movimientos'),
            Tab(text: 'Catálogo'),
            Tab(text: 'Ubicaciones'),
          ],
        ),
        Expanded(
          child: TabBarView(
            controller: _tabs,
            children: const [
              _StockTab(),
              _MovementsTab(),
              _CatalogTab(),
              _LocationsTab(),
            ],
          ),
        ),
      ],
    );
  }
}

// ── Piezas comunes ─────────────────────────────────────────────────────────────────────────────────

const _listPadding = EdgeInsets.fromLTRB(16, 12, 16, 32);

class _ErrorText extends StatelessWidget {
  const _ErrorText(this.message);
  final String message;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 24),
    child: Text(
      message,
      style: const TextStyle(color: AppColors.signalRed),
      textAlign: TextAlign.center,
    ),
  );
}

class _EmptyText extends StatelessWidget {
  const _EmptyText(this.message);
  final String message;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 8),
    child: Text(
      message,
      textAlign: TextAlign.center,
      style: const TextStyle(color: AppColors.inkSecondary),
    ),
  );
}

class _LoadMoreButton extends StatelessWidget {
  const _LoadMoreButton({required this.loading, required this.onPressed});
  final bool loading;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 8),
    child: Center(
      child: OutlinedButton(
        onPressed: loading ? null : onPressed,
        child: loading
            ? const SizedBox(
                height: 18,
                width: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : const Text('Cargar más'),
      ),
    ),
  );
}

class _Pill extends StatelessWidget {
  const _Pill(this.text, this.color);
  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.14),
      borderRadius: BorderRadius.circular(999),
    ),
    child: Text(
      text,
      style: AppTextStyles.tabularNumber.copyWith(
        color: color,
        fontWeight: FontWeight.w700,
        fontSize: 13,
      ),
    ),
  );
}

IconData _categoryIcon(String category) => switch (category) {
  'Toner' => Icons.print_outlined,
  'Repuesto' => Icons.build_outlined,
  _ => Icons.inventory_2_outlined,
};

List<(String, String)> _locationOptions(InventoryState state) => [
  for (final l in state.locations) (l.id, l.name),
];

List<(String, String)> get _categoryOptions => [
  for (final e in inventoryCategories.entries) (e.key, e.value),
];

/// Abre una hoja inferior con el estilo de la app.
Future<T?> _showSheet<T>(BuildContext context, WidgetBuilder builder) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    backgroundColor: AppColors.claySurface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: builder,
  );
}

// ── Existencias ────────────────────────────────────────────────────────────────────────────────────

class _StockTab extends StatelessWidget {
  const _StockTab();

  Future<void> _operate(
    BuildContext context,
    InventoryState state,
    InventoryOperation kind,
  ) async {
    final ok = await _showSheet<bool>(
      context,
      (_) => ChangeNotifierProvider.value(
        value: state,
        child: _OperationSheet(kind: kind),
      ),
    );
    if (ok == true && context.mounted) {
      _snack(context, 'Movimiento registrado.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<InventoryState>();
    return RefreshIndicator(
      onRefresh: () => state.loadStock(silent: true),
      child: ListView(
        padding: _listPadding,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              FilledButton.icon(
                onPressed: () =>
                    _operate(context, state, InventoryOperation.entry),
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Registrar entrada'),
              ),
              OutlinedButton.icon(
                onPressed: () =>
                    _operate(context, state, InventoryOperation.transfer),
                icon: const Icon(Icons.swap_horiz, size: 18),
                label: const Text('Traspasar'),
              ),
              OutlinedButton.icon(
                onPressed: () =>
                    _operate(context, state, InventoryOperation.adjust),
                icon: const Icon(Icons.tune, size: 18),
                label: const Text('Ajustar'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              ListFilterDropdown(
                label: 'Ubicación',
                value: state.stockLocationId,
                options: _locationOptions(state),
                allLabel: 'Todas',
                onChanged: (v) => state.setStockFilter(locationId: v),
              ),
              ListFilterDropdown(
                label: 'Categoría',
                value: state.stockCategory,
                options: _categoryOptions,
                allLabel: 'Todas',
                onChanged: (v) => state.setStockFilter(category: v),
              ),
              ClayChoiceChip(
                label: const Text('Solo stock bajo'),
                selected: state.stockOnlyLow,
                onSelected: (v) => state.setStockFilter(onlyLow: v),
              ),
            ],
          ),
          const SizedBox(height: 8),
          if (state.stockLoading && state.stock.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.stockError != null && state.stock.isEmpty)
            _ErrorText(state.stockError!)
          else if (state.stock.isEmpty)
            const _EmptyText(
              'Sin existencias registradas todavía. Registra una entrada para empezar.',
            )
          else ...[
            for (final row in state.stock) _StockCard(row: row),
            if (state.stockHasMore)
              _LoadMoreButton(
                loading: state.stockLoadingMore,
                onPressed: () => state.loadStock(more: true),
              ),
          ],
        ],
      ),
    );
  }
}

class _StockCard extends StatelessWidget {
  const _StockCard({required this.row});
  final StockRow row;

  @override
  Widget build(BuildContext context) {
    final color = row.isLow ? AppColors.signalRed : AppColors.signalBlue;
    return ClayCard(
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          ClayIconBadge(icon: _categoryIcon(row.category), color: color),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  row.itemName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${inventoryCategoryLabel(row.category)} · ${row.locationName}',
                  style: const TextStyle(
                    color: AppColors.inkSecondary,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Mínimo: ${row.minimumStock}',
                  style: const TextStyle(
                    color: AppColors.inkSecondary,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              _Pill(
                '${row.quantity}',
                row.isLow ? AppColors.signalRed : AppColors.signalBlue,
              ),
              if (row.isLow)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    row.isNegative ? 'negativo' : 'bajo',
                    style: const TextStyle(
                      color: AppColors.signalRed,
                      fontSize: 12,
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

// ── Movimientos ────────────────────────────────────────────────────────────────────────────────────

class _MovementsTab extends StatelessWidget {
  const _MovementsTab();

  @override
  Widget build(BuildContext context) {
    final state = context.watch<InventoryState>();
    return RefreshIndicator(
      onRefresh: () => state.loadMovements(silent: true),
      child: ListView(
        padding: _listPadding,
        children: [
          ListFilterDropdown(
            label: 'Ubicación',
            value: state.movementsLocationId,
            options: _locationOptions(state),
            allLabel: 'Todas',
            onChanged: state.setMovementsLocation,
          ),
          const SizedBox(height: 8),
          if (state.movementsLoading && state.movements.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.movementsError != null && state.movements.isEmpty)
            _ErrorText(state.movementsError!)
          else if (state.movements.isEmpty)
            const _EmptyText('Sin movimientos.')
          else ...[
            for (final m in state.movements) _MovementCard(movement: m),
            if (state.movementsHasMore)
              _LoadMoreButton(
                loading: state.movementsLoadingMore,
                onPressed: () => state.loadMovements(more: true),
              ),
          ],
        ],
      ),
    );
  }
}

class _MovementCard extends StatelessWidget {
  const _MovementCard({required this.movement});
  final InventoryMovement movement;

  @override
  Widget build(BuildContext context) {
    final negative = movement.delta < 0;
    final color = negative ? AppColors.signalRed : AppColors.signalBlue;
    final who = movement.createdByUserName;
    final notes = movement.notes;
    return ClayCard(
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClayIconBadge(
            icon: negative ? Icons.arrow_downward : Icons.arrow_upward,
            color: color,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  movement.itemName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${movement.typeLabel} · ${movement.locationName}',
                  style: const TextStyle(
                    color: AppColors.inkSecondary,
                    fontSize: 12,
                  ),
                ),
                if (who != null && who.isNotEmpty)
                  Text(
                    'Registró: $who',
                    style: const TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ),
                  ),
                if (notes != null && notes.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(notes, style: const TextStyle(fontSize: 12)),
                  ),
                Padding(
                  padding: const EdgeInsets.only(top: 2),
                  child: Text(
                    formatDateTimeShort(movement.occurredAt),
                    style: const TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(
            movement.signedQuantity,
            style: AppTextStyles.tabularNumber.copyWith(
              color: color,
              fontWeight: FontWeight.w700,
              fontSize: 16,
            ),
          ),
        ],
      ),
    );
  }
}

// ── Catálogo ───────────────────────────────────────────────────────────────────────────────────────

class _CatalogTab extends StatefulWidget {
  const _CatalogTab();

  @override
  State<_CatalogTab> createState() => _CatalogTabState();
}

class _CatalogTabState extends State<_CatalogTab>
    with AutomaticKeepAliveClientMixin {
  static const _debounce = Duration(milliseconds: 350);
  final _search = TextEditingController();
  Timer? _timer;

  @override
  bool get wantKeepAlive => true;

  @override
  void dispose() {
    _timer?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearch(InventoryState state, String value) {
    _timer?.cancel();
    _timer = Timer(_debounce, () => state.setItemFilter(search: value));
  }

  Future<void> _edit(
    BuildContext context,
    InventoryState state,
    InventoryItem? item,
  ) async {
    final ok = await _showSheet<bool>(
      context,
      (_) => ChangeNotifierProvider.value(
        value: state,
        child: _ItemSheet(item: item),
      ),
    );
    if (ok == true && context.mounted) _snack(context, 'Ítem guardado.');
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final state = context.watch<InventoryState>();
    return RefreshIndicator(
      onRefresh: () => state.loadItems(silent: true),
      child: ListView(
        padding: _listPadding,
        children: [
          TextField(
            controller: _search,
            onChanged: (v) => _onSearch(state, v),
            decoration: InputDecoration(
              labelText: 'Buscar ítem',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _search.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Limpiar',
                      icon: const Icon(Icons.clear),
                      onPressed: () {
                        _search.clear();
                        _timer?.cancel();
                        state.setItemFilter(search: '');
                      },
                    ),
            ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              ListFilterDropdown(
                label: 'Categoría',
                value: state.itemCategory,
                options: _categoryOptions,
                allLabel: 'Todas',
                onChanged: (v) => state.setItemFilter(category: v),
              ),
              FilledButton.icon(
                onPressed: () => _edit(context, state, null),
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Nuevo ítem'),
              ),
            ],
          ),
          const SizedBox(height: 8),
          if (state.itemsLoading && state.items.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.itemsError != null && state.items.isEmpty)
            _ErrorText(state.itemsError!)
          else if (state.items.isEmpty)
            const _EmptyText('El catálogo está vacío.')
          else ...[
            for (final item in state.items)
              _ItemCard(item: item, onTap: () => _edit(context, state, item)),
            if (state.itemsHasMore)
              _LoadMoreButton(
                loading: state.itemsLoadingMore,
                onPressed: () => state.loadItems(more: true),
              ),
          ],
        ],
      ),
    );
  }
}

class _ItemCard extends StatelessWidget {
  const _ItemCard({required this.item, required this.onTap});
  final InventoryItem item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final unit = item.unit;
    return ClayCard(
      onTap: onTap,
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          ClayIconBadge(
            icon: _categoryIcon(item.category),
            color: item.isActive ? AppColors.signalBlue : AppColors.neutral,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${inventoryCategoryLabel(item.category)}'
                  '${unit != null && unit.isNotEmpty ? ' · $unit' : ''}'
                  ' · Mínimo: ${item.minimumStock}',
                  style: const TextStyle(
                    color: AppColors.inkSecondary,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          _Pill(
            item.isActive ? 'Activo' : 'Inactivo',
            item.isActive ? AppColors.signalBlue : AppColors.neutral,
          ),
          const Icon(Icons.edit_outlined, size: 18, color: AppColors.neutral),
        ],
      ),
    );
  }
}

// ── Ubicaciones ────────────────────────────────────────────────────────────────────────────────────

class _LocationsTab extends StatelessWidget {
  const _LocationsTab();

  @override
  Widget build(BuildContext context) {
    final state = context.watch<InventoryState>();
    final isAdmin = context.watch<AuthState>().hasRole(RoleNames.administrador);
    final main = state.mainLocation;
    return RefreshIndicator(
      onRefresh: () => state.loadLocations(),
      child: ListView(
        padding: _listPadding,
        children: [
          if (state.locationsError != null && state.locations.isEmpty)
            _ErrorText(state.locationsError!)
          else ...[
            if (main != null)
              _MainLocationCard(
                location: main,
                cities: state.cities,
                editable: isAdmin,
              )
            else
              const _EmptyText('Todavía no hay sede principal.'),
            const SizedBox(height: 12),
            const Text(
              'Inventario por zona',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
            ),
            const SizedBox(height: 4),
            const Text(
              'Cada zona tiene su inventario, creado con la zona. Se gestiona desde la sección Zonas.',
              style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
            const SizedBox(height: 8),
            if (state.zoneLocations.isEmpty)
              const _EmptyText('Todavía no hay zonas.')
            else
              for (final z in state.zoneLocations)
                ClayCard(
                  padding: const EdgeInsets.all(14),
                  child: Row(
                    children: [
                      const ClayIconBadge(
                        icon: Icons.map_outlined,
                        color: AppColors.signalBlue,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          z.name,
                          style: const TextStyle(
                            fontWeight: FontWeight.bold,
                            fontSize: 15,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
          ],
        ],
      ),
    );
  }
}

class _MainLocationCard extends StatefulWidget {
  const _MainLocationCard({
    required this.location,
    required this.cities,
    required this.editable,
  });

  final InventoryLocation location;
  final List<City> cities;
  final bool editable;

  @override
  State<_MainLocationCard> createState() => _MainLocationCardState();
}

class _MainLocationCardState extends State<_MainLocationCard> {
  late final TextEditingController _name;
  late final TextEditingController _address;
  String? _cityId;
  bool _dirty = false;
  int _pickerKey = 0;

  @override
  void initState() {
    super.initState();
    _name = TextEditingController(text: widget.location.name);
    _address = TextEditingController(text: widget.location.address ?? '');
    _cityId = widget.location.cityId;
  }

  @override
  void didUpdateWidget(covariant _MainLocationCard old) {
    super.didUpdateWidget(old);
    final a = old.location;
    final b = widget.location;
    final changed =
        a.name != b.name || a.address != b.address || a.cityId != b.cityId;
    // Un cambio hecho por otro usuario se refleja solo si no estoy editando.
    if (changed && !_dirty) _reset();
    if (old.cities.length != widget.cities.length && !_dirty) _pickerKey++;
  }

  void _reset() {
    _name.text = widget.location.name;
    _address.text = widget.location.address ?? '';
    _cityId = widget.location.cityId;
    _pickerKey++;
  }

  @override
  void dispose() {
    _name.dispose();
    _address.dispose();
    super.dispose();
  }

  MainLocationDraft get _draft => MainLocationDraft(
    name: _name.text,
    address: _address.text,
    cityId: _cityId,
  );

  Future<void> _save(InventoryState state) async {
    final error = await state.saveMainLocation(_draft);
    if (!mounted) return;
    if (error == null) {
      setState(() => _dirty = false);
      _snack(context, 'Sede principal actualizada.');
    } else {
      _snack(context, error);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.read<InventoryState>();
    final busy = context.select<InventoryState, bool>((s) => s.busy);
    final cities = state.cities;
    final currentCityName = widget.location.cityName;
    String? department;
    for (final c in cities) {
      if (c.id == _cityId) department = c.stateOrProvince;
    }
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Sede principal y bodega principal',
            style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
          ),
          const SizedBox(height: 12),
          if (!widget.editable) ...[
            _ReadOnlyLine('Nombre', widget.location.name),
            _ReadOnlyLine('Dirección', widget.location.address ?? '—'),
            _ReadOnlyLine('Municipio', currentCityName ?? '—'),
            const SizedBox(height: 8),
            const Text(
              'Solo el Administrador puede cambiar la sede principal.',
              style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
          ] else ...[
            TextField(
              controller: _name,
              maxLength: inventoryLocationNameMax,
              onChanged: (_) => setState(() => _dirty = true),
              decoration: const InputDecoration(labelText: 'Nombre *'),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _address,
              maxLength: inventoryLocationAddressMax,
              onChanged: (_) => setState(() => _dirty = true),
              decoration: const InputDecoration(labelText: 'Dirección'),
            ),
            const SizedBox(height: 8),
            if (cities.isEmpty)
              const Text(
                'Cargando municipios…',
                style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
              )
            else
              DepartmentCityPicker(
                key: ValueKey(_pickerKey),
                cities: cities,
                initialDepartmentName: department,
                initialCityId: _cityId,
                departmentLabel: 'Departamento',
                cityLabel: 'Municipio',
                onChanged: (dept, cityId) => setState(() {
                  _cityId = cityId;
                  _dirty = true;
                }),
              ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: busy || _draft.validate() != null
                  ? null
                  : () => _save(state),
              child: busy
                  ? const SizedBox(
                      height: 18,
                      width: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Text('Guardar'),
            ),
          ],
        ],
      ),
    );
  }
}

class _ReadOnlyLine extends StatelessWidget {
  const _ReadOnlyLine(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 90,
          child: Text(
            label,
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 13),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
          ),
        ),
      ],
    ),
  );
}

// ── Hojas de formulario ────────────────────────────────────────────────────────────────────────────

/// Marco común de las hojas: título, campos, error en línea y botones. [onSubmit] devuelve null si salió bien
/// (cierra la hoja con `true`) o el mensaje de error a mostrar sin cerrarla.
class _SheetFrame extends StatelessWidget {
  const _SheetFrame({
    required this.title,
    required this.children,
    required this.error,
    required this.submitting,
    required this.canSubmit,
    required this.onSubmit,
    this.submitLabel = 'Guardar',
  });

  final String title;
  final List<Widget> children;
  final String? error;
  final bool submitting;
  final bool canSubmit;
  final VoidCallback onSubmit;
  final String submitLabel;

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    return Padding(
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom: media.viewInsets.bottom + media.padding.bottom + 16,
      ),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            ...children,
            if (error != null) ...[
              const SizedBox(height: 8),
              Text(error!, style: const TextStyle(color: AppColors.signalRed)),
            ],
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: submitting
                        ? null
                        : () => Navigator.of(context).pop(),
                    child: const Text('Cancelar'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: canSubmit && !submitting ? onSubmit : null,
                    child: submitting
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : Text(submitLabel),
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

class _OperationSheet extends StatefulWidget {
  const _OperationSheet({required this.kind});
  final InventoryOperation kind;

  @override
  State<_OperationSheet> createState() => _OperationSheetState();
}

class _OperationSheetState extends State<_OperationSheet> {
  late OperationDraft _draft;
  late final TextEditingController _quantity;
  late final TextEditingController _notes = TextEditingController();
  String? _itemName;
  String? _error;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final state = context.read<InventoryState>();
    final initialLocation = widget.kind == InventoryOperation.entry
        ? state.mainLocation?.id ?? ''
        : state.stockLocationId ?? '';
    _draft = OperationDraft(kind: widget.kind, locationId: initialLocation);
    _quantity = TextEditingController(text: '1');
  }

  @override
  void dispose() {
    _quantity.dispose();
    _notes.dispose();
    super.dispose();
  }

  void _update(OperationDraft draft) => setState(() {
    _draft = draft;
    _error = null;
  });

  Future<void> _pickItem(InventoryState state) async {
    final picked = await _showSheet<InventoryItem>(
      context,
      (_) => _ItemPickerSheet(state: state),
    );
    if (picked == null) return;
    _itemName = picked.name;
    _update(_draft.copyWith(itemId: picked.id));
  }

  Future<void> _submit(InventoryState state) async {
    setState(() {
      _submitting = true;
      _error = null;
    });
    final error = await state.saveOperation(_draft);
    if (!mounted) return;
    if (error == null) {
      Navigator.of(context).pop(true);
    } else {
      setState(() {
        _submitting = false;
        _error = error;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.read<InventoryState>();
    final kind = widget.kind;
    final isTransfer = kind == InventoryOperation.transfer;
    final isAdjust = kind == InventoryOperation.adjust;
    final destinations = [
      for (final l in state.locations)
        if (l.id != _draft.locationId) l,
    ];
    return _SheetFrame(
      title: kind.title,
      error: _error,
      submitting: _submitting,
      canSubmit: _draft.isValid,
      submitLabel: 'Registrar',
      onSubmit: () => _submit(state),
      children: [
        InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: _submitting ? null : () => _pickItem(state),
          child: InputDecorator(
            decoration: const InputDecoration(
              labelText: 'Ítem *',
              suffixIcon: Icon(Icons.search),
            ),
            child: Text(
              _itemName ?? 'Busca un ítem',
              style: TextStyle(
                color: _itemName == null
                    ? AppColors.inkSecondary
                    : AppColors.inkPrimary,
              ),
            ),
          ),
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          initialValue: _draft.locationId.isEmpty ? null : _draft.locationId,
          isExpanded: true,
          decoration: InputDecoration(
            labelText: isTransfer ? 'Desde *' : 'Ubicación *',
          ),
          items: [
            for (final l in state.locations)
              DropdownMenuItem(
                value: l.id,
                child: Text(l.name, overflow: TextOverflow.ellipsis),
              ),
          ],
          onChanged: (v) => _update(
            _draft.copyWith(
              locationId: v ?? '',
              toLocationId: v == _draft.toLocationId ? '' : null,
            ),
          ),
        ),
        if (isTransfer) ...[
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            key: ValueKey('to-${_draft.locationId}-${_draft.toLocationId}'),
            initialValue: _draft.toLocationId.isEmpty
                ? null
                : _draft.toLocationId,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Hacia *'),
            items: [
              for (final l in destinations)
                DropdownMenuItem(
                  value: l.id,
                  child: Text(l.name, overflow: TextOverflow.ellipsis),
                ),
            ],
            onChanged: (v) => _update(_draft.copyWith(toLocationId: v ?? '')),
          ),
        ],
        const SizedBox(height: 12),
        TextField(
          controller: _quantity,
          keyboardType: TextInputType.numberWithOptions(signed: isAdjust),
          inputFormatters: [
            FilteringTextInputFormatter.allow(
              RegExp(isAdjust ? r'^-?\d{0,6}' : r'^\d{0,6}'),
            ),
          ],
          onChanged: (v) {
            final n = int.tryParse(v) ?? 0;
            _update(
              isAdjust
                  ? _draft.copyWith(delta: n)
                  : _draft.copyWith(quantity: n),
            );
          },
          decoration: InputDecoration(
            labelText: isAdjust ? 'Ajuste (+ suma, − resta) *' : 'Cantidad *',
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _notes,
          maxLength: inventoryNotesMax,
          maxLines: 2,
          onChanged: (v) => _update(_draft.copyWith(notes: v)),
          decoration: InputDecoration(
            labelText: isAdjust ? 'Motivo (obligatorio) *' : 'Notas (opcional)',
          ),
        ),
      ],
    );
  }
}

/// Selector de ítem con búsqueda (debounce) sobre los ítems activos. Devuelve el ítem elegido.
class _ItemPickerSheet extends StatefulWidget {
  const _ItemPickerSheet({required this.state});
  final InventoryState state;

  @override
  State<_ItemPickerSheet> createState() => _ItemPickerSheetState();
}

class _ItemPickerSheetState extends State<_ItemPickerSheet> {
  static const _debounce = Duration(milliseconds: 350);
  final _controller = TextEditingController();
  Timer? _timer;
  int _req = 0;
  bool _loading = true;
  String? _error;
  List<InventoryItem> _results = [];

  @override
  void initState() {
    super.initState();
    _search();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String _) {
    _timer?.cancel();
    _timer = Timer(_debounce, _search);
  }

  Future<void> _search() async {
    final id = ++_req;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await widget.state.searchActiveItems(_controller.text);
      if (!mounted || id != _req) return;
      setState(() {
        _results = items;
        _loading = false;
      });
    } catch (e, st) {
      debugPrint('_ItemPickerSheet._search failed: $e\n$st');
      if (!mounted || id != _req) return;
      setState(() {
        _error = e is ApiException ? e.message : 'No se pudo buscar.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    return Padding(
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom: media.viewInsets.bottom + media.padding.bottom + 16,
      ),
      child: SizedBox(
        height: media.size.height * 0.65,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Elegir ítem', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            TextField(
              controller: _controller,
              autofocus: true,
              onChanged: _onChanged,
              decoration: const InputDecoration(
                labelText: 'Buscar por nombre',
                prefixIcon: Icon(Icons.search),
              ),
            ),
            const SizedBox(height: 8),
            Expanded(child: _body()),
          ],
        ),
      ),
    );
  }

  Widget _body() {
    if (_loading && _results.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) return Center(child: _ErrorText(_error!));
    if (_results.isEmpty) {
      return const Center(
        child: Text(
          'Sin resultados.',
          style: TextStyle(color: AppColors.inkSecondary),
        ),
      );
    }
    return ListView(
      children: [
        for (final item in _results)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(item.name),
            subtitle: Text(
              inventoryCategoryLabel(item.category),
              style: const TextStyle(fontSize: 12),
            ),
            onTap: () => Navigator.of(context).pop(item),
          ),
      ],
    );
  }
}

class _ItemSheet extends StatefulWidget {
  const _ItemSheet({this.item});
  final InventoryItem? item;

  @override
  State<_ItemSheet> createState() => _ItemSheetState();
}

class _ItemSheetState extends State<_ItemSheet> {
  late ItemDraft _draft;
  late final TextEditingController _name;
  late final TextEditingController _unit;
  late final TextEditingController _minimum;
  String? _error;
  bool _submitting = false;

  bool get _editing => widget.item != null;

  @override
  void initState() {
    super.initState();
    final item = widget.item;
    _draft = item == null ? const ItemDraft() : ItemDraft.fromItem(item);
    _name = TextEditingController(text: _draft.name);
    _unit = TextEditingController(text: _draft.unit);
    _minimum = TextEditingController(text: '${_draft.minimumStock}');
  }

  @override
  void dispose() {
    _name.dispose();
    _unit.dispose();
    _minimum.dispose();
    super.dispose();
  }

  void _update(ItemDraft Function(ItemDraft) change) => setState(() {
    _draft = change(_draft);
    _error = null;
  });

  ItemDraft _with({
    String? name,
    String? category,
    String? unit,
    int? minimumStock,
    bool? isActive,
  }) => ItemDraft(
    name: name ?? _draft.name,
    category: category ?? _draft.category,
    unit: unit ?? _draft.unit,
    minimumStock: minimumStock ?? _draft.minimumStock,
    isActive: isActive ?? _draft.isActive,
  );

  Future<void> _submit(InventoryState state) async {
    setState(() {
      _submitting = true;
      _error = null;
    });
    final error = await state.saveItem(widget.item?.id, _draft);
    if (!mounted) return;
    if (error == null) {
      Navigator.of(context).pop(true);
    } else {
      setState(() {
        _submitting = false;
        _error = error;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.read<InventoryState>();
    return _SheetFrame(
      title: _editing ? 'Editar ítem' : 'Nuevo ítem',
      error: _error,
      submitting: _submitting,
      canSubmit: _draft.validate() == null,
      onSubmit: () => _submit(state),
      children: [
        TextField(
          controller: _name,
          maxLength: inventoryItemNameMax,
          onChanged: (v) => _update((_) => _with(name: v)),
          decoration: const InputDecoration(
            labelText: 'Nombre *',
            hintText: 'Ej: Fusor',
          ),
        ),
        const SizedBox(height: 4),
        const Text(
          'Categoría',
          style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
        ),
        const SizedBox(height: 6),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final e in inventoryCategories.entries)
              ClayChoiceChip(
                label: Text(e.value),
                selected: _draft.category == e.key,
                onSelected: (_) => _update((_) => _with(category: e.key)),
              ),
          ],
        ),
        const SizedBox(height: 12),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: TextField(
                controller: _unit,
                maxLength: inventoryUnitMax,
                onChanged: (v) => _update((_) => _with(unit: v)),
                decoration: const InputDecoration(
                  labelText: 'Unidad',
                  hintText: 'und',
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: TextField(
                controller: _minimum,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                onChanged: (v) =>
                    _update((_) => _with(minimumStock: int.tryParse(v) ?? 0)),
                decoration: const InputDecoration(
                  labelText: 'Stock mínimo',
                  helperText: '0 = sin alerta',
                ),
              ),
            ),
          ],
        ),
        if (_editing)
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Activo'),
            value: _draft.isActive,
            onChanged: (v) => _update((_) => _with(isActive: v)),
          ),
      ],
    );
  }
}
