import 'package:flutter/services.dart';

/// Teléfonos: exactamente 10 dígitos numéricos, sin letras, espacios ni símbolos.
/// Misma regla que PhoneRules.cs (backend, la que cuenta como validación) y
/// frontend-web/src/utils/phone.ts.
const int kPhoneLength = 10;

/// Para `TextField.inputFormatters`: solo deja teclear dígitos y corta a 10
/// (también aplica al pegar texto).
final List<TextInputFormatter> phoneInputFormatters = [
  FilteringTextInputFormatter.digitsOnly,
  LengthLimitingTextInputFormatter(kPhoneLength),
];

/// Vacío es válido salvo que el campo sea obligatorio (`required`).
bool isValidPhone(String text, {bool required = false}) {
  final value = text.trim();
  if (value.isEmpty) return !required;
  return value.length == kPhoneLength;
}

/// `errorText` para el TextField: solo avisa cuando ya hay algo escrito pero
/// incompleto (un campo vacío obligatorio ya lo maneja el botón deshabilitado).
String? phoneErrorText(String text) {
  final value = text.trim();
  if (value.isEmpty || value.length == kPhoneLength) return null;
  return 'Deben ser $kPhoneLength dígitos';
}
