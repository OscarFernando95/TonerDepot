import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../services/api_client.dart';
import '../../state/my_contracts_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Portal Cliente — espejo de solo lectura de MyContractsView.vue.
class MyContractsScreen extends StatelessWidget {
  const MyContractsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MyContractsState(ApiClient.instance)..load(),
      child: const _MyContractsBody(),
    );
  }
}

class _MyContractsBody extends StatelessWidget {
  const _MyContractsBody();

  String _formatDate(String iso) => iso.split('T').first;

  @override
  Widget build(BuildContext context) {
    return Consumer<MyContractsState>(
      builder: (context, state, _) {
        if (state.loading && state.contracts.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.contracts.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                state.error!,
                style: const TextStyle(color: AppColors.signalRed),
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        if (state.contracts.isEmpty) {
          return const PlaceholderScreen(
            title: 'Sin contratos',
            message: 'Todavía no tienes contratos registrados.',
          );
        }
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView.builder(
            padding: const EdgeInsets.all(16),
            itemCount: state.contracts.length,
            itemBuilder: (context, index) {
              final contract = state.contracts[index];
              return ClayCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            contract.cityNames.isEmpty
                                ? 'Contrato'
                                : contract.cityNames.join(', '),
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                            ),
                          ),
                        ),
                        StatusChip.contract(contract.status),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Vigencia: ${_formatDate(contract.startDate)}'
                      '${contract.endDate != null ? ' a ${_formatDate(contract.endDate!)}' : ' (sin fecha de fin)'}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    Text(
                      '${contract.assetCount} equipo(s) asociados',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    if (contract.includedPrintsPerMonth != null)
                      Text(
                        'Impresiones incluidas/mes: ${contract.includedPrintsPerMonth}',
                        style: const TextStyle(
                          color: AppColors.inkSecondary,
                          fontSize: 12,
                        ).merge(AppTextStyles.tabularNumber),
                      ),
                    if (contract.notes != null && contract.notes!.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          contract.notes!,
                          style: const TextStyle(
                            fontSize: 12,
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ),
                  ],
                ),
              );
            },
          ),
        );
      },
    );
  }
}
