import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../models/base_kit.dart';
import '../theme/app_theme.dart';
import 'quantity_stepper.dart';

/// Cantidad de una pieza del kit: unidades con "− n +" o, para un revelador, GRAMOS por cambio en un campo numérico
/// (un paso de 1 g no sirve para 180 g).
class KitQuantityInput extends StatefulWidget {
  const KitQuantityInput({
    super.key,
    required this.value,
    required this.onChanged,
    this.grams = false,
  });

  final int value;
  final bool grams;
  final ValueChanged<int> onChanged;

  @override
  State<KitQuantityInput> createState() => _KitQuantityInputState();
}

class _KitQuantityInputState extends State<KitQuantityInput> {
  late final TextEditingController _controller = TextEditingController(
    text: '${widget.value}',
  );

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.grams) {
      return QuantityStepper(
        value: widget.value,
        max: baseKitMaxQuantity,
        onChanged: widget.onChanged,
      );
    }
    return SizedBox(
      width: 120,
      child: TextField(
        controller: _controller,
        keyboardType: TextInputType.number,
        inputFormatters: [
          FilteringTextInputFormatter.digitsOnly,
          LengthLimitingTextInputFormatter(5),
        ],
        textAlign: TextAlign.end,
        style: AppTextStyles.tabularNumber.copyWith(fontWeight: FontWeight.w700),
        decoration: const InputDecoration(
          isDense: true,
          suffixText: 'g',
          helperText: 'por cambio',
        ),
        onChanged: (v) {
          final n = int.tryParse(v);
          if (n != null) widget.onChanged(n);
        },
      ),
    );
  }
}
