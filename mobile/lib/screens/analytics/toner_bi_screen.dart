import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/toner_analytics.dart';
import '../../services/api_client.dart';
import '../../state/toner_analytics_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_date_field.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_segmented_control.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/list_filter_dropdown.dart';
import '../../widgets/toner_analytics_monthly_chart.dart';

/// BI de tóner (espejo de TonerBiView.vue), solo Administrador: cuánto tóner gasta cada máquina y cuánto dura. Sin
/// precios ni costos. La exportación a CSV es solo de la web.
class TonerBiScreen extends StatelessWidget {
  const TonerBiScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TonerAnalyticsState(ApiClient.instance)..init(),
      child: const _TonerBiBody(),
    );
  }
}

enum _BiTab {
  machines('Máquina', 'Por máquina'),
  clients('Cliente', 'Por cliente'),
  zones('Zona', 'Por zona'),
  models('Modelo', 'Por modelo');

  const _BiTab(this.shortLabel, this.title);
  final String shortLabel;
  final String title;
}

const _vsModelGreen = Color(0xFF2E7D32);

class _TonerBiBody extends StatefulWidget {
  const _TonerBiBody();

  @override
  State<_TonerBiBody> createState() => _TonerBiBodyState();
}

class _TonerBiBodyState extends State<_TonerBiBody> {
  _BiTab _tab = _BiTab.machines;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<TonerAnalyticsState>();
    final summary = state.summary;
    return RefreshIndicator(
      onRefresh: state.reload,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
        children: [
          const _Header(),
          const SizedBox(height: 14),
          _Filters(state: state),
          const SizedBox(height: 14),
          if (state.error != null && summary == null)
            _ErrorBanner(message: state.error!, onRetry: state.reload)
          else if (state.loading && summary == null)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (summary != null) ...[
            if (state.loading) const LinearProgressIndicator(),
            if (state.error != null) ...[
              _ErrorBanner(message: state.error!, onRetry: state.reload),
              const SizedBox(height: 12),
            ],
            _Kpis(summary: summary),
            const SizedBox(height: 14),
            _ChartPanel(summary: summary),
            const SizedBox(height: 14),
            ClaySegmentedControl<_BiTab>(
              segments: [
                for (final t in _BiTab.values)
                  ClaySegment(value: t, label: t.shortLabel),
              ],
              selected: _tab,
              onChanged: (t) => setState(() => _tab = t),
            ),
            const SizedBox(height: 12),
            Text(
              _tab.title,
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
            ),
            const SizedBox(height: 8),
            switch (_tab) {
              _BiTab.machines => _MachinesSection(state: state),
              _BiTab.clients => _GroupList(rows: summary.byClient),
              _BiTab.zones => _GroupList(rows: summary.byZone),
              _BiTab.models => _GroupList(rows: summary.byModel),
            },
          ],
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header();

  @override
  Widget build(BuildContext context) {
    return const ClaySurface(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClayIconBadge(
            icon: Icons.insights_outlined,
            color: AppColors.signalBlue,
          ),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'BI de tóner',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 17),
                ),
                SizedBox(height: 4),
                Text(
                  'Cuánto tóner gasta cada máquina y cuánto dura. La duración se estima con el contador: lo que '
                  'recorre entre una entrega y la siguiente del mismo tóner, dividido entre las unidades de la '
                  'entrega anterior.',
                  style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function({bool silent}) onRetry;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      borderColor: AppColors.signalRed,
      child: Row(
        children: [
          const Icon(Icons.error_outline, color: AppColors.signalRed),
          const SizedBox(width: 10),
          Expanded(child: Text(message)),
          TextButton(
            onPressed: () => onRetry(),
            child: const Text('Reintentar'),
          ),
        ],
      ),
    );
  }
}

class _Filters extends StatelessWidget {
  const _Filters({required this.state});

  final TonerAnalyticsState state;

  Future<void> _pickDate(BuildContext context, {required bool isFrom}) async {
    final filter = state.filter;
    final current = (isFrom ? filter.from : filter.to) ?? DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: current,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
      helpText: isFrom ? 'Desde' : 'Hasta',
    );
    if (picked == null) return;
    // El rango siempre queda ordenado: si se cruzan, el otro extremo se mueve al día elegido.
    var from = filter.from ?? picked;
    var to = filter.to ?? picked;
    if (isFrom) {
      from = picked;
      if (to.isBefore(from)) to = from;
    } else {
      to = picked;
      if (from.isAfter(to)) from = to;
    }
    await state.setRange(from, to);
  }

