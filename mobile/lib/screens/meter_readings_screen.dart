import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/meter_reading_asset.dart';
import '../models/role_names.dart';
import '../state/auth_state.dart';
import '../state/meter_reading_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_date_field.dart';
import '../widgets/clay_segmented_control.dart';
import '../widgets/clay_surface.dart';
import '../widgets/list_filter_dropdown.dart';
import '../widgets/toner_sheet.dart';

/// Técnico ve dos pestañas propias de la app, sin equivalente en la web:
/// "Vinculados" (activos que un admin le asignó explícitamente) y "Por
/// cobertura" (respaldo — todos los activos instalados en sus ciudades de
/// cobertura, estén o no vinculados a él o a otro técnico; ver
/// AssetService.ListForMeterReadingByCoverageAsync, backend — cubre el caso
/// de un técnico titular ausente). Administrador/Coordinador no hacen
/// soporte a los activos, así que ven exactamente lo mismo que
/// MeterReadingsView.vue: filtros de ciudad/cliente + agrupar por ciudad o
/// ver como lista, sin esas pestañas.
class MeterReadingsScreen extends StatefulWidget {
  const MeterReadingsScreen({super.key});

  @override
  State<MeterReadingsScreen> createState() => _MeterReadingsScreenState();
}

class _MeterReadingsScreenState extends State<MeterReadingsScreen> {
  String _tab = 'linked';
  bool _isStaff = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _isStaff = context.read<AuthState>().hasAnyRole(RoleNames.staffRoles);
      final state = context.read<MeterReadingState>();
      state.load();
      if (!_isStaff) state.loadCoverage();
      if (mounted) setState(() {});
    });
  }

  Future<void> _openRegisterSheet(MeterReadingAsset asset) async {
    final counterController = TextEditingController();
    DateTime selectedDate = DateTime.now();
    final formKey = GlobalKey<FormState>();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (sheetContext, setSheetState) {
            return Padding(
              padding: EdgeInsets.fromLTRB(
                20,
                20,
                20,
                MediaQuery.of(sheetContext).viewInsets.bottom +
                    MediaQuery.of(sheetContext).padding.bottom +
                    20,
              ),
              // SingleChildScrollView: al abrirse el teclado (autofocus) el
              // alto disponible baja y la Column sola se desbordaba.
              child: SingleChildScrollView(
                child: Form(
                  key: formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${asset.assetBrandName} ${asset.model}',
                        style: Theme.of(sheetContext).textTheme.titleMedium,
                      ),
                      Text(
                        'Serie: ${asset.serialNumber}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                      if (asset.lastMeterReading != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 4),
                          child: Text(
                            'Último contador registrado: ${asset.lastMeterReading!.toStringAsFixed(0)}',
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                            ).merge(AppTextStyles.tabularNumber),
                          ),
                        ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: counterController,
                        autofocus: true,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: false,
                        ),
                        style: AppTextStyles.tabularNumber,
                        decoration: const InputDecoration(
                          labelText: 'Nuevo valor del contador',
                        ),
                        validator: (v) {
                          final value = int.tryParse(v ?? '');
                          if (value == null) return 'Ingresa un número válido.';
                          if (asset.lastMeterReading != null &&
                              value < asset.lastMeterReading!) {
                            return 'No puede ser menor al último registrado.';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 12),
                      ClayDateField(
                        label: 'Fecha de lectura',
                        value:
                            '${selectedDate.day}/${selectedDate.month}/${selectedDate.year}',
                        onTap: () async {
                          final picked = await showDatePicker(
                            context: sheetContext,
                            initialDate: selectedDate,
                            firstDate: DateTime(2020),
                            lastDate: DateTime.now(),
                          );
                          if (picked != null) {
                            setSheetState(() => selectedDate = picked);
                          }
                        },
                      ),
                      const SizedBox(height: 20),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton(
                          onPressed: () {
                            if (formKey.currentState!.validate()) {
                              Navigator.of(sheetContext).pop(true);
                            }
                          },
                          child: const Text('Registrar'),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            );
          },
        );
      },
    );

    if (confirmed == true && mounted) {
      final value = int.parse(counterController.text);
      final error = await context.read<MeterReadingState>().register(
        asset,
        value,
        selectedDate,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(error ?? 'Contador registrado.')));
    }
  }

  /// Registro de tóner: solo sobre las máquinas vinculadas al técnico (el servidor rechaza las demás).
  Future<void> _openTonerSheet(MeterReadingAsset asset) async {
    final entry = await TonerSheet.show(
      context,
      api: context.read<MeterReadingState>().inventoryApi,
      asset: asset,
    );
    if (entry == null || !mounted) return;
    // El tóner recién registrado debe verse ya en la tarjeta (último tóner, unidades en 90 días).
    unawaited(context.read<MeterReadingState>().refreshSilently());
    final warning = entry.stockWarning;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          warning == null || warning.isEmpty
              ? 'Tóner registrado.'
              : 'Tóner registrado. $warning',
        ),
      ),
    );
  }

  Widget _buildList(MeterReadingState state, {required bool coverage}) {
    final loading = coverage ? state.loadingCoverage : state.loading;
    final error = coverage ? state.coverageError : state.error;
    final groups = coverage ? state.coverageGroupedByCity : state.groupedByCity;
    final onRefresh = coverage ? state.loadCoverage : state.load;
    final emptyMessage = coverage
        ? 'No hay equipos instalados en las ciudades de tu cobertura.'
        : 'No hay equipos vinculados para registrar contadores.';
    // Técnico sin máquinas vinculadas: explicación en vez de una lista en blanco.
    final emptyIsTechnicianLinked = !coverage && !_isStaff;

    if (loading && groups.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (error != null && groups.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            error,
            style: const TextStyle(color: AppColors.signalRed),
            textAlign: TextAlign.center,
          ),
        ),
      );
    }
    if (groups.isEmpty) {
      return RefreshIndicator(
        onRefresh: onRefresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(24),
          children: [
            const SizedBox(height: 48),
            if (emptyIsTechnicianLinked)
              const _NoLinkedMachines()
            else
              Text(
                emptyMessage,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.inkSecondary),
              ),
          ],
        ),
      );
    }
    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          for (final group in groups)
            Theme(
              data: Theme.of(context)
                  .copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                initiallyExpanded: groups.length == 1,
                tilePadding: const EdgeInsets.symmetric(horizontal: 4),
                title: Container(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  decoration: BoxDecoration(
                    color: AppColors.claySurface,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Center(
                    child: Text(
                      group.city.toUpperCase(),
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        letterSpacing: 0.6,
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ),
                children: [
                  for (final asset in group.assets)
                    _assetCard(
                      asset,
                      // El técnico registra tóner solo sobre SUS máquinas (el servidor rechaza las demás).
                      showToner: !coverage,
                      showLastToner: !coverage,
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  /// Tarjeta de un activo, con su botón "Registrar" — reutilizada por las
  /// pestañas de Técnico y por la vista de Staff.
  Widget _assetCard(
    MeterReadingAsset asset, {
    bool showToner = false,
    bool showLastToner = false,
  }) {
    return ClayCard(
      padding: EdgeInsets.zero,
      // Row en vez de ListTile(isThreeLine): el tile tiene alto
      // fijo y el subtítulo (que se parte en 2-3 líneas según
      // el ancho) lo desbordaba por décimas de píxel.
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${asset.assetBrandName} ${asset.model}',
                    style: Theme.of(context).textTheme.bodyLarge,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${asset.clientName ?? 'Sin cliente'}${asset.clientLocationName != null ? ' — ${asset.clientLocationName}' : ''}'
                    '${asset.area != null && asset.area!.isNotEmpty ? ' · ${asset.area}' : ''}\n'
                    'Serie: ${asset.serialNumber}'
                    '${asset.lastMeterReading != null ? ' · Último: ${asset.lastMeterReading!.toStringAsFixed(0)}' : ' · Sin lecturas'}',
                    style: const TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ).merge(AppTextStyles.tabularNumber),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FilledButton(
                  onPressed: () => _openRegisterSheet(asset),
                  child: const Text('Registrar'),
                ),
                if (showToner) ...[
                  const SizedBox(height: 6),
                  OutlinedButton(
                    onPressed: () => _openTonerSheet(asset),
                    child: const Text('Tóner'),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }

  /// Administrador/Coordinador — espejo tal cual de MeterReadingsView.vue:
  /// filtros de ciudad/cliente + agrupar por ciudad (2 niveles, ciudad →
  /// cliente) o ver como lista plana. Sin pestañas Vinculados/Por cobertura
  /// (esas son un concepto exclusivo del flujo de Técnico, sin equivalente
  /// en la web).
  Widget _buildStaffBody(MeterReadingState state) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              ListFilterDropdown(
                label: 'Ciudad',
                value: state.cityFilter,
                options: [for (final c in state.cityOptions) (c, c)],
                onChanged: state.setCityFilter,
              ),
              ListFilterDropdown(
                label: 'Cliente',
                value: state.clientFilter,
                options: state.clientOptions,
                onChanged: state.setClientFilter,
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
          child: ClaySegmentedControl<String>(
            selected: state.viewMode,
            onChanged: state.setViewMode,
            segments: const [
              ClaySegment(value: 'grouped', label: 'Agrupar por ciudad'),
              ClaySegment(value: 'flat', label: 'Ver como lista'),
            ],
          ),
        ),
        Expanded(child: _buildStaffList(state)),
      ],
    );
  }

  Widget _buildStaffList(MeterReadingState state) {
    if (state.loading && state.assets.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.error != null && state.assets.isEmpty) {
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
    final filtered = state.filtered;
    if (filtered.isEmpty) {
      return Center(
        child: Text(
          'No hay equipos que coincidan con los filtros.',
          textAlign: TextAlign.center,
          style: const TextStyle(color: AppColors.inkSecondary),
        ),
      );
    }
    if (state.viewMode == 'flat') {
      return RefreshIndicator(
        onRefresh: state.load,
        child: ListView(
          padding: const EdgeInsets.all(12),
          children: [for (final asset in filtered) _assetCard(asset)],
        ),
      );
    }
    final groups = state.groupedByCityAndClient;
    return RefreshIndicator(
      onRefresh: state.load,
      child: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          for (final cityGroup in groups)
            Theme(
              data: Theme.of(context)
                  .copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                tilePadding: const EdgeInsets.symmetric(horizontal: 4),
                title: Container(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  decoration: BoxDecoration(
                    color: AppColors.claySurface,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Center(
                    child: Text(
                      '${cityGroup.city.toUpperCase()} (${cityGroup.count})',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        letterSpacing: 0.6,
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ),
                children: [
                  for (final clientGroup in cityGroup.clientGroups)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(12, 0, 0, 8),
                      child: Theme(
                        data: Theme.of(context)
                            .copyWith(dividerColor: Colors.transparent),
                        child: ExpansionTile(
                          tilePadding: EdgeInsets.zero,
                          title: Text(
                            '${clientGroup.clientLabel} (${clientGroup.assets.length})',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          children: [
                            for (final asset in clientGroup.assets)
                              _assetCard(asset),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<MeterReadingState>(
      builder: (context, state, _) {
        if (_isStaff) return _buildStaffBody(state);
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
              child: ClaySegmentedControl<String>(
                selected: _tab,
                onChanged: (value) => setState(() => _tab = value),
                segments: const [
                  ClaySegment(value: 'linked', label: 'Mis máquinas'),
                  ClaySegment(value: 'coverage', label: 'Por cobertura'),
                ],
              ),
            ),
            Expanded(child: _buildList(state, coverage: _tab == 'coverage')),
          ],
        );
      },
    );
  }
}

/// Técnico sin ninguna máquina vinculada.
class _NoLinkedMachines extends StatelessWidget {
  const _NoLinkedMachines();

  @override
  Widget build(BuildContext context) {
    return const Column(
      children: [
        Icon(
          Icons.print_disabled_outlined,
          size: 40,
          color: AppColors.inkSecondary,
        ),
        SizedBox(height: 12),
        Text(
          'Todavía no tienes máquinas vinculadas.',
          textAlign: TextAlign.center,
          style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
        ),
        SizedBox(height: 8),
        Text(
          'Se vinculan solas cuando completas la instalación de un equipo; si necesitas otras, '
          'pídele a un administrador que te las vincule desde Técnicos → Activos.',
          textAlign: TextAlign.center,
          style: TextStyle(color: AppColors.inkSecondary),
        ),
      ],
    );
  }
}
