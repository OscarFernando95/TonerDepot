import 'package:flutter/material.dart';

import '../../models/asset_brand.dart';
import '../../models/asset_model.dart';
import '../../services/api_client.dart';
import '../../services/asset_api.dart';
import '../../services/asset_brand_api.dart';
import '../../services/asset_model_api.dart';
import '../../theme/app_theme.dart';

/// Se crea referenciando un AssetModel ya existente (no hay marca/modelo
/// libres) — de ahí el selector en cascada marca → modelo. Arranca en
/// EnBodega siempre.
///
/// Espejo de AssetsListView.vue (diálogo "Nuevo activo" con los botones `+`
/// "Nueva marca"/"Nuevo modelo" junto a esos selectores) — reusa los mismos
/// diálogos inline que ya existen en asset_brands_list_screen.dart
/// (`_showAddBrandDialog`) y asset_brand_detail_screen.dart
/// (`_showModelDialog`), en vez de navegar a otra pantalla: no hay una ruta
/// que devuelva la marca/modelo creado, y ambas pantallas de origen dependen
/// de un ChangeNotifierProvider (AssetBrandsState/AssetBrandDetailState) que
/// no tiene sentido montar solo para este atajo.
class AssetCreateScreen extends StatefulWidget {
  const AssetCreateScreen({super.key});

  @override
  State<AssetCreateScreen> createState() => _AssetCreateScreenState();
}

class _AssetCreateScreenState extends State<AssetCreateScreen> {
  static const _types = ['Impresora', 'ComputoEquipo'];

  final _serialController = TextEditingController();
  bool _loadingBrands = true;
  bool _loadingModels = false;
  bool _submitting = false;
  String? _loadError;
  List<AssetBrand> _brands = [];
  List<AssetModel> _models = [];
  String? _selectedBrandId;
  String? _selectedModelId;
  String _type = 'Impresora';

  @override
  void initState() {
    super.initState();
    _loadBrands();
  }

  @override
  void dispose() {
    _serialController.dispose();
    super.dispose();
  }

  Future<void> _loadBrands() async {
    try {
      final brands = await AssetBrandApi(ApiClient.instance).list();
      setState(() {
        _brands = brands;
        _loadingBrands = false;
      });
    } catch (e) {
      setState(() {
        _loadingBrands = false;
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las marcas.';
      });
    }
  }

