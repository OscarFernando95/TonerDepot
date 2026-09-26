import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/auth_state.dart';
import '../state/my_work_state.dart';
import '../theme/app_theme.dart';
import 'home_screen.dart';
import 'meter_readings_screen.dart';
import 'my_work_screen.dart';

/// Shell con barra lateral (Drawer) y 3 destinos, igual a la navegación del
/// frontend web (AppLayout.vue: rail + contenido) pero en su variante móvil
/// off-canvas. AppBar y logout viven aquí porque son comunes a las 3
/// pestañas; el índice se mantiene con IndexedStack para no perder el
/// estado de cada pestaña al cambiar de una a otra.
class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _index = 0;

  static const _titles = ['Inicio', 'Lectura de contadores', 'Mi trabajo'];

  @override
  void initState() {
    super.initState();
    // "Mi trabajo" ya no dispara su propia carga en initState (dejó de tener
    // Scaffold propio) — se carga una sola vez aquí porque "Inicio" también
    // necesita saber cuántos tickets/órdenes hay para el indicador.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<MyWorkState>().loadAll();
    });
  }

  /// Para navegación desde el Drawer: cierra el panel y cambia de pestaña.
  void _selectFromDrawer(int index) {
    Navigator.of(context).pop();
    setState(() => _index = index);
  }

  /// Para navegación desde botones dentro del contenido (ej. accesos
  /// directos en Inicio) — el Drawer no está abierto, así que no hay nada
  /// que cerrar.
  void _selectTab(int index) => setState(() => _index = index);

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[_index]),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Cerrar sesión',
            onPressed: () => auth.logout(),
          ),
        ],
      ),
      drawer: Drawer(
        backgroundColor: AppColors.boardPanel,
        shape: const RoundedRectangleBorder(),
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
                    if (auth.currentUser != null)
                      Text(auth.currentUser!.fullName, style: const TextStyle(color: AppColors.flapInkDim)),
                  ],
                ),
              ),
              const Divider(height: 1, color: AppColors.boardSeamSoft),
              const SizedBox(height: 8),
              _DrawerItem(
                icon: Icons.home_outlined,
                label: 'Inicio',
                selected: _index == 0,
                onTap: () => _selectFromDrawer(0),
              ),
              _DrawerItem(
                icon: Icons.speed_outlined,
                label: 'Lectura de contadores',
                selected: _index == 1,
                onTap: () => _selectFromDrawer(1),
              ),
              _DrawerItem(
                icon: Icons.work_outline,
                label: 'Mi trabajo',
                selected: _index == 2,
                onTap: () => _selectFromDrawer(2),
              ),
            ],
          ),
        ),
      ),
      body: IndexedStack(
        index: _index,
        children: [
          HomeScreen(onNavigate: _selectTab),
          const MeterReadingsScreen(),
          const MyWorkScreen(),
        ],
      ),
    );
  }
}

class _DrawerItem extends StatelessWidget {
  const _DrawerItem({required this.icon, required this.label, required this.selected, required this.onTap});

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: selected ? AppColors.signalBlueWash : null,
      child: ListTile(
        leading: Icon(icon, color: selected ? AppColors.signalBlueBright : AppColors.flapInkDim),
        title: Text(
          label,
          style: TextStyle(color: selected ? AppColors.flapInk : AppColors.flapInkDim, fontWeight: FontWeight.w600),
        ),
        onTap: onTap,
      ),
    );
  }
}
