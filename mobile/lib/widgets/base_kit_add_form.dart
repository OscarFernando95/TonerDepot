import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../models/base_kit.dart';
import '../services/api_client.dart';
import '../theme/app_theme.dart';
import 'quantity_stepper.dart';

/// Campo de unidad/grupo con sugerencias (los grupos ya usados en el kit) — equivalente al `el-autocomplete` de la
/// web. Las sugerencias aparecen como chips mientras el campo tiene el foco.
class BaseKitGroupField extends StatefulWidget {
  const BaseKitGroupField({
    super.key,
    this.controller,
    this.initialValue = '',
    required this.suggestions,
    required this.onChanged,
    this.enabled = true,
    this.label = 'Unidad / grupo',
  });

  /// Si se pasa, el padre es dueño del controller (y [initialValue] se ignora).
  final TextEditingController? controller;
  final String initialValue;
  final List<String> suggestions;
  final ValueChanged<String> onChanged;
  final bool enabled;
  final String label;

  @override
  State<BaseKitGroupField> createState() => _BaseKitGroupFieldState();
}

class _BaseKitGroupFieldState extends State<BaseKitGroupField> {
  late final TextEditingController _controller =
      widget.controller ?? TextEditingController(text: widget.initialValue);
  final _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _focus.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _focus.dispose();
    if (widget.controller == null) _controller.dispose();
    super.dispose();
  }

  void _pick(String value) {
    _controller.text = value;
    _controller.selection = TextSelection.collapsed(offset: value.length);
    widget.onChanged(value);
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final suggestions = _focus.hasFocus && widget.enabled
        ? BaseKitLogic.suggestGroups(widget.suggestions, _controller.text)
        : const <String>[];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          controller: _controller,
          focusNode: _focus,
          enabled: widget.enabled,
          inputFormatters: [
            LengthLimitingTextInputFormatter(baseKitMaxGroupLength),
          ],
          textCapitalization: TextCapitalization.sentences,
          onChanged: (v) {
            widget.onChanged(v);
            setState(() {});
          },
          decoration: InputDecoration(labelText: widget.label, isDense: true),
        ),
        if (suggestions.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Wrap(
              spacing: 6,
              runSpacing: 4,
              children: [
                for (final s in suggestions)
                  ActionChip(
                    label: Text(s, style: const TextStyle(fontSize: 12)),
                    visualDensity: VisualDensity.compact,
                    onPressed: () => _pick(s),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Hoja con búsqueda (con debounce) sobre GET /inventory/items para elegir un ítem del catálogo.
class BaseKitItemPickerSheet extends StatefulWidget {
  const BaseKitItemPickerSheet({super.key, required this.search});

  final Future<List<KitItemOption>> Function(String query) search;

  static Future<KitItemOption?> show(
    BuildContext context, {
    required Future<List<KitItemOption>> Function(String query) search,
  }) {
    return showModalBottomSheet<KitItemOption>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => BaseKitItemPickerSheet(search: search),
    );
  }

  @override
  State<BaseKitItemPickerSheet> createState() => _BaseKitItemPickerSheetState();
}

class _BaseKitItemPickerSheetState extends State<BaseKitItemPickerSheet> {
  final _controller = TextEditingController();
  Timer? _timer;
  int _requestId = 0;
  bool _loading = true;
  String? _error;
  List<KitItemOption> _results = [];

  @override
  void initState() {
    super.initState();
    _run();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String _) {
    _timer?.cancel();
    _timer = Timer(const Duration(milliseconds: 350), _run);
  }

  Future<void> _run() async {
    final id = ++_requestId;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await widget.search(_controller.text);
      if (!mounted || id != _requestId) return;
      setState(() => _results = results);
    } catch (e, st) {
      debugPrint('BaseKitItemPickerSheet search failed: $e\n$st');
      if (!mounted || id != _requestId) return;
      setState(() {
        _results = [];
        _error = e is ApiException
            ? e.message
            : 'No se pudo buscar en el catálogo.';
      });
    } finally {
      if (mounted && id == _requestId) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.of(context).size.height * 0.75;
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: SizedBox(
        height: height,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 18, 20, 8),
              child: Text(
                'Elegir ítem del catálogo',
                style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: TextField(
                controller: _controller,
                autofocus: true,
                onChanged: _onChanged,
                decoration: const InputDecoration(
                  hintText: 'Buscar por nombre',
                  prefixIcon: Icon(Icons.search),
                  isDense: true,
                ),
              ),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: _loading && _results.isEmpty
                  ? const Center(child: CircularProgressIndicator())
                  : _error != null
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Text(
                          _error!,
                          textAlign: TextAlign.center,
                          style: const TextStyle(color: AppColors.signalRed),
                        ),
                      ),
                    )
                  : _results.isEmpty
                  ? const Center(
                      child: Text(
                        'Sin resultados.',
                        style: TextStyle(color: AppColors.inkSecondary),
                      ),
                    )
                  : ListView.builder(
                      itemCount: _results.length,
                      itemBuilder: (context, index) {
                        final item = _results[index];
                        return ListTile(
                          title: Text(item.name),
                          subtitle: item.unit == null || item.unit!.isEmpty
                              ? null
                              : Text(item.unit!),
                          onTap: () => Navigator.of(context).pop(item),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Fila "agregar ítem": elegir del catálogo + unidad/grupo + cantidad. [onAdd] devuelve un mensaje de error (o null si
/// se agregó) que se muestra en un SnackBar.
class BaseKitAddForm extends StatefulWidget {
  const BaseKitAddForm({
    super.key,
    required this.search,
    required this.groups,
    required this.onAdd,
    this.itemHint = 'Agregar ítem del catálogo',
    this.addLabel = 'Agregar',
    this.enabled = true,
  });

  final Future<List<KitItemOption>> Function(String query) search;
  final List<String> groups;
  final String? Function(KitItemOption item, String group, int quantity) onAdd;
  final String itemHint;
  final String addLabel;
  final bool enabled;

  @override
  State<BaseKitAddForm> createState() => _BaseKitAddFormState();
}

class _BaseKitAddFormState extends State<BaseKitAddForm> {
  final _groupController = TextEditingController();
  KitItemOption? _item;
  int _quantity = 1;

  @override
  void dispose() {
    _groupController.dispose();
    super.dispose();
  }

  bool get _canAdd =>
      widget.enabled &&
      _item != null &&
      _groupController.text.trim().isNotEmpty;

  Future<void> _pickItem() async {
    final picked = await BaseKitItemPickerSheet.show(
      context,
      search: widget.search,
    );
    if (picked != null && mounted) setState(() => _item = picked);
  }

  void _add() {
    final item = _item;
    if (item == null) return;
    final error = widget.onAdd(item, _groupController.text, _quantity);
    if (error != null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error)));
      return;
    }
    setState(() {
      _item = null;
      _quantity = 1;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OutlinedButton.icon(
          onPressed: widget.enabled ? _pickItem : null,
          icon: const Icon(Icons.search, size: 18),
          label: Text(
            _item?.name ?? widget.itemHint,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        const SizedBox(height: 10),
        BaseKitGroupField(
          controller: _groupController,
          suggestions: widget.groups,
          enabled: widget.enabled,
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: 6),
        Row(
          children: [
            const Text(
              'Cantidad',
              style: TextStyle(color: AppColors.inkSecondary),
            ),
            const SizedBox(width: 8),
            QuantityStepper(
              value: _quantity,
              max: baseKitMaxQuantity,
              onChanged: (v) => setState(() => _quantity = v),
            ),
          ],
        ),
        const SizedBox(height: 6),
        FilledButton.icon(
          onPressed: _canAdd ? _add : null,
          icon: const Icon(Icons.add, size: 18),
          label: Text(widget.addLabel),
        ),
      ],
    );
  }
}
