import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import 'clay_icon_badge.dart';

/// Bloque "Datos de la máquina": cliente, sede, ciudad, área y último contador.
/// Se usa igual en órdenes, tickets e instalaciones para que el técnico lea
/// siempre lo mismo en el mismo orden. Las filas sin dato se omiten; el
/// contador siempre se muestra (con "Sin lecturas" si nunca se registró uno).
class MachineInfoPanel extends StatelessWidget {
  const MachineInfoPanel({
    super.key,
    this.clientName,
    this.locationName,
    this.cityName,
    this.area,
    this.lastMeterReading,
    this.showCounter = true,
    this.compact = false,
  });

  final String? clientName;
  final String? locationName;
  final String? cityName;
  final String? area;
  final int? lastMeterReading;

  /// false para equipos sin catalogar, que no tienen lecturas.
  final bool showCounter;

  /// Versión para tarjetas de lista: menos aire entre filas.
  final bool compact;

  static String formatCounter(int? value) => value == null
      ? 'Sin lecturas'
      : value.toString().replaceAllMapped(
          RegExp(r'\B(?=(\d{3})+(?!\d))'),
          (_) => '.',
        );

  bool _has(String? v) => v != null && v.trim().isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[
      if (_has(clientName))
        _InfoLine(
          icon: Icons.apartment_outlined,
          label: 'Cliente',
          value: clientName!,
          bold: true,
        ),
      if (_has(locationName))
        _InfoLine(
          icon: Icons.storefront_outlined,
          label: 'Sede',
          value: locationName!,
        ),
      if (_has(cityName))
        _InfoLine(
          icon: Icons.location_city_outlined,
          label: 'Ciudad',
          value: cityName!,
        ),
      if (_has(area))
        _InfoLine(
          icon: Icons.meeting_room_outlined,
          label: 'Área',
          value: area!,
        ),
    ];

    if (rows.isEmpty && !showCounter) return const SizedBox.shrink();

    return Container(
      width: double.infinity,
      padding: EdgeInsets.all(compact ? 10 : 14),
      decoration: BoxDecoration(
        color: AppColors.signalBlue.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.signalBlue.withValues(alpha: 0.14)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (!compact) ...[
            const Row(
              children: [
                Icon(
                  Icons.print_outlined,
                  size: 15,
                  color: AppColors.signalBlue,
                ),
                SizedBox(width: 6),
                Text(
                  'DATOS DE LA MÁQUINA',
                  style: TextStyle(
                    color: AppColors.signalBlue,
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.6,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
          ],
          for (var i = 0; i < rows.length; i++) ...[
            if (i > 0) SizedBox(height: compact ? 6 : 8),
            rows[i],
          ],
          if (showCounter) ...[
            if (rows.isNotEmpty) SizedBox(height: compact ? 8 : 12),
            _CounterTile(value: lastMeterReading, compact: compact),
          ],
        ],
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  const _InfoLine({
    required this.icon,
    required this.label,
    required this.value,
    this.bold = false,
  });

  final IconData icon;
  final String label;
  final String value;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        ClayIconBadge(
          icon: icon,
          color: AppColors.signalBlue,
          size: 28,
          iconSize: 15,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  color: AppColors.inkSecondary,
                  fontSize: 11,
                ),
              ),
              Text(
                value,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: bold ? FontWeight.w700 : FontWeight.w500,
                  color: AppColors.inkPrimary,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _CounterTile extends StatelessWidget {
  const _CounterTile({required this.value, required this.compact});

  final int? value;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final hasValue = value != null;
    return Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(
        horizontal: 12,
        vertical: compact ? 8 : 10,
      ),
      decoration: BoxDecoration(
        color: AppColors.claySurfaceRaised,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.neutralSoft),
      ),
      child: Row(
        children: [
          Icon(
            Icons.speed_outlined,
            size: 18,
            color: hasValue ? AppColors.signalBlue : AppColors.neutral,
          ),
          const SizedBox(width: 8),
          const Expanded(
            child: Text(
              'Último contador',
              style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
          ),
          Text(
            MachineInfoPanel.formatCounter(value),
            style: AppTextStyles.tabularNumber.copyWith(
              fontSize: hasValue ? 17 : 13,
              fontWeight: FontWeight.w700,
              color: hasValue ? AppColors.inkPrimary : AppColors.neutral,
            ),
          ),
        ],
      ),
    );
  }
}
