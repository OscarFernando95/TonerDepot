import 'package:flutter/material.dart';

import '../models/city.dart';

/// Cascada Departamento→Ciudad con autocompletar (busca escribiendo, no solo
/// desplegando una lista) — mismo patrón que ya usaba
/// `technician_coverage_screen.dart` con `Autocomplete` nativo de Flutter
/// (sin dependencias nuevas), ahora compartido para no repetirlo en cada
/// formulario que pide una ciudad (crear/editar cliente, crear/editar
/// usuario). Espejo de `el-select filterable` + cascada en
/// CreateClientDialog.vue/UsersView.vue/ClientDetailView.vue.
class DepartmentCityPicker extends StatefulWidget {
  const DepartmentCityPicker({
    super.key,
    required this.cities,
    this.initialDepartmentName,
    this.initialCityId,
    required this.onChanged,
    this.departmentLabel = 'Departamento *',
    this.cityLabel = 'Ciudad *',
  });

  final List<City> cities;
  final String? initialDepartmentName;
  final String? initialCityId;

  /// Se llama cada vez que cambia el departamento (con `cityId: null`, hay
  /// que volver a elegir ciudad) o la ciudad.
  final void Function(String? departmentName, String? cityId) onChanged;
  final String departmentLabel;
  final String cityLabel;

  @override
  State<DepartmentCityPicker> createState() => _DepartmentCityPickerState();
}

class _DepartmentCityPickerState extends State<DepartmentCityPicker> {
  late String? _departmentName = widget.initialDepartmentName;

  String? get _initialCityName {
    if (widget.initialCityId == null) return null;
    for (final c in widget.cities) {
      if (c.id == widget.initialCityId) return c.name;
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final departments = ({for (final c in widget.cities) c.stateOrProvince}.toList())..sort();
    final citiesInDepartment = widget.cities.where((c) => c.stateOrProvince == _departmentName).toList()
      ..sort((a, b) => a.name.compareTo(b.name));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Autocomplete<String>(
          initialValue: TextEditingValue(text: _departmentName ?? ''),
          optionsBuilder: (textEditingValue) {
            final q = textEditingValue.text.trim().toLowerCase();
            if (q.isEmpty) return departments;
            return departments.where((d) => d.toLowerCase().contains(q));
          },
          onSelected: (value) {
            setState(() => _departmentName = value);
            widget.onChanged(_departmentName, null);
          },
          fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
            return TextField(
              controller: controller,
              focusNode: focusNode,
              decoration: InputDecoration(
                labelText: widget.departmentLabel,
                prefixIcon: const Icon(Icons.search),
              ),
            );
          },
        ),
        const SizedBox(height: 12),
        // key: fuerza a Flutter a recrear el widget (y su controlador de
        // texto interno) cuando cambia el departamento — si no, el texto ya
        // escrito de un departamento anterior se quedaría pegado.
        Autocomplete<City>(
          key: ValueKey(_departmentName),
          displayStringForOption: (city) => city.name,
          initialValue: TextEditingValue(text: _initialCityName ?? ''),
          optionsBuilder: (textEditingValue) {
            if (_departmentName == null) return const Iterable<City>.empty();
            final q = textEditingValue.text.trim().toLowerCase();
            if (q.isEmpty) return citiesInDepartment;
            return citiesInDepartment.where((c) => c.name.toLowerCase().contains(q));
          },
          onSelected: (city) => widget.onChanged(_departmentName, city.id),
          fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
            return TextField(
              controller: controller,
              focusNode: focusNode,
              enabled: _departmentName != null,
              decoration: InputDecoration(
                labelText: widget.cityLabel,
                prefixIcon: const Icon(Icons.search),
              ),
            );
          },
        ),
      ],
    );
  }
}