  @override
  Widget build(BuildContext context) {
    final filter = state.filter;
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: ClayDateField(
                  label: 'Desde',
                  value: TonerAnalytics.formatDate(filter.from),
                  onTap: () => _pickDate(context, isFrom: true),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: ClayDateField(
                  label: 'Hasta',
                  value: TonerAnalytics.formatDate(filter.to),
                  onTap: () => _pickDate(context, isFrom: false),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              ListFilterDropdown(
                key: ValueKey('toner-zone-${filter.zoneId}'),
                label: 'Zona',
                allLabel: 'Todas las zonas',
                value: filter.zoneId,
                options: [for (final z in state.zones) (z.id, z.name)],
                onChanged: state.setZone,
              ),
              ListFilterDropdown(
                key: ValueKey('toner-client-${filter.clientId}'),
                label: 'Cliente',
                allLabel: 'Todos los clientes',
                value: filter.clientId,
                options: [for (final c in state.clients) (c.id, c.name)],
                onChanged: state.setClient,
              ),
              ListFilterDropdown(
                key: ValueKey('toner-brand-${filter.brandId}'),
                label: 'Marca',
                allLabel: 'Toda marca',
                value: filter.brandId,
                options: [for (final b in state.brands) (b.id, b.name)],
                onChanged: state.setBrand,
              ),
              if (filter.brandId != null)
                ListFilterDropdown(
                  key: ValueKey(
                    'toner-model-${filter.brandId}-${filter.modelId}',
                  ),
                  label: 'Modelo',
                  allLabel: 'Todo modelo',
                  value: filter.modelId,
                  options: [for (final m in state.models) (m.id, m.name)],
                  onChanged: state.setModel,
                ),
            ],
          ),
          if (filter.hasDimensionFilters)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () => state.clearDimensionFilters(),
                icon: const Icon(Icons.filter_alt_off_outlined, size: 18),
                label: const Text('Quitar filtros'),
              ),
            ),
        ],
      ),
    );
  }
}

class _Kpis extends StatelessWidget {
  const _Kpis({required this.summary});

  final TonerSummary summary;

  @override
  Widget build(BuildContext context) {
    final tiles = [
      _KpiTile(
        label: 'Tóner usado',
        value: TonerAnalytics.formatNumber(summary.totalUnits),
        note: TonerAnalytics.splitNote(
          summary.changedByTechnicianUnits,
          summary.deliveredToUserUnits,
        ),
        icon: Icons.print_outlined,
      ),
      _KpiTile(
        label: 'Máquinas con consumo',
        value: TonerAnalytics.formatNumber(summary.machines),
        note: 'en el rango y filtros elegidos',
        icon: Icons.precision_manufacturing_outlined,
      ),
      _KpiTile(
        label: 'Páginas por tóner',
        value: TonerAnalytics.formatNumber(summary.avgPagesPerUnit),
        note: 'promedio de duración medida',
        icon: Icons.description_outlined,
      ),
      _KpiTile(
        label: 'Tóner con duración medida',
        value: TonerAnalytics.formatNumber(summary.measuredUnits),
        note: 'de ${TonerAnalytics.formatNumber(summary.totalUnits)} usados',
        icon: Icons.timelapse_outlined,
      ),
    ];
    return LayoutBuilder(
      builder: (context, constraints) {
        const gap = 12.0;
        final width = (constraints.maxWidth - gap) / 2;
        return Wrap(
          spacing: gap,
          runSpacing: gap,
          children: [for (final t in tiles) SizedBox(width: width, child: t)],
        );
      },
    );
  }
}

class _KpiTile extends StatelessWidget {
  const _KpiTile({
    required this.label,
    required this.value,
    required this.note,
    required this.icon,
  });

  final String label;
  final String value;
  final String note;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      label: '$label: $value. $note',
      child: ExcludeSemantics(
        child: ClaySurface(
          padding: const EdgeInsets.all(14),
          radius: 18,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  ClayIconBadge(
                    icon: icon,
                    color: AppColors.signalBlue,
                    size: 28,
                    iconSize: 14,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      label,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkSecondary,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                value,
                style: AppTextStyles.tabularNumber.copyWith(
                  fontSize: 26,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                note,
                style: const TextStyle(
                  fontSize: 11,
                  color: AppColors.inkSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ChartPanel extends StatelessWidget {
  const _ChartPanel({required this.summary});

  final TonerSummary summary;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Tóner usado por mes',
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          const SizedBox(height: 10),
          if (summary.monthly.isEmpty)
            const Text(
              'No hay tóner registrado en este rango.',
              style: TextStyle(color: AppColors.inkSecondary),
            )
          else
            TonerAnalyticsMonthlyChart(monthly: summary.monthly),
        ],
      ),
    );
  }
}

class _MachinesSection extends StatelessWidget {
  const _MachinesSection({required this.state});

  final TonerAnalyticsState state;

  @override
  Widget build(BuildContext context) {
    if (state.machines.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 24),
        child: Center(
          child: Text(
            'Sin consumo de tóner en este rango.',
            style: TextStyle(color: AppColors.inkSecondary),
          ),
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'Vs. modelo: 100 % = rinde igual que el promedio del mismo tóner en otras máquinas del modelo.',
          style: TextStyle(fontSize: 11, color: AppColors.inkSecondary),
        ),
        const SizedBox(height: 6),
        for (final row in state.machines) _MachineCard(row: row),
        const SizedBox(height: 8),
        _Pager(state: state),
      ],
    );
  }
}

