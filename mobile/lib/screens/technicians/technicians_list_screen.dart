import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/technician.dart';
import '../../services/api_client.dart';
import '../../state/technicians_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../utils/date_only.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de TechniciansView.vue — gestión de cobertura geográfica, no CRUD
/// de la persona/usuario técnico (eso vive en Usuarios, Fase F).
class TechniciansListScreen extends StatelessWidget {
  const TechniciansListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TechniciansState(ApiClient.instance)..load(),
      child: Consumer<TechniciansState>(
        builder: (context, state, _) {
          if (state.loading && state.technicians.isEmpty) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state.error != null && state.technicians.isEmpty) {
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
          if (state.technicians.isEmpty) {
            return const PlaceholderScreen(
              title: 'Sin técnicos',
              message: 'No hay técnicos registrados.',
            );
          }
          return RefreshIndicator(
            onRefresh: state.load,
            child: ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: state.technicians.length,
              itemBuilder: (context, index) =>
                  _TechnicianItem(technician: state.technicians[index]),
            ),
          );
        },
      ),
    );
  }
}

class _TechnicianItem extends StatelessWidget {
  const _TechnicianItem({required this.technician});

  final Technician technician;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push(
        '/technicians/${technician.id}',
        extra: technician.fullName,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  technician.fullName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.technicianStatus(technician.status),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              StatusChip.availability(
                technician.timeOffUntil != null
                    ? 'timeOff'
                    : (technician.isWorkingNow ? 'working' : 'offHours'),
              ),
              if (technician.timeOffUntil != null) ...[
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'hasta ${formatDateTimeShort(technician.timeOffUntil!)}',
                    style: const TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ],
          ),
          if (technician.phone != null) ...[
            const SizedBox(height: 4),
            Text(
              technician.phone!,
              style: const TextStyle(color: AppColors.inkSecondary),
            ),
          ],
          const SizedBox(height: 8),
          Text(
            technician.coverageCityNames.isEmpty
                ? 'Sin ciudades de cobertura'
                : technician.coverageCityNames.join(', '),
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(height: 4),
          Wrap(
            children: [
              TextButton.icon(
                onPressed: () => context.push(
                  '/technicians/${technician.id}/schedule',
                  extra: technician.fullName,
                ),
                icon: const Icon(Icons.schedule, size: 18),
                label: const Text('Horario'),
              ),
              TextButton.icon(
                onPressed: () async {
                  await context.push(
                    '/technicians/${technician.id}/time-off',
                    extra: technician.fullName,
                  );
                  if (context.mounted) context.read<TechniciansState>().load();
                },
                icon: const Icon(Icons.beach_access_outlined, size: 18),
                label: const Text('Fuera de la oficina'),
              ),
              TextButton.icon(
                onPressed: () => context.push(
                  '/technicians/${technician.id}/visits',
                  extra: technician.fullName,
                ),
                icon: const Icon(Icons.pin_drop_outlined, size: 18),
                label: const Text('Visitas'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
