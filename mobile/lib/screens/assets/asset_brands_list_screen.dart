import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client.dart';
import '../../state/asset_brands_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../common/placeholder_screen.dart';

/// Espejo de AssetBrandsView.vue — el backend no tiene PUT/DELETE de marca,
/// solo List + Create; la edición vive un nivel abajo, en los modelos.
class AssetBrandsListScreen extends StatelessWidget {
  const AssetBrandsListScreen({super.key});

  Future<void> _showAddBrandDialog(BuildContext context, AssetBrandsState state) async {
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
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Cancelar')),
            FilledButton(
              onPressed: controller.text.trim().isEmpty ? null : () => Navigator.of(dialogContext).pop(controller.text.trim()),
              child: const Text('Crear'),
            ),
          ],
        ),
      ),
    );
    if (name != null && context.mounted) {
      final error = await state.addBrand(name);
      if (error != null && context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AssetBrandsState(ApiClient.instance)..load(),
      child: Consumer<AssetBrandsState>(
        builder: (context, state, _) {
          return Scaffold(
            backgroundColor: Colors.transparent,
            floatingActionButton: FloatingActionButton.extended(
              onPressed: state.busyWithAction ? null : () => _showAddBrandDialog(context, state),
              icon: const Icon(Icons.add),
              label: const Text('Marca'),
            ),
            body: Builder(
              builder: (context) {
                if (state.loading && state.brands.isEmpty) {
                  return const Center(child: CircularProgressIndicator());
                }
                if (state.error != null && state.brands.isEmpty) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
                    ),
                  );
                }
                if (state.brands.isEmpty) {
                  return const PlaceholderScreen(title: 'Sin marcas', message: 'Todavía no hay marcas registradas.');
                }
                return RefreshIndicator(
                  onRefresh: state.load,
                  child: ListView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                    itemCount: state.brands.length,
                    itemBuilder: (context, index) {
                      final brand = state.brands[index];
                      return ClayCard(
                        onTap: () => context.push('/asset-brands/${brand.id}', extra: brand.name),
                        child: Row(
                          children: [
                            const Icon(Icons.category_outlined, color: AppColors.signalBlue),
                            const SizedBox(width: 12),
                            Expanded(child: Text(brand.name, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15))),
                            const Icon(Icons.chevron_right, color: AppColors.inkSecondary),
                          ],
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
