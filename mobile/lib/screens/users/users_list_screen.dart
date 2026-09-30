import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/managed_user.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../state/users_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_choice_chip.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de UsersView.vue — Administrador únicamente. GET /users no tiene
/// filtros server-side, mismo patrón que Activos/Contratos: pagina de
/// verdad y filtra por rol client-side.
class UsersListScreen extends StatelessWidget {
  const UsersListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => UsersListState(ApiClient.instance)..load(),
      // Builder para que el `context` de abajo (FAB incluido) sí vea el
      // UsersListState que se acaba de crear — el `context` del propio
      // build() de este StatelessWidget es su ancestro, no su descendiente
      // (causa real del bug: la lista no se refrescaba tras crear usuario).
      child: Builder(
        builder: (context) => Scaffold(
          backgroundColor: Colors.transparent,
          floatingActionButton: FloatingActionButton.extended(
            onPressed: () async {
              final created = await context.push<bool>('/users/new');
              if (created == true && context.mounted) {
                context.read<UsersListState>().load();
              }
            },
            icon: const Icon(Icons.add),
            label: const Text('Usuario'),
          ),
          body: Consumer<UsersListState>(
            builder: (context, state, _) {
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                    child: Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: [
                        ClayChoiceChip(
                          label: const Text('Todos'),
                          selected: state.roleFilter == null,
                          onSelected: (_) => state.setRoleFilter(null),
                        ),
                        for (final role in RoleNames.all)
                          ClayChoiceChip(
                            label: Text(role),
                            selected: state.roleFilter == role,
                            onSelected: (_) => state.setRoleFilter(role),
                          ),
                      ],
                    ),
                  ),
                  Expanded(
                    child: Builder(
                      builder: (context) {
                        if (state.loading && state.users.isEmpty) {
                          return const Center(
                            child: CircularProgressIndicator(),
                          );
                        }
                        if (state.error != null && state.users.isEmpty) {
                          return Center(
                            child: Padding(
                              padding: const EdgeInsets.all(24),
                              child: Text(
                                state.error!,
                                style: const TextStyle(
                                  color: AppColors.signalRed,
                                ),
                                textAlign: TextAlign.center,
                              ),
                            ),
                          );
                        }
                        final users = state.filtered;
                        if (users.isEmpty) {
                          return const PlaceholderScreen(
                            title: 'Sin usuarios',
                            message:
                                'No hay usuarios que coincidan con el filtro.',
                          );
                        }
                        return RefreshIndicator(
                          onRefresh: state.load,
                          child: NotificationListener<ScrollNotification>(
                            onNotification: (notification) {
                              if (notification.metrics.pixels >=
                                  notification.metrics.maxScrollExtent - 200) {
                                state.loadMore();
                              }
                              return false;
                            },
                            child: ListView.builder(
                              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
                              itemCount: users.length + (state.hasMore ? 1 : 0),
                              itemBuilder: (context, index) {
                                if (index >= users.length) {
                                  return const Padding(
                                    padding: EdgeInsets.symmetric(vertical: 16),
                                    child: Center(
                                      child: CircularProgressIndicator(),
                                    ),
                                  );
                                }
                                return _UserItem(user: users[index]);
                              },
                            ),
                          ),
                        );
                      },
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

class _UserItem extends StatelessWidget {
  const _UserItem({required this.user});

  final ManagedUser user;

  // Ícono según rol — ver RoleNames para los strings exactos del backend.
  IconData get _roleIcon {
    switch (user.roleName) {
      case RoleNames.administrador:
        return Icons.admin_panel_settings_outlined;
      case RoleNames.coordinador:
        return Icons.supervisor_account_outlined;
      case RoleNames.tecnico:
        return Icons.engineering_outlined;
      case RoleNames.cliente:
        return Icons.storefront_outlined;
      case RoleNames.ventas:
        return Icons.point_of_sale_outlined;
      default:
        return Icons.person_outline;
    }
  }

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      // Igual que Activos/Contratos: el detalle no devuelve datos al volver,
      // pull-to-refresh en la lista es lo que trae los cambios.
      onTap: () => context.push('/users/${user.id}', extra: user),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ClayIconBadge(
                icon: _roleIcon,
                color: user.isActive ? AppColors.signalBlue : AppColors.neutral,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  user.fullName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.activeState(user.isActive),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Cédula: ${user.cedula} — ${user.roleName}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          if (user.cityName != null)
            Text(
              user.cityName!,
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
        ],
      ),
    );
  }
}
