import 'dart:async';

import 'package:flutter/material.dart';

import '../models/inventory.dart';
import '../services/api_client.dart';
import '../services/inventory_api.dart';
import '../theme/app_theme.dart';

/// Hoja con búsqueda (con debounce) sobre /inventory/parts para elegir un ítem. Devuelve la opción elegida.
/// Con [category] = 'Toner' la usa el formulario de tóner; sin categoría, el "Agregar repuesto" del check-out.
class PartPickerSheet extends StatefulWidget {
  const PartPickerSheet({
    super.key,
    required this.api,
    this.assetId,
    this.category,
    this.title = 'Agregar repuesto',
  });

  final InventoryApi api;
  final String? assetId;
  final String? category;
  final String title;

  static Future<PartOption?> show(
    BuildContext context, {
    required InventoryApi api,
    String? assetId,
    String? category,
    String title = 'Agregar repuesto',
  }) {
    return showModalBottomSheet<PartOption>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => PartPickerSheet(
        api: api,
        assetId: assetId,
        category: category,
        title: title,
      ),
    );
  }

  @override
  State<PartPickerSheet> createState() => _PartPickerSheetState();
}

class _PartPickerSheetState extends State<PartPickerSheet> {
  static const _debounce = Duration(milliseconds: 350);

  final _searchController = TextEditingController();
  Timer? _timer;
  int _requestId = 0;
  bool _loading = true;
  String? _error;
  List<PartOption> _results = [];
  bool _hasMore = false;

  @override
  void initState() {
    super.initState();
    _search();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onChanged(String _) {
    _timer?.cancel();
    _timer = Timer(_debounce, _search);
  }

  Future<void> _search() async {
    final id = ++_requestId;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await widget.api.searchParts(
        assetId: widget.assetId,
        search: _searchController.text,
        category: widget.category,
      );
      // Una respuesta vieja no pisa a la de una búsqueda posterior.
      if (!mounted || id != _requestId) return;
      setState(() {
        _results = page.items;
        _hasMore = page.hasMore;
        _loading = false;
      });
    } catch (e, st) {
      debugPrint('PartPickerSheet._search failed: $e\n$st');
      if (!mounted || id != _requestId) return;
      setState(() {
        _error = e is ApiException ? e.message : 'No se pudo buscar.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    return Padding(
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom: media.viewInsets.bottom + media.padding.bottom + 16,
      ),
      child: SizedBox(
        height: media.size.height * 0.65,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(widget.title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            TextField(
              controller: _searchController,
              autofocus: true,
              onChanged: _onChanged,
              decoration: const InputDecoration(
                labelText: 'Buscar por nombre',
                prefixIcon: Icon(Icons.search),
              ),
            ),
            const SizedBox(height: 8),
            Expanded(child: _buildBody()),
          ],
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading && _results.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Text(
          _error!,
          style: const TextStyle(color: AppColors.signalRed),
          textAlign: TextAlign.center,
        ),
      );
    }
    if (_results.isEmpty) {
      return const Center(
        child: Text(
          'Sin resultados.',
          style: TextStyle(color: AppColors.inkSecondary),
        ),
      );
    }
    return ListView(
      children: [
        for (final option in _results)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(option.name),
            subtitle: Text(
              option.category == 'Toner'
                  ? 'Tóner'
                  : option.category == 'ConsumibleBase'
                  ? 'Consumible base'
                  : option.category,
              style: const TextStyle(fontSize: 12),
            ),
            trailing: Text(
              'Stock: ${option.stock}',
              style: AppTextStyles.tabularNumber.copyWith(
                color: option.stock <= 0
                    ? AppColors.signalRed
                    : AppColors.inkSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
            onTap: () => Navigator.of(context).pop(option),
          ),
        if (_hasMore)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text(
              'Hay más resultados: afina la búsqueda.',
              style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
          ),
      ],
    );
  }
}
