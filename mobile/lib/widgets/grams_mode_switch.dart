import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../utils/grams_display.dart';

/// Interruptor "Gramos / Kg y bolsas" para ver el saldo del revelador. Se guarda en el teléfono y vale para toda la app.
class GramsModeSwitch extends StatefulWidget {
  const GramsModeSwitch({super.key});

  @override
  State<GramsModeSwitch> createState() => _GramsModeSwitchState();
}

class _GramsModeSwitchState extends State<GramsModeSwitch> {
  @override
  void initState() {
    super.initState();
    loadGramsMode();
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<GramsMode>(
      valueListenable: gramsMode,
      builder: (context, mode, _) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text(
            'Revelador',
            style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(width: 8),
          SegmentedButton<GramsMode>(
            showSelectedIcon: false,
            style: const ButtonStyle(
              visualDensity: VisualDensity.compact,
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            ),
            segments: const [
              ButtonSegment(value: GramsMode.grams, label: Text('Gramos')),
              ButtonSegment(value: GramsMode.kgBags, label: Text('Kg y bolsas')),
            ],
            selected: {mode},
            onSelectionChanged: (s) => setGramsMode(s.first),
          ),
        ],
      ),
    );
  }
}
