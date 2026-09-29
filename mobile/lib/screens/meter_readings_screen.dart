import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/meter_reading_asset.dart';
import '../state/meter_reading_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';

/// Dos pestañas: "Vinculados" (activos que el admin le asignó explícitamente
/// al técnico) y "Por cobertura" (respaldo — todos los activos instalados en
/// sus ciudades de cobertura, estén o no vinculados a él o a otro técnico;
/// ver AssetService.ListForMeterReadingByCoverageAsync, backend). Cubre el
/// caso de un técnico titular ausente (vacaciones, incapacidad, renuncia):
/// sin esta pestaña, sus activos quedarían sin nadie que les registre
/// lecturas hasta que un admin los revincule a mano.
class MeterReadingsScreen extends StatefulWidget {
  const MeterReadingsScreen({super.key});

  @override
  State<MeterReadingsScreen> createState() => _MeterReadingsScreenState();
}

class _MeterReadingsScreenState extends State<MeterReadingsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final state = context.read<MeterReadingState>();
      state.load();
      state.loadCoverage();
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
                          style: const TextStyle(color: AppColors.inkSecondary)
                              .merge(AppTextStyles.tabularNumber),
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
                        final value = double.tryParse(v ?? '');
                        if (value == null) return 'Ingresa un número válido.';
                        if (asset.lastMeterReading != null &&
                            value < asset.lastMeterReading!) {
                          return 'No puede ser menor al último registrado.';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 12),
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      shape: const RoundedRectangleBorder(
                        side: BorderSide(color: AppColors.neutralSoft),
                      ),
                      title: const Text('Fecha de lectura'),
                      subtitle: Text(
                        '${selectedDate.day}/${selectedDate.month}/${selectedDate.year}',
                      ),
                      trailing: const Icon(
                        Icons.calendar_today_outlined,
                        size: 18,
                      ),
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
            );
          },
        );
      },
    );

    if (confirmed == true && mounted) {
      final value = double.parse(counterController.text);
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

  Widget _buildList(MeterReadingState state, {required bool coverage}) {
    final loading = coverage ? state.loadingCoverage : state.loading;
    final error = coverage ? state.coverageError : state.error;
    final groups = coverage ? state.coverageGroupedByCity : state.groupedByCity;
    final onRefresh = coverage ? state.loadCoverage : state.load;
    final emptyMessage = coverage
        ? 'No hay equipos instalados en las ciudades de tu cobertura.'
        : 'No hay equipos vinculados para registrar contadores.';

    if (loading && groups.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (error != null && groups.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(error, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
        ),
      );
    }
    if (groups.isEmpty) {
      return Center(child: Text(emptyMessage, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.inkSecondary)));
    }
    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          for (final group in groups)
            Theme(
              data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
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
                      style: const TextStyle(letterSpacing: 0.6, fontWeight: FontWeight.w700, fontSize: 13),
                    ),
                  ),
                ),
                children: [
                  for (final asset in group.assets)
                    ClayCard(
                      padding: EdgeInsets.zero,
                      child: ListTile(
                        title: Text('${asset.assetBrandName} ${asset.model}'),
                        subtitle: Text(
                          '${asset.clientName ?? 'Sin cliente'}${asset.clientLocationName != null ? ' — ${asset.clientLocationName}' : ''}'
                          '${asset.area != null && asset.area!.isNotEmpty ? ' · ${asset.area}' : ''}\n'
                          'Serie: ${asset.serialNumber}'
                          '${asset.lastMeterReading != null ? ' · Último: ${asset.lastMeterReading!.toStringAsFixed(0)}' : ' · Sin lecturas'}',
                          style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12)
                              .merge(AppTextStyles.tabularNumber),
                        ),
                        isThreeLine: true,
                        trailing: FilledButton(
                          onPressed: () => _openRegisterSheet(asset),
                          child: const Text('Registrar'),
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
        return DefaultTabController(
          length: 2,
          child: Column(
            children: [
              Container(
                margin: const EdgeInsets.fromLTRB(12, 12, 12, 0),
                decoration: BoxDecoration(
                  color: AppColors.claySurface,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: TabBar(
                  labelColor: AppColors.inkPrimary,
                  unselectedLabelColor: AppColors.inkSecondary,
                  indicatorColor: AppColors.signalBlue,
                  indicatorSize: TabBarIndicatorSize.label,
                  tabs: const [
                    Tab(text: 'Vinculados'),
                    Tab(text: 'Por cobertura'),
                  ],
                ),
              ),
              Expanded(
                child: TabBarView(
                  children: [
                    _buildList(state, coverage: false),
                    _buildList(state, coverage: true),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
