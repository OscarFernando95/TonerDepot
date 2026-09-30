import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../models/city.dart';
import '../../models/managed_user.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../services/city_api.dart';
import '../../state/auth_state.dart';
import '../../state/user_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../utils/phone_input.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/department_city_picker.dart';
import '../../widgets/status_chip.dart';

// Espejo de _roleIcon en users_list_screen.dart.
IconData _roleIcon(String roleName) {
  switch (roleName) {
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

class UserDetailScreen extends StatelessWidget {
  const UserDetailScreen({super.key, required this.user});

  final ManagedUser user;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => UserDetailState(ApiClient.instance, user),
      child: Scaffold(
        appBar: AppBar(title: const Text('Usuario')),
        body: const _UserDetailBody(),
      ),
    );
  }
}

class _UserDetailBody extends StatelessWidget {
  const _UserDetailBody();

  Future<void> _showEditDialog(
    BuildContext context,
    UserDetailState state,
  ) async {
    final user = state.user;
    final cedulaController = TextEditingController(text: user.cedula);
    final fullNameController = TextEditingController(text: user.fullName);
    final emailController = TextEditingController(text: user.email ?? '');
    final phoneController = TextEditingController(text: user.phone ?? '');
    final addressController = TextEditingController(text: user.address ?? '');
    String? cityId = user.cityId;
    // Cascada Departamento→Ciudad: se deriva de la ciudad actual del
    // usuario una vez carguen los municipios (mismo patrón que
    // UsersView.vue al abrir el diálogo de edición).
    String? departmentName;
    List<City> cities = [];
    bool loading = true;

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          if (loading) {
            CityApi(ApiClient.instance).list().then((loaded) {
              cities = loaded;
              departmentName = cities
                  .where((c) => c.id == cityId)
                  .map((c) => c.stateOrProvince)
                  .firstOrNull;
              setDialogState(() => loading = false);
            });
            return const AlertDialog(
              content: SizedBox(
                height: 120,
                child: Center(child: CircularProgressIndicator()),
              ),
            );
          }
          bool isValid() =>
              cedulaController.text.trim().isNotEmpty &&
              fullNameController.text.trim().isNotEmpty &&
              isValidPhone(phoneController.text, required: true) &&
              addressController.text.trim().isNotEmpty &&
              cityId != null;
          return AlertDialog(
            title: const Text('Editar usuario'),
            content: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: cedulaController,
                      onChanged: (_) => setDialogState(() {}),
                      decoration: const InputDecoration(labelText: 'Cédula *'),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: fullNameController,
                      onChanged: (_) => setDialogState(() {}),
                      decoration: const InputDecoration(
                        labelText: 'Nombre completo *',
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: emailController,
                      decoration: const InputDecoration(labelText: 'Correo'),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: phoneController,
                      keyboardType: TextInputType.phone,
                      inputFormatters: phoneInputFormatters,
                      onChanged: (_) => setDialogState(() {}),
                      decoration: InputDecoration(
                        labelText: 'Teléfono *',
                        errorText: phoneErrorText(phoneController.text),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: addressController,
                      onChanged: (_) => setDialogState(() {}),
                      decoration: const InputDecoration(
                        labelText: 'Dirección *',
                      ),
                    ),
                    const SizedBox(height: 12),
                    DepartmentCityPicker(
                      cities: cities,
                      initialDepartmentName: departmentName,
                      initialCityId: cityId,
                      onChanged: (newDepartment, newCityId) => setDialogState(() {
                        departmentName = newDepartment;
                        cityId = newCityId;
                      }),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: isValid()
                    ? () async {
                        Navigator.of(dialogContext).pop();
                        final error = await state.updateUser(
                          cedula: cedulaController.text.trim(),
                          email: emailController.text.trim().isEmpty
                              ? null
                              : emailController.text.trim(),
                          fullName: fullNameController.text.trim(),
                          phone: phoneController.text.trim(),
                          address: addressController.text.trim(),
                          cityId: cityId!,
                        );
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(error ?? 'Usuario actualizado.'),
                            ),
                          );
                        }
                      }
                    : null,
                child: const Text('Guardar'),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _confirmResetPassword(
    BuildContext context,
    UserDetailState state,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('¿Restablecer contraseña?'),
        content: const Text(
          'Se genera una contraseña nueva y se envía por correo. Esto cierra cualquier sesión activa de este usuario.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Restablecer'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    final result = await state.resetPassword();
    if (!context.mounted) return;
    if (result.error != null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(result.error!)));
      return;
    }
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Contraseña restablecida'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Ya se envió por correo, pero esta es la única vez que se muestra en pantalla:',
            ),
            const SizedBox(height: 12),
            SelectableText(
              result.password!,
              style: const TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 18,
              ).merge(AppTextStyles.tabularNumber),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () {
              Clipboard.setData(ClipboardData(text: result.password!));
              ScaffoldMessenger.of(dialogContext).showSnackBar(
                const SnackBar(content: Text('Contraseña copiada.')),
              );
            },
            child: const Text('Copiar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Listo'),
          ),
        ],
      ),
    );
  }

  Future<void> _confirmRevokeSessions(
    BuildContext context,
    UserDetailState state,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('¿Cerrar sesiones activas?'),
        content: const Text(
          'Se cierran todas las sesiones abiertas de este usuario (web y app). Tendrá que iniciar sesión de nuevo.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Cerrar sesiones'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    final result = await state.revokeSessions();
    if (!context.mounted) return;
    final message =
        result.error ??
        (result.revoked! > 0
            ? 'Se cerraron ${result.revoked} sesión(es).'
            : 'El usuario no tenía sesiones activas.');
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<UserDetailState>();
    final auth = context.watch<AuthState>();
    final user = state.user;
    // El backend no protege contra auto-desactivación — lo hacemos acá para
    // que un Administrador no se bloquee a sí mismo por accidente.
    final isSelf = user.id == auth.currentUser?.id;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ClaySurface(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  // Insignia de cabecera — mismo ícono por rol que
                  // users_list_screen.dart, color según si está activo.
                  ClayIconBadge(
                    icon: _roleIcon(user.roleName),
                    color: user.isActive
                        ? AppColors.signalBlue
                        : AppColors.neutral,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      user.fullName,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  StatusChip.activeState(user.isActive),
                ],
              ),
              Text(
                'Cédula: ${user.cedula}',
                style: const TextStyle(color: AppColors.inkSecondary),
              ),
              Text(
                'Rol: ${user.roleName}',
                style: const TextStyle(color: AppColors.inkSecondary),
              ),
              if (user.email != null)
                Text(
                  user.email!,
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
              if (user.phone != null)
                Text(
                  user.phone!,
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
              if (user.address != null)
                Text(
                  user.address!,
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
              if (user.cityName != null)
                Text(
                  user.cityName!,
                  style: const TextStyle(color: AppColors.inkSecondary),
                ),
              if (user.mustChangePassword)
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    'Pendiente de cambiar su contraseña inicial.',
                    style: TextStyle(fontSize: 12, fontStyle: FontStyle.italic),
                  ),
                ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: state.busyWithAction
                          ? null
                          : () => _showEditDialog(context, state),
                      icon: const Icon(Icons.edit_outlined, size: 18),
                      label: const Text('Editar'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: FilledButton.icon(
                      onPressed: state.busyWithAction || isSelf
                          ? null
                          : () async {
                              final error = await state.setStatus(
                                !user.isActive,
                              );
                              if (context.mounted) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text(
                                      error ?? 'Estado actualizado.',
                                    ),
                                  ),
                                );
                              }
                            },
                      style: user.isActive
                          ? FilledButton.styleFrom(
                              backgroundColor: AppColors.signalRed,
                            )
                          : null,
                      icon: Icon(
                        user.isActive
                            ? Icons.block
                            : Icons.check_circle_outline,
                        size: 18,
                      ),
                      label: Text(user.isActive ? 'Desactivar' : 'Activar'),
                    ),
                  ),
                ],
              ),
              if (isSelf)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text(
                    'No puedes desactivar tu propia cuenta desde acá.',
                    style: TextStyle(
                      color: AppColors.inkSecondary,
                      fontSize: 12,
                    ),
                  ),
                ),
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: state.busyWithAction
                      ? null
                      : () => _confirmResetPassword(context, state),
                  icon: const Icon(Icons.password_outlined, size: 18),
                  label: const Text('Restablecer contraseña'),
                ),
              ),
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: state.busyWithAction
                      ? null
                      : () => _confirmRevokeSessions(context, state),
                  icon: const Icon(Icons.logout, size: 18),
                  label: const Text('Cerrar sesiones activas'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
