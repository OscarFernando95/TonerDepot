import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// El revelador se guarda y se descuenta SIEMPRE en gramos; esto solo decide cómo se muestra:
///   [GramsMode.grams] → "1.250 g"
///   [GramsMode.kgBags] → "1,25 kg (2 bolsas + 250 g)"
enum GramsMode { grams, kgBags }

const _prefsKey = 'toner.gramsMode';

/// Preferencia compartida por toda la app: cambiarla en una pantalla la cambia en todas.
final ValueNotifier<GramsMode> gramsMode = ValueNotifier(GramsMode.grams);

bool _loaded = false;

Future<void> loadGramsMode() async {
  if (_loaded) return;
  _loaded = true;
  try {
    final prefs = await SharedPreferences.getInstance();
    gramsMode.value = prefs.getString(_prefsKey) == 'kg'
        ? GramsMode.kgBags
        : GramsMode.grams;
  } catch (e, st) {
    debugPrint('loadGramsMode failed: $e\n$st');
  }
}

Future<void> setGramsMode(GramsMode mode) async {
  gramsMode.value = mode;
  try {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_prefsKey, mode == GramsMode.kgBags ? 'kg' : 'g');
  } catch (e, st) {
    debugPrint('setGramsMode failed: $e\n$st');
  }
}

String _thousands(int value) => value.toString().replaceAllMapped(
  RegExp(r'\B(?=(\d{3})+(?!\d))'),
  (_) => '.',
);

String _kg(double kg) {
  final text = kg
      .toStringAsFixed(3)
      .replaceFirst(RegExp(r'0+$'), '')
      .replaceFirst(RegExp(r'\.$'), '');
  return text.replaceAll('.', ',');
}

String _bags(int count) => '${_thousands(count)} ${count == 1 ? 'bolsa' : 'bolsas'}';

String formatGrams(int grams, int? gramsPerBag, {GramsMode? mode}) {
  final effective = mode ?? gramsMode.value;
  if (effective == GramsMode.grams) return '${_thousands(grams)} g';

  final sign = grams < 0 ? '-' : '';
  final abs = grams.abs();
  final kg = '$sign${_kg(abs / 1000)} kg';
  if (gramsPerBag == null || gramsPerBag <= 0) return kg;

  final bags = abs ~/ gramsPerBag;
  final rest = abs - bags * gramsPerBag;
  final parts = <String>[
    if (bags > 0) _bags(bags),
    if (rest > 0) '${_thousands(rest)} g',
  ];
  return parts.isEmpty ? kg : '$kg ($sign${parts.join(' + ')})';
}

/// Cantidad de un ítem: gramos del revelador (según el modo elegido) o el número tal cual para el resto.
String formatQuantity(int quantity, int? gramsPerBag, {GramsMode? mode}) =>
    gramsPerBag == null
    ? '$quantity'
    : formatGrams(quantity, gramsPerBag, mode: mode);
