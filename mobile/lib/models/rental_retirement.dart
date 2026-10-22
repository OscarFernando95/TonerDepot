/// Salida de alquiler por modelo: lógica pura (textos, formato, decisión de
/// mostrar) del límite de impresiones de un modelo de equipo. Es solo
/// informativo — el servidor calcula `printsUntilRetirement` y
/// `rentalRetirementStatus` (null | 'Proxima' | 'Superada'); el rol Cliente
/// nunca recibe estos datos (llegan null), así que si el estado es null no se
/// muestra nada.
library;

/// Aviso por defecto: cuando faltan 50.000 impresiones o menos.
const int defaultRentalRetirementWarningPrints = 50000;

/// Formatea un entero con separador de miles es-CO ('.'): 50000 -> '50.000'.
/// Sin dependencia de `intl`/datos de locale para que sea testeable y estable.
String formatPrintsEsCo(int value) {
  final digits = value.abs().toString();
  final buffer = StringBuffer(value < 0 ? '-' : '');
  for (var i = 0; i < digits.length; i++) {
    if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
    buffer.write(digits[i]);
  }
  return buffer.toString();
}

/// Aviso de salida de alquiler listo para pintar.
class RentalRetirementAlert {
  const RentalRetirementAlert._({
    required this.exceeded,
    required this.label,
    required this.detail,
  });

  /// true = 'Superada' (rojo); false = 'Proxima' (ámbar).
  final bool exceeded;

  /// Texto del chip.
  final String label;

  /// 'Faltan N impresiones' / 'Superó el límite por N impresiones'; null si el
  /// servidor no mandó el conteo.
  final String? detail;

  /// Decide si hay algo que mostrar. null = no mostrar nada (sin límite, sin
  /// lecturas, rol Cliente o un estado desconocido).
  static RentalRetirementAlert? from({
    required String? status,
    required int? printsUntilRetirement,
  }) {
    final bool exceeded;
    switch (status) {
      case 'Proxima':
        exceeded = false;
      case 'Superada':
        exceeded = true;
      default:
        return null;
    }
    final n = printsUntilRetirement;
    String? detail;
    if (n != null) {
      if (exceeded) {
        detail =
            'Superó el límite por ${formatPrintsEsCo(n.abs())} impresiones';
      } else {
        detail = n == 1
            ? 'Falta 1 impresión'
            : 'Faltan ${formatPrintsEsCo(n)} impresiones';
      }
    }
    return RentalRetirementAlert._(
      exceeded: exceeded,
      label: exceeded
          ? 'Superó el límite de alquiler'
          : 'Próxima a salir de alquiler',
      detail: detail,
    );
  }
}

/// Lee un entero tolerando null, int y num (el JSON puede traer 5.0).
int? parseNullableInt(Object? value) => value is num ? value.toInt() : null;

/// Validación de "Impresiones para salir de alquiler": opcional (vacío = sin
/// límite), entero > 0. null = válido.
String? validateRentalThreshold(String? text) {
  final t = (text ?? '').trim();
  if (t.isEmpty) return null;
  final n = int.tryParse(t);
  if (n == null || n <= 0) return 'Debe ser un entero mayor que 0.';
  return null;
}

/// Validación de "Avisar cuando falten (impresiones)": obligatorio, entero > 0.
String? validateRentalWarning(String? text) {
  final t = (text ?? '').trim();
  final n = int.tryParse(t);
  if (n == null || n <= 0) return 'Debe ser un entero mayor que 0.';
  return null;
}

/// Convierte el texto del umbral al valor a enviar: vacío -> null (sin límite).
int? parseRentalThreshold(String? text) {
  final t = (text ?? '').trim();
  return t.isEmpty ? null : int.tryParse(t);
}

/// Línea de resumen del límite del modelo, donde se listan los otros umbrales
/// ('Salida de alquiler: 200.000 impr. (aviso a 50.000)' o 'Sin límite').
String rentalThresholdSummary(int? threshold, int warningPrints) {
  if (threshold == null) return 'Salida de alquiler: sin límite';
  return 'Salida de alquiler: ${formatPrintsEsCo(threshold)} impr. '
      '(aviso a ${formatPrintsEsCo(warningPrints)})';
}
