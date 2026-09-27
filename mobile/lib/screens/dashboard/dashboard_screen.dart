import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/dashboard_summary.dart';
import '../../services/api_client.dart';
import '../../state/dashboard_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';

/// Espejo de DashboardView.vue para Administrador/Coordinador — recortado a
/// tarjetas + listas (nada de `el-table`/`el-progress`, no tienen sentido en
/// una pantalla angosta). DashboardState se instancia local a esta pantalla.
class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => DashboardState(ApiClient.instance)..load(),
      child: const _DashboardBody(),
    );
  }
}

class _DashboardBody extends StatelessWidget {
  const _DashboardBody();

  static const _periods = [7, 30, 90];

  String _formatHours(double? value) => value == null ? 'Sin datos' : '${value.toStringAsFixed(1)} h';
  String _formatPercentage(double? value) => value == null ? 'Sin datos' : '${value.toStringAsFixed(0)}%';

  @override
  Widget build(BuildContext context) {
    final state = context.watch<DashboardState>();
    final summary = state.summary;

    return RefreshIndicator(
      onRefresh: state.load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Text('Indicadores', style: Theme.of(context).textTheme.titleLarge),
              const Spacer(),
              for (final days in _periods)
                Padding(
                  padding: const EdgeInsets.only(left: 6),
                  child: ChoiceChip(
                    label: Text('$days d'),
                    selected: state.periodDays == days,
                    onSelected: (_) => state.setPeriod(days),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 16),
          if (state.loading && summary == null) const Center(child: Padding(padding: EdgeInsets.all(32), child: CircularProgressIndicator())),
          if (state.error != null && summary == null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
            ),
          if (summary != null) ..._buildContent(context, summary),
        ],
      ),
    );
  }

  List<Widget> _buildContent(BuildContext context, DashboardSummary summary) {
    final backlogTotal = summary.ticketsByCity.fold<int>(0, (sum, c) => sum + c.openCount + c.unassignedCount);

    return [
      GridView.count(
        crossAxisCount: 2,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        crossAxisSpacing: 12,
        mainAxisSpacing: 12,
        childAspectRatio: 1.5,
        children: [
          _KpiCard(label: 'MTTR', value: _formatHours(summary.mttr.averageResolutionHours), sub: '${summary.mttr.resolvedTicketCount} resueltos'),
          _KpiCard(label: 'Cumplimiento SLA', value: _formatPercentage(summary.slaCompliance.overallCompliancePercentage), sub: 'Del período'),
          _KpiCard(
            label: 'Mantenimientos a tiempo',
            value: _formatPercentage(summary.maintenanceCompliance.onTimePercentage),
            sub: '${summary.maintenanceCompliance.onTimeCount} / ${summary.maintenanceCompliance.completedCount}',
          ),
          _KpiCard(label: 'Backlog', value: '$backlogTotal', sub: '${summary.ticketsByCity.length} ciudad(es)'),
        ],
      ),
      const SizedBox(height: 20),
      Text('Tickets abiertos / sin asignar por ciudad', style: Theme.of(context).textTheme.titleSmall),
      const SizedBox(height: 8),
      if (summary.ticketsByCity.isEmpty)
        const _EmptyRow(message: 'Sin tickets pendientes.')
      else
        ClaySurface(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              for (final city in summary.ticketsByCity)
                _DataRow(
                  title: city.cityName,
                  dotColor: city.unassignedCount > 0
                      ? AppColors.signalRed
                      : city.openCount > 0
                          ? AppColors.signalAmber
                          : AppColors.signalBlue,
                  trailing: '${city.openCount} abiertos · ${city.unassignedCount} sin asignar',
                ),
            ],
          ),
        ),
      const SizedBox(height: 20),
      Text('Utilización de técnicos (base ${summary.periodDays} días x 8h/día)', style: Theme.of(context).textTheme.titleSmall),
      const SizedBox(height: 8),
      if (summary.technicianUtilization.isEmpty)
        const _EmptyRow(message: 'Sin registros de tiempo.')
      else
        ClaySurface(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              for (final tech in summary.technicianUtilization)
                _DataRow(
                  title: tech.technicianName,
                  dotColor: tech.utilizationPercentage >= 90
                      ? AppColors.signalRed
                      : tech.utilizationPercentage >= 70
                          ? AppColors.signalAmber
                          : AppColors.signalBlue,
                  trailing: '${tech.hoursLogged.toStringAsFixed(1)} h · ${tech.utilizationPercentage.toStringAsFixed(0)}%',
                ),
            ],
          ),
        ),
      const SizedBox(height: 20),
      Text('Cumplimiento de SLA por prioridad', style: Theme.of(context).textTheme.titleSmall),
      const SizedBox(height: 8),
      ClaySurface(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final p in summary.slaCompliance.byPriority) _SlaPriorityRow(item: p),
          ],
        ),
      ),
      const SizedBox(height: 16),
    ];
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({required this.label, required this.value, required this.sub});

  final String label;
  final String value;
  final String sub;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(label.toUpperCase(), style: const TextStyle(color: AppColors.inkSecondary, fontSize: 11, letterSpacing: 0.5)),
          const SizedBox(height: 6),
          Text(
            value,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold).merge(AppTextStyles.tabularNumber),
          ),
          const SizedBox(height: 2),
          Text(sub, style: const TextStyle(color: AppColors.inkSecondary, fontSize: 11)),
        ],
      ),
    );
  }
}

class _DataRow extends StatelessWidget {
  const _DataRow({required this.title, required this.dotColor, required this.trailing});

  final String title;
  final Color dotColor;
  final String trailing;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Row(
        children: [
          Container(width: 10, height: 10, decoration: BoxDecoration(color: dotColor, shape: BoxShape.circle)),
          const SizedBox(width: 12),
          Expanded(child: Text(title, style: const TextStyle(fontWeight: FontWeight.w600))),
          Text(trailing, style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12).merge(AppTextStyles.tabularNumber)),
        ],
      ),
    );
  }
}

class _SlaPriorityRow extends StatelessWidget {
  const _SlaPriorityRow({required this.item});

  final SlaPriorityCompliance item;

  Color get _color {
    final pct = item.compliancePercentage;
    if (pct == null) return AppColors.neutral;
    if (pct >= 90) return AppColors.signalBlue;
    if (pct >= 70) return AppColors.signalAmber;
    return AppColors.signalRed;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(item.priority, style: const TextStyle(fontWeight: FontWeight.w600))),
              Text('Meta ${item.targetHours}h', style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12)),
              const SizedBox(width: 12),
              Text(
                item.compliancePercentage == null ? 'Sin datos' : '${item.compliancePercentage!.toStringAsFixed(0)}%',
                style: TextStyle(color: _color, fontWeight: FontWeight.bold),
              ),
            ],
          ),
          const SizedBox(height: 6),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: item.compliancePercentage == null ? 0 : (item.compliancePercentage! / 100).clamp(0, 1),
              minHeight: 8,
              backgroundColor: AppColors.neutralSoft,
              color: _color,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            '${item.withinSlaCount} / ${item.resolvedCount} dentro de SLA',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 11),
          ),
        ],
      ),
    );
  }
}

class _EmptyRow extends StatelessWidget {
  const _EmptyRow({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Text(message, style: const TextStyle(color: AppColors.inkSecondary)),
    );
  }
}
