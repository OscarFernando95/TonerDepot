import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/meter_reading_asset.dart';
import '../state/meter_reading_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';

class MeterReadingsScreen extends StatefulWidget {
  const MeterReadingsScreen({super.key});

  @override
  State<MeterReadingsScreen> createState() => _MeterReadingsScreenState();
}

class _MeterReadingsScreenState extends State<MeterReadingsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<MeterReadingState>().load());
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
                MediaQuery.of(sheetContext).viewInsets.bottom + MediaQuery.of(sheetContext).padding.bottom + 20,
              ),
              child: Form(
                key: formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${asset.assetBrandName} ${asset.model}', style: Theme.of(sheetContext).textTheme.titleMedium),
                    Text('Serie: ${asset.serialNumber}', style: const TextStyle(color: AppColors.inkSecondary)),
                    if (asset.lastMeterReading != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          'Último contador registrado: ${asset.lastMeterReading!.toStringAsFixed(0)}',
                          style: const TextStyle(color: AppColors.inkSecondary).merge(AppTextStyles.tabularNumber),
                        ),
                      ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: counterController,
                      autofocus: true,
                      keyboardType: const TextInputType.numberWithOptions(decimal: false),
                      style: AppTextStyles.tabularNumber,
                      decoration: const InputDecoration(labelText: 'Nuevo valor del contador'),
                      validator: (v) {
                        final value = double.tryParse(v ?? '');
                        if (value == null) return 'Ingresa un número válido.';
                        if (asset.lastMeterReading != null && value < asset.lastMeterReading!) {
                          return 'No puede ser menor al último registrado.';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 12),
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      shape: const RoundedRectangleBorder(side: BorderSide(color: AppColors.neutralSoft)),
                      title: const Text('Fecha de lectura'),
                      subtitle: Text('${selectedDate.day}/${selectedDate.month}/${selectedDate.year}'),
                      trailing: const Icon(Icons.calendar_today_outlined, size: 18),
                      onTap: () async {
                        final picked = await showDatePicker(
                          context: sheetContext,
                          initialDate: selectedDate,
                          firstDate: DateTime(2020),
                          lastDate: DateTime.now(),
                        );
                        if (picked != null) setSheetState(() => selectedDate = picked);
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
      final error = await context.read<MeterReadingState>().register(asset, value, selectedDate);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? 'Contador registrado.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<MeterReadingState>(
      builder: (context, state, _) {
        if (state.loading && state.assets.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.assets.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
            ),
          );
        }
        final groups = state.groupedByCity;
        if (groups.isEmpty) {
          return const Center(child: Text('No hay equipos instalados para registrar contadores.'));
        }
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView(
            padding: const EdgeInsets.all(12),
            children: [
              for (final group in groups)
                Theme(
                  data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
                  child: ExpansionTile(
                    initiallyExpanded: groups.length == 1,
                    title: Text(
                      group.city.toUpperCase(),
                      style: const TextStyle(letterSpacing: 0.6, fontWeight: FontWeight.w600, fontSize: 13),
                    ),
                    children: [
                      for (final asset in group.assets)
                        ClayCard(
                          padding: EdgeInsets.zero,
                          child: ListTile(
                            title: Text('${asset.assetBrandName} ${asset.model}'),
                            subtitle: Text(
                              '${asset.clientName ?? 'Sin cliente'}${asset.clientLocationName != null ? ' — ${asset.clientLocationName}' : ''}\n'
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
      },
    );
  }
}
