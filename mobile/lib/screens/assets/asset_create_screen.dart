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
                DropdownButtonFormField<String>(
                  initialValue: _selectedBrandId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Marca *'),
                  items: [
                    for (final b in _brands)
                      DropdownMenuItem(value: b.id, child: Text(b.name)),
                  ],
                  onChanged: _onBrandChanged,
                ),
                const SizedBox(height: 12),
                if (_loadingModels)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: LinearProgressIndicator(),
                  )
                else
                  DropdownButtonFormField<String>(
                    initialValue: _selectedModelId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Modelo *'),
                    items: [
                      for (final m in _models)
                        DropdownMenuItem(value: m.id, child: Text(m.name)),
                    ],
                    onChanged: _selectedBrandId == null
                        ? null
                        : (value) => setState(() => _selectedModelId = value),
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
