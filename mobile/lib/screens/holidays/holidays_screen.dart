import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/role_names.dart';
import '../../models/technician_schedule.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/holidays_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_choice_chip.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

const _months = [
  'ene',
  'feb',
  'mar',
  'abr',
  'may',
  'jun',
  'jul',
  'ago',
  'sep',
  'oct',
  'nov',
  'dic',
];
const _weekdays = ['lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom'];

String _formatDate(String yyyyMmDd) {
  final d = DateTime.parse(yyyyMmDd);
  return '${_weekdays[d.weekday - 1]} ${d.day} ${_months[d.month - 1]}';
}

String _twoDigits(int n) => n.toString().padLeft(2, '0');
String _isoDate(DateTime d) => '${d.year}-${_twoDigits(d.month)}-${_twoDigits(d.day)}';

/// Cierre de la empresa (ámbar) > legal trabajado (azul) > legal normal
/// (neutral) — mismo orden de prioridad que el `tag` ya calculado más abajo.
IconData _holidayIcon(Holiday h) {
  if (h.source == 'custom') return Icons.event_busy_outlined;
  if (h.isWorkingDay) return Icons.event_available_outlined;
  return Icons.event_outlined;
}

Color _holidayColor(Holiday h) {
  if (h.source == 'custom') return AppColors.signalAmber;
  if (h.isWorkingDay) return AppColors.signalBlue;
  return AppColors.neutral;
}

/// Festivos de Colombia (calculados) y cierres de la empresa — espejo de
/// HolidaysView.vue. Agregar/quitar ajustes es RoleNames.Administrador
/// únicamente en el backend (ni Coordinador ni Técnico pueden, igual que en
/// la web); el resto de roles lo ve en modo solo lectura.
class HolidaysScreen extends StatelessWidget {
  const HolidaysScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final canEdit = context.read<AuthState>().hasRole(RoleNames.administrador);
    return ChangeNotifierProvider(
      create: (_) => HolidaysState(ApiClient.instance)..load(),
      child: Scaffold(
        backgroundColor: Colors.transparent,
        floatingActionButton: canEdit
            ? Builder(
                builder: (context) => FloatingActionButton.extended(
                  onPressed: () => _openAddClosureSheet(context),
                  icon: const Icon(Icons.add),
                  label: const Text('Día no laborable'),
                ),
              )
            : null,
        body: _HolidaysBody(canEdit: canEdit),
      ),
    );
  }

  static Future<void> _openAddClosureSheet(BuildContext context) async {
    final state = context.read<HolidaysState>();
    final nameController = TextEditingController();
    DateTime? selectedDate;

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
      ),
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
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Agregar día no laborable', style: Theme.of(sheetContext).textTheme.titleLarge),
                  const SizedBox(height: 16),
                  InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () async {
                      final now = DateTime.now();
                      final picked = await showDatePicker(
                        context: sheetContext,
                        initialDate: selectedDate ?? now,
                        firstDate: DateTime(now.year - 1),
                        lastDate: DateTime(now.year + 3),
                      );
                      if (picked != null) setSheetState(() => selectedDate = picked);
                    },
                    child: InputDecorator(
                      decoration: const InputDecoration(labelText: 'Fecha *'),
                      child: Text(
                        selectedDate == null ? 'Selecciona una fecha' : _isoDate(selectedDate!),
                        style: selectedDate == null ? const TextStyle(color: AppColors.inkSecondary) : null,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: nameController,
                    maxLength: 120,
                    onChanged: (_) => setSheetState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Nombre *',
                      hintText: 'Ej. Cierre de fin de año',
                    ),
                  ),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: FilledButton(
                      onPressed: selectedDate != null && nameController.text.trim().isNotEmpty
                          ? () => Navigator.of(sheetContext).pop(true)
                          : null,
                      child: const Text('Guardar', style: TextStyle(fontWeight: FontWeight.w700)),
                    ),
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    if (confirmed == true && selectedDate != null && context.mounted) {
      final error = await state.addClosure(_isoDate(selectedDate!), nameController.text.trim());
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? 'Día no laborable agregado.')),
      );
    }
  }
}

class _HolidaysBody extends StatelessWidget {
  const _HolidaysBody({required this.canEdit});

  final bool canEdit;

  Future<bool> _confirm(BuildContext context, String message) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Confirmar'),
        content: Text(message),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Confirmar')),
        ],
      ),
    );
    return confirmed ?? false;
  }

  Future<void> _markWorking(BuildContext context, HolidaysState state, Holiday h) async {
    final ok = await _confirm(context, '¿Tratar el ${_formatDate(h.date)} (${h.name}) como día laborable?');
    if (!ok || !context.mounted) return;
    final error = await state.markWorking(h);
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error ?? 'Ajuste guardado.')));
  }

  Future<void> _removeOverride(BuildContext context, HolidaysState state, Holiday h) async {
    final ok = await _confirm(context, '¿Quitar este ajuste?');
    if (!ok || !context.mounted) return;
    final error = await state.removeOverride(h);
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error ?? 'Ajuste eliminado.')));
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<HolidaysState>(
      builder: (context, state, _) {
        final thisYear = DateTime.now().year;
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final year in [
                      thisYear - 1,
                      thisYear,
                      thisYear + 1,
                      thisYear + 2,
                    ])
                      Padding(
                        padding: const EdgeInsets.only(right: 6),
                        child: ClayChoiceChip(
                          label: Text('$year'),
                          selected: state.year == year,
                          onSelected: (_) => state.load(year),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Expanded(
              child: Builder(
                builder: (context) {
                  if (state.loading && state.holidays.isEmpty) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (state.error != null && state.holidays.isEmpty) {
                    return Center(
                      child: Text(
                        state.error!,
                        style: const TextStyle(color: AppColors.signalRed),
                      ),
                    );
                  }
                  return RefreshIndicator(
                    onRefresh: state.load,
                    child: ListView.builder(
                      padding: EdgeInsets.fromLTRB(16, 4, 16, canEdit ? 96 : 16),
                      itemCount: state.holidays.length,
                      itemBuilder: (context, index) {
                        final h = state.holidays[index];
                        final tag = h.source == 'custom'
                            ? 'Cierre de la empresa'
                            : (h.isWorkingDay
                                  ? 'Legal — trabajado'
                                  : 'Legal');
                        final canRemove = h.source == 'custom' || h.isWorkingDay;
                        return ClayCard(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  ClayIconBadge(
                                    icon: _holidayIcon(h),
                                    color: _holidayColor(h),
                                  ),
                                  const SizedBox(width: 10),
                                  SizedBox(
                                    width: 92,
                                    child: Text(
                                      _formatDate(h.date),
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ),
                                  Expanded(child: Text(h.name)),
                                  Text(
                                    tag,
                                    style: const TextStyle(
                                      color: AppColors.inkSecondary,
                                      fontSize: 11,
                                    ),
                                  ),
                                ],
                              ),
                              if (canEdit) ...[
                                const SizedBox(height: 4),
                                Align(
                                  alignment: Alignment.centerRight,
                                  child: canRemove
                                      ? TextButton(
                                          onPressed: () => _removeOverride(context, state, h),
                                          style: TextButton.styleFrom(foregroundColor: AppColors.signalRed),
                                          child: const Text('Quitar ajuste'),
                                        )
                                      : TextButton(
                                          onPressed: () => _markWorking(context, state, h),
                                          child: const Text('Marcar laborable'),
                                        ),
                                ),
                              ],
                            ],
                          ),
                        );
                      },
                    ),
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}
