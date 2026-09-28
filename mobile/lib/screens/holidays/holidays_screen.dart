import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../services/api_client.dart';
import '../../state/holidays_state.dart';
import '../../theme/app_theme.dart';
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

/// Festivos de Colombia (calculados) y cierres de la empresa. Solo lectura: agregar/quitar ajustes se hace en la web.
class HolidaysScreen extends StatelessWidget {
  const HolidaysScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => HolidaysState(ApiClient.instance)..load(),
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Consumer<HolidaysState>(
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
                            child: ChoiceChip(
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
                          padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                          itemCount: state.holidays.length,
                          itemBuilder: (context, index) {
                            final h = state.holidays[index];
                            final tag = h.source == 'custom'
                                ? 'Cierre de la empresa'
                                : (h.isWorkingDay
                                      ? 'Legal — trabajado'
                                      : 'Legal');
                            return ClayCard(
                              child: Row(
                                children: [
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
        ),
      ),
    );
  }
}
