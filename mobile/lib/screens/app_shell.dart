import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../models/role_names.dart';
import '../router/app_destinations.dart';
import '../state/auth_state.dart';
import '../state/my_work_state.dart';
import '../theme/app_theme.dart';
import '../widgets/glass_panel.dart';

/// Reemplaza a MainShell: ya no arma un IndexedStack fijo de 3 destinos —
/// `child` es lo que resuelve go_router (ver router/app_router.dart) para la
/// ruta activa, y el Drawer se arma filtrando kAppDestinations por el rol
/// del usuario (equivalente móvil de AppLayout.vue + meta.roles).
class AppShell extends StatefulWidget {
  const AppShell({super.key, required this.child});

  final Widget child;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  @override
  void initState() {
    super.initState();
    // "Mi trabajo" es 100% self-service de Tecnico (technician_api.dart) —
    // llamarlo para otro rol solo produce errores 403 que nadie va a ver.
    if (context.read<AuthState>().hasRole(RoleNames.tecnico)) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        context.read<MyWorkState>().loadAll();
      });
    }
  }

  void _selectDestination(AppDestination destination) {
    Navigator.of(context).pop();
    context.go(destination.path);
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    final currentPath = GoRouterState.of(context).matchedLocation;
    final visibleDestinations = kAppDestinations
        .where((d) => auth.hasAnyRole(d.roles))
        .toList();
    AppDestination? current;
    for (final d in kAppDestinations) {
      if (d.path == currentPath) {
        current = d;
        break;
      }
    }

    return Scaffold(
      // Necesario para que GlassAppBar tenga algo real detrás que difuminar
      // (ver _HeaderGlow) — sin esto, el blur no tiene efecto visible.
      extendBodyBehindAppBar: true,
      // El scrim gris oscuro por defecto (45% negro) se mezcla con el blur
      // del Drawer y lo deja con un aspecto sucio/turbio en vez de liviano —
      // con la pantalla ya clara detrás, basta un scrim mucho más sutil.
      drawerScrimColor: AppColors.inkPrimary.withValues(alpha: 0.12),
      appBar: GlassAppBar(
        title: current?.label ?? 'Toner',
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Cerrar sesión',
            onPressed: () => auth.logout(),
          ),
        ],
      ),
      drawer: Drawer(
        backgroundColor: Colors.transparent,
        shape: const RoundedRectangleBorder(),
        // El Drawer sí tiene contenido real detrás (la pantalla, oscurecida
        // por el scrim) — a diferencia del AppBar, aquí el blur funciona sin
        // ningún fondo decorativo de por medio.
        child: GlassPanel(
          // MaterialType.transparency: no pinta color propio (el tinte ya lo
          // pone GlassPanel), pero sí le da a los ListTile de abajo un
          // Material real donde pintar el splash del tap — sin esto, Flutter
          // no puede pintar el ink splash sobre un Container transparente y
          // lo avisa en cada frame ("ink splashes may be invisible").
          child: Material(
            type: MaterialType.transparency,
            child: SafeArea(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(20, 24, 20, 16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'TONER',
                          style: TextStyle(
                            color: AppColors.signalBlueBright,
                            fontWeight: FontWeight.bold,
                            fontSize: 20,
                            letterSpacing: 1.4,
                          ),
                        ),
                        const SizedBox(height: 6),
                        if (auth.currentUser != null) ...[
                          Text(
                            auth.currentUser!.fullName,
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                            ),
                          ),
                          Text(
                            auth.currentUser!.role,
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                              fontSize: 11,
                              letterSpacing: 0.6,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const Divider(height: 1, color: AppColors.neutralSoft),
                  const SizedBox(height: 8),
                  for (final destination in visibleDestinations)
                    _DrawerItem(
                      icon: destination.icon,
                      label: destination.label,
                      selected: destination.path == currentPath,
                      onTap: () => _selectDestination(destination),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
      body: Stack(
        children: [
          const _HeaderGlow(),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.only(top: kToolbarHeight),
              child: widget.child,
            ),
          ),
        ],
      ),
      bottomNavigationBar: _GlassBottomNav(
        destinations: visibleDestinations,
        currentPath: currentPath,
        onSelect: (d) => context.go(d.path),
      ),
    );
  }
}

/// Barra inferior con los mismos destinos que el Drawer (ya filtrados por
/// rol). Un rol de staff tiene 13 destinos, demasiados para una barra fija,
/// así que hace scroll horizontal y mantiene visible el destino activo.
class _GlassBottomNav extends StatefulWidget {
  const _GlassBottomNav({
    required this.destinations,
    required this.currentPath,
    required this.onSelect,
  });

  final List<AppDestination> destinations;
  final String currentPath;
  final ValueChanged<AppDestination> onSelect;

  @override
  State<_GlassBottomNav> createState() => _GlassBottomNavState();
}

class _GlassBottomNavState extends State<_GlassBottomNav> {
  final Map<String, GlobalKey> _itemKeys = {};

  GlobalKey _keyFor(String path) => _itemKeys.putIfAbsent(path, GlobalKey.new);

  @override
  void initState() {
    super.initState();
    _scrollToCurrent();
  }

  @override
  void didUpdateWidget(_GlassBottomNav oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.currentPath != widget.currentPath) _scrollToCurrent();
  }

  void _scrollToCurrent() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final itemContext = _itemKeys[widget.currentPath]?.currentContext;
      if (itemContext == null) return;
      Scrollable.ensureVisible(
        itemContext,
        alignment: 0.5,
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    return GlassPanel(
      child: SafeArea(
        top: false,
        child: SizedBox(
          height: 64,
          // Si los botones caben, quedan centrados; si no, minWidth se
          // ignora y la barra hace scroll horizontal.
          child: LayoutBuilder(
            builder: (context, constraints) => SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: ConstrainedBox(
                constraints: BoxConstraints(minWidth: constraints.maxWidth - 8),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    for (final d in widget.destinations)
                      _BottomNavItem(
                        key: _keyFor(d.path),
                        icon: d.icon,
                        label: d.label,
                        selected: d.path == widget.currentPath,
                        onTap: () => widget.onSelect(d),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _BottomNavItem extends StatelessWidget {
  const _BottomNavItem({
    super.key,
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? AppColors.signalBlueBright : AppColors.inkSecondary;
    return Material(
      type: MaterialType.transparency,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Container(
          width: 84,
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
          decoration: BoxDecoration(
            color: selected ? AppColors.signalBlueWash : null,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: color, size: 24),
              const SizedBox(height: 2),
              Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: color,
                  fontSize: 11,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Fondo decorativo detrás del GlassAppBar y del Drawer — sin esto,
/// `BackdropFilter` solo difumina el color plano de `canvasBg` (o, en
/// pantallas todavía vacías de las Fases C-F, directamente nada) y el
/// "glass" no se distingue de una barra sólida con opacidad. Dos manchas de
/// color en vez de una para que se note incluso si el contenido de abajo es
/// un placeholder en blanco. Ignora eventos de puntero: es puramente visual.
class _HeaderGlow extends StatelessWidget {
  const _HeaderGlow();

  @override
  Widget build(BuildContext context) {
    return const IgnorePointer(
      child: Stack(
        children: [
          Positioned(
            top: -140,
            right: -80,
            width: 300,
            height: 300,
            child: DecoratedBox(
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(
                  colors: [Color(0x662F6FED), Colors.transparent],
                ),
              ),
            ),
          ),
          Positioned(
            top: -100,
            left: -100,
            width: 260,
            height: 260,
            child: DecoratedBox(
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(
                  colors: [Color(0x55D9A441), Colors.transparent],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DrawerItem extends StatelessWidget {
  const _DrawerItem({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: selected ? AppColors.signalBlueWash : null,
      child: ListTile(
        leading: Icon(
          icon,
          color: selected ? AppColors.signalBlueBright : AppColors.inkSecondary,
        ),
        title: Text(
          label,
          style: TextStyle(
            color: selected ? AppColors.inkPrimary : AppColors.inkSecondary,
            fontWeight: FontWeight.w600,
          ),
        ),
        onTap: onTap,
      ),
    );
  }
}
