namespace Toner.Application.Common;

// Teléfonos (usuarios, clientes, sedes): exactamente 10 dígitos numéricos, sin letras, espacios ni símbolos.
// La misma regla está duplicada en frontend-web/src/utils/phone.ts y mobile/lib/utils/phone_input.dart para
// que el usuario no pueda ni teclear otra cosa, pero solo esta cuenta como validación (CLAUDE.md).
public static class PhoneRules
{
    public const string Pattern = @"^\d{10}$";
    public const string Message = "El teléfono debe tener exactamente 10 dígitos numéricos.";
}