  Future<void> _onBrandChanged(String? brandId) async {
    setState(() {
      _selectedBrandId = brandId;
      _selectedModelId = null;
      _models = [];
      _loadingModels = brandId != null;
    });
    if (brandId == null) return;
    try {
      final models = await AssetModelApi(ApiClient.instance)
          .listForBrand(brandId);
      setState(() {
        _models = models;
        _loadingModels = false;
      });
    } catch (e) {
      setState(() => _loadingModels = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException
                  ? e.message
                  : 'No se pudieron cargar los modelos.',
            ),
          ),
        );
      }
    }
  }

  /// Espejo de openQuickBrandDialog/saveQuickBrand en AssetsListView.vue —
  /// mismo diálogo inline (solo nombre) que ya usa
  /// asset_brands_list_screen.dart, con try/catch propio porque acá no hay
  /// un AssetBrandsState detrás.
  Future<void> _createBrand() async {
    final controller = TextEditingController();
    final name = await showDialog<String>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Nueva marca'),
          content: TextField(
            controller: controller,
            onChanged: (_) => setDialogState(() {}),
            decoration: const InputDecoration(labelText: 'Nombre *'),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: controller.text.trim().isEmpty
                  ? null
                  : () =>
                        Navigator.of(dialogContext).pop(controller.text.trim()),
              child: const Text('Crear'),
            ),
          ],
        ),
      ),
    );
    if (name == null || !mounted) return;
    try {
      final brand = await AssetBrandApi(ApiClient.instance).create(name);
      setState(() {
        _brands = [..._brands, brand];
        _selectedBrandId = brand.id;
        _selectedModelId = null;
        _models = [];
      });
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear la marca.',
            ),
          ),
        );
      }
    }
  }

  /// Espejo de openQuickModelDialog/saveQuickModel en AssetsListView.vue —
  /// mismo diálogo inline (nombre + 5 umbrales/intervalos) que ya usa
  /// asset_brand_detail_screen.dart, con try/catch propio porque acá no hay
  /// un AssetBrandDetailState detrás.
  Future<void> _createModel() async {
    if (_selectedBrandId == null) return;
    final brandId = _selectedBrandId!;
    final nameController = TextEditingController();
    final generalPrintController = TextEditingController();
    final generalMonthsController = TextEditingController();
    final unitsPrintController = TextEditingController();
    final unitsMonthsController = TextEditingController();
    final consumablesController = TextEditingController();

    bool isValid() =>
        nameController.text.trim().isNotEmpty &&
        int.tryParse(generalPrintController.text.trim()) != null &&
        int.tryParse(generalMonthsController.text.trim()) != null &&
        int.tryParse(unitsPrintController.text.trim()) != null &&
        int.tryParse(unitsMonthsController.text.trim()) != null &&
        int.tryParse(consumablesController.text.trim()) != null;

    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Nuevo modelo'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: nameController,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(labelText: 'Nombre *'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: generalPrintController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral mantenimiento general (impresiones) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: generalMonthsController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Intervalo mantenimiento general (meses) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: unitsPrintController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral unidades (impresiones) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: unitsMonthsController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Intervalo unidades (meses) *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: consumablesController,
                    keyboardType: TextInputType.number,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Umbral insumos (impresiones) *',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: isValid()
                  ? () => Navigator.of(dialogContext).pop(true)
                  : null,
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );
    if (result != true || !mounted) return;
    try {
      final model = await AssetModelApi(ApiClient.instance).create(
        brandId,
        name: nameController.text.trim(),
        generalPrintThreshold: int.parse(generalPrintController.text.trim()),
        generalMonthsInterval: int.parse(
          generalMonthsController.text.trim(),
        ),
        unitsPrintThreshold: int.parse(unitsPrintController.text.trim()),
        unitsMonthsInterval: int.parse(unitsMonthsController.text.trim()),
        consumablesPrintThreshold: int.parse(
          consumablesController.text.trim(),
        ),
      );
      setState(() {
        _models = [..._models, model];
        _selectedModelId = model.id;
      });
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear el modelo.',
            ),
          ),
        );
      }
    }
  }

  bool get _isValid =>
      _selectedModelId != null && _serialController.text.trim().isNotEmpty;

  Future<void> _submit() async {
    if (!_isValid) return;
    setState(() => _submitting = true);
    try {
      await AssetApi(ApiClient.instance).create(
        assetModelId: _selectedModelId!,
        serialNumber: _serialController.text.trim(),
        type: _type,
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear el activo.',
            ),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Nuevo activo')),
      body: _loadingBrands
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
          ? Center(
              child: Text(
                _loadError!,
                style: const TextStyle(color: AppColors.signalRed),
              ),
            )
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: DropdownButtonFormField<String>(
                        initialValue: _selectedBrandId,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Marca *',
                        ),
                        items: [
                          for (final b in _brands)
                            DropdownMenuItem(value: b.id, child: Text(b.name)),
                        ],
                        onChanged: _onBrandChanged,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: IconButton.outlined(
                        onPressed: _createBrand,
                        icon: const Icon(Icons.add),
                        tooltip: 'Crear marca',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                if (_loadingModels)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: LinearProgressIndicator(),
                  )
                else
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: DropdownButtonFormField<String>(
                          initialValue: _selectedModelId,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'Modelo *',
                          ),
                          items: [
                            for (final m in _models)
                              DropdownMenuItem(
                                value: m.id,
                                child: Text(m.name),
                              ),
                          ],
                          onChanged: _selectedBrandId == null
                              ? null
                              : (value) =>
                                    setState(() => _selectedModelId = value),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: IconButton.outlined(
                          onPressed: _selectedBrandId == null
                              ? null
                              : _createModel,
                          icon: const Icon(Icons.add),
                          tooltip: 'Crear modelo',
                        ),
                      ),
                    ],
                  ),
                const SizedBox(height: 12),
                TextField(
                  controller: _serialController,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(
                    labelText: 'Número de serie *',
                  ),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _type,
                  decoration: const InputDecoration(labelText: 'Tipo'),
                  items: [
                    for (final t in _types)
                      DropdownMenuItem(value: t, child: Text(t)),
                  ],
                  onChanged: (value) =>
                      setState(() => _type = value ?? 'Impresora'),
                ),
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: FilledButton(
                    onPressed: _isValid && !_submitting ? _submit : null,
                    child: _submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text(
                            'Crear activo',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ],
            ),
    );
  }
}