class _MachineCard extends StatelessWidget {
  const _MachineCard({required this.row});

  final TonerMachineRow row;

  @override
  Widget build(BuildContext context) {
    final place = [
      if (row.locationName != null && row.locationName!.isNotEmpty)
        row.locationName!,
      if (row.zoneName != null && row.zoneName!.isNotEmpty) row.zoneName!,
    ].join(' · ');
    return ClayCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${row.brand} ${row.model}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                      ),
                    ),
                    Text(
                      row.serialNumber,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.inkSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              _VsModelChip(percent: row.vsModelPercent),
            ],
          ),
          const SizedBox(height: 6),
          Text(row.clientName ?? '—', style: const TextStyle(fontSize: 13)),
          if (place.isNotEmpty)
            Text(
              place,
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.inkSecondary,
              ),
            ),
          const SizedBox(height: 10),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _Metric(
                label: 'Tóner usado',
                value: '${row.totalUnits}',
                note:
                    '${row.changedByTechnicianUnits} téc. · ${row.deliveredToUserUnits} usuario',
              ),
              _Metric(
                label: 'Páginas por tóner',
                value: TonerAnalytics.formatNumber(row.avgPagesPerUnit),
                note: '${row.measuredUnits} medidos',
              ),
              _Metric(
                label: 'Páginas en el rango',
                value: TonerAnalytics.formatNumber(row.pagesInRange),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Último registro ${TonerAnalytics.formatDate(row.lastEventAt)} · '
            'contador ${TonerAnalytics.formatNumber(row.lastCounter)}',
            style: const TextStyle(fontSize: 11, color: AppColors.inkSecondary),
          ),
        ],
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value, this.note});

  final String label;
  final String value;
  final String? note;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 10, color: AppColors.inkSecondary),
          ),
          Text(
            value,
            style: AppTextStyles.tabularNumber.copyWith(
              fontSize: 17,
              fontWeight: FontWeight.w800,
            ),
          ),
          if (note != null)
            Text(
              note!,
              style: const TextStyle(
                fontSize: 10,
                color: AppColors.inkSecondary,
              ),
            ),
        ],
      ),
    );
  }
}

/// Chip "Vs. modelo": rojo < 85 %, verde > 115 %, neutro entre medio; "sin comparación" si no hay dato. Nunca solo
/// color: lleva ícono y texto.
class _VsModelChip extends StatelessWidget {
  const _VsModelChip({required this.percent});

  final double? percent;

  @override
  Widget build(BuildContext context) {
    final level = TonerAnalytics.classifyVsModel(percent);
    if (level == VsModelLevel.none) {
      return const Text(
        'sin comparación',
        style: TextStyle(fontSize: 11, color: AppColors.inkSecondary),
      );
    }
    final (color, icon, hint) = switch (level) {
      VsModelLevel.worse => (
        AppColors.signalRed,
        Icons.arrow_downward,
        'bajo el modelo',
      ),
      VsModelLevel.better => (
        _vsModelGreen,
        Icons.arrow_upward,
        'sobre el modelo',
      ),
      _ => (AppColors.inkSecondary, Icons.drag_handle, 'como el modelo'),
    };
    final text = TonerAnalytics.formatPercent(percent!);
    return Semantics(
      label: 'Versus modelo: $text, $hint',
      child: ExcludeSemantics(
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: color.withValues(alpha: 0.6)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 13, color: color),
              const SizedBox(width: 3),
              Text(
                text,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: color,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Pager extends StatelessWidget {
  const _Pager({required this.state});

  final TonerAnalyticsState state;

  @override
  Widget build(BuildContext context) {
    if (state.totalPages <= 1) return const SizedBox.shrink();
    final busy = state.loadingMachines;
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        IconButton(
          tooltip: 'Página anterior',
          onPressed: busy || state.page <= 1
              ? null
              : () => state.goToPage(state.page - 1),
          icon: const Icon(Icons.chevron_left),
        ),
        Text(
          'Página ${state.page} de ${state.totalPages}',
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        IconButton(
          tooltip: 'Página siguiente',
          onPressed: busy || state.page >= state.totalPages
              ? null
              : () => state.goToPage(state.page + 1),
          icon: const Icon(Icons.chevron_right),
        ),
      ],
    );
  }
}

class _GroupList extends StatelessWidget {
  const _GroupList({required this.rows});

  final List<TonerGroupRow> rows;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 24),
        child: Center(
          child: Text(
            'Sin datos en este rango.',
            style: TextStyle(color: AppColors.inkSecondary),
          ),
        ),
      );
    }
    return Column(
      children: [
        for (final row in rows)
          ClayCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  row.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    _Metric(label: 'Máquinas', value: '${row.machines}'),
                    _Metric(label: 'Tóner usado', value: '${row.totalUnits}'),
                    _Metric(
                      label: 'Páginas por tóner',
                      value: TonerAnalytics.formatNumber(row.avgPagesPerUnit),
                    ),
                  ],
                ),
              ],
            ),
          ),
      ],
    );
  }
}
