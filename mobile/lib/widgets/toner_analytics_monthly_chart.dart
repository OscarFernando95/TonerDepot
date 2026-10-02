import 'package:flutter/material.dart';

import '../models/toner_analytics.dart';
import '../theme/app_theme.dart';

/// Barras de tóner usado por mes, dibujadas con contenedores proporcionales (sin paquetes de gráficos). Si no caben
/// en el ancho, se desplaza en horizontal. Expone una etiqueta semántica con todos los valores.
class TonerAnalyticsMonthlyChart extends StatelessWidget {
  const TonerAnalyticsMonthlyChart({super.key, required this.monthly});

  final List<TonerMonth> monthly;

  static const _plotHeight = 130.0;
  static const _minBarSlot = 46.0;

  @override
  Widget build(BuildContext context) {
    final maxValue = monthly.fold<int>(0, (m, e) => e.units > m ? e.units : m);
    return Semantics(
      container: true,
      label: TonerAnalytics.chartSemanticLabel(monthly),
      child: ExcludeSemantics(
        child: LayoutBuilder(
          builder: (context, constraints) {
            final slot = (constraints.maxWidth / monthly.length).clamp(
              _minBarSlot,
              double.infinity,
            );
            final content = Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                for (final m in monthly)
                  SizedBox(
                    width: slot,
                    child: _Bar(month: m, maxValue: maxValue),
                  ),
              ],
            );
            if (slot * monthly.length <= constraints.maxWidth + 0.5) {
              return content;
            }
            return SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: content,
            );
          },
        ),
      ),
    );
  }
}

class _Bar extends StatelessWidget {
  const _Bar({required this.month, required this.maxValue});

  final TonerMonth month;
  final int maxValue;

  @override
  Widget build(BuildContext context) {
    final fraction = TonerAnalytics.barFraction(month.units, maxValue);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          height: TonerAnalyticsMonthlyChart._plotHeight,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              Text(
                '${month.units}',
                style: AppTextStyles.tabularNumber.copyWith(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 3),
              AnimatedContainer(
                duration: const Duration(milliseconds: 250),
                curve: Curves.easeOut,
                width: 24,
                height:
                    (TonerAnalyticsMonthlyChart._plotHeight - 20) * fraction,
                decoration: BoxDecoration(
                  color: AppColors.signalBlue,
                  borderRadius: const BorderRadius.vertical(
                    top: Radius.circular(8),
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.signalBlue.withValues(alpha: 0.3),
                      blurRadius: 8,
                      offset: const Offset(0, 3),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        Container(height: 1, color: AppColors.neutralSoft),
        const SizedBox(height: 4),
        Text(
          TonerAnalytics.monthLabel(month.month),
          style: const TextStyle(fontSize: 11, color: AppColors.inkSecondary),
        ),
      ],
    );
  }
}
