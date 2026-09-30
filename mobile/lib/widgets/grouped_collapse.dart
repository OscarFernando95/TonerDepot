import 'package:flutter/material.dart';

import '../models/grouping.dart';
import '../theme/app_theme.dart';

/// Árbol colapsable de 3 niveles (ciudad → cliente → contrato → items) —
/// espejo del `el-collapse` anidado de AssetsListView.vue /
/// MaintenanceSchedulesView.vue. Todo colapsado por defecto, el usuario
/// expande a demanda el grupo que le interesa (mismo criterio que la web:
/// nunca renderizar de una vez cientos de filas).
class GroupedCollapseList<T> extends StatelessWidget {
  const GroupedCollapseList({
    super.key,
    required this.groups,
    required this.itemBuilder,
    this.emptyMessage = 'Sin resultados.',
    this.padding = const EdgeInsets.fromLTRB(16, 4, 16, 16),
  });

  final List<CityGroup<T>> groups;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final String emptyMessage;

  /// 16 abajo por defecto; pantallas con un FAB flotante encima de la lista
  /// (ej. "+ Activo") deben pasar más (96) para que no quede montado sobre
  /// el último grupo — mismo criterio que el resto de listas de la app.
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    if (groups.isEmpty) {
      return Padding(
        padding: const EdgeInsets.all(24),
        child: Text(
          emptyMessage,
          style: const TextStyle(color: AppColors.inkSecondary),
          textAlign: TextAlign.center,
        ),
      );
    }
    return ListView.builder(
      padding: padding,
      itemCount: groups.length,
      itemBuilder: (context, index) => _CityTile<T>(
        group: groups[index],
        itemBuilder: itemBuilder,
      ),
    );
  }
}

class _GroupHeader extends StatelessWidget {
  const _GroupHeader({required this.label, required this.count});

  final String label;
  final int count;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(
            label,
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
            overflow: TextOverflow.ellipsis,
          ),
        ),
        Container(
          margin: const EdgeInsets.only(left: 8),
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
          decoration: BoxDecoration(
            color: AppColors.canvasBg,
            borderRadius: BorderRadius.circular(999),
          ),
          child: Text(
            '$count',
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: AppColors.inkSecondary,
            ),
          ),
        ),
      ],
    );
  }
}

class _CityTile<T> extends StatelessWidget {
  const _CityTile({required this.group, required this.itemBuilder});

  final CityGroup<T> group;
  final Widget Function(BuildContext context, T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: AppColors.claySurfaceRaised,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: AppColors.inkPrimary.withValues(alpha: 0.12),
            offset: const Offset(0, 8),
            blurRadius: 22,
            spreadRadius: -10,
          ),
        ],
      ),
      clipBehavior: Clip.antiAlias,
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: 16),
          childrenPadding: EdgeInsets.zero,
          iconColor: AppColors.signalBlue,
          collapsedIconColor: AppColors.inkSecondary,
          title: _GroupHeader(label: group.city, count: group.count),
          children: [
            for (final clientGroup in group.clientGroups)
              _ClientTile<T>(group: clientGroup, itemBuilder: itemBuilder),
          ],
        ),
      ),
    );
  }
}

class _ClientTile<T> extends StatelessWidget {
  const _ClientTile({required this.group, required this.itemBuilder});

  final ClientGroup<T> group;
  final Widget Function(BuildContext context, T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(12, 0, 12, 10),
      decoration: BoxDecoration(
        color: AppColors.canvasBg,
        borderRadius: BorderRadius.circular(14),
      ),
      clipBehavior: Clip.antiAlias,
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: 12),
          childrenPadding: EdgeInsets.zero,
          iconColor: AppColors.signalBlue,
          collapsedIconColor: AppColors.inkSecondary,
          title: _GroupHeader(label: group.clientLabel, count: group.count),
          children: [
            for (final contractGroup in group.contractGroups)
              _ContractTile<T>(group: contractGroup, itemBuilder: itemBuilder),
          ],
        ),
      ),
    );
  }
}

class _ContractTile<T> extends StatelessWidget {
  const _ContractTile({required this.group, required this.itemBuilder});

  final ContractGroup<T> group;
  final Widget Function(BuildContext context, T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(10, 0, 10, 8),
      decoration: BoxDecoration(
        color: AppColors.claySurfaceRaised,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.neutralSoft),
      ),
      clipBehavior: Clip.antiAlias,
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: 12),
          childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
          iconColor: AppColors.signalBlue,
          collapsedIconColor: AppColors.inkSecondary,
          title: _GroupHeader(
            label: group.contractLabel,
            count: group.items.length,
          ),
          children: [
            for (final item in group.items) itemBuilder(context, item),
          ],
        ),
      ),
    );
  }
}
