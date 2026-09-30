import 'package:flutter/material.dart';

/// Dropdown compacto para filas de filtros de listado (ciudad/cliente/
/// contrato) — reutilizado por ContractsListScreen, AssetsListScreen,
/// MaintenanceSchedulesListScreen y ClientsListScreen, espejo de los
/// `el-select clearable filterable` de esas mismas vistas en la web. Las
/// opciones se calculan siempre sobre la lista YA cargada en pantalla (igual
/// que la web: `[...new Set(items.map(...))]`), no sobre un catálogo aparte,
/// así nunca se ofrece un filtro que de todos modos daría lista vacía.
class ListFilterDropdown extends StatelessWidget {
  const ListFilterDropdown({
    super.key,
    required this.label,
    required this.value,
    required this.options,
    required this.onChanged,
    this.allLabel = 'Todos',
  });

  /// Etiqueta corta del filtro (ej. "Ciudad").
  final String label;

  /// Id seleccionado actualmente, o null para "sin filtro".
  final String? value;

  /// Pares (id, etiqueta) ya ordenados por quien construye el widget.
  final List<(String, String)> options;

  final ValueChanged<String?> onChanged;

  final String allLabel;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 168,
      child: DropdownButtonFormField<String?>(
        initialValue: value,
        isExpanded: true,
        isDense: true,
        decoration: InputDecoration(
          labelText: label,
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 12,
            vertical: 10,
          ),
        ),
        items: [
          DropdownMenuItem(value: null, child: Text(allLabel)),
          for (final option in options)
            DropdownMenuItem(
              value: option.$1,
              child: Text(option.$2, overflow: TextOverflow.ellipsis),
            ),
        ],
        onChanged: onChanged,
      ),
    );
  }
}
