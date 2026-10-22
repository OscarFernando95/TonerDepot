using Microsoft.Extensions.Options;

namespace Toner.Infrastructure.Push;

// Garantiza que el push no se apague en silencio donde importa. El transporte ya se degrada a "apagado" con un
// Warning, pero eso en producción significa técnicos sin avisos y nadie enterado: aquí se falla rápido, igual que
// JwtSettingsValidator.EnsureValid con la clave JWT.
public static class PushStartupGuard
{
    public static bool IsRequired(FirebaseSettings settings, bool isProduction) => settings.Required ?? isProduction;

    public static void EnsureEnabled(IPushTransport transport, IOptions<FirebaseSettings> settings, bool isProduction)
    {
        if (transport.IsEnabled || !IsRequired(settings.Value, isProduction))
        {
            return;
        }

        throw new InvalidOperationException(
            "Las notificaciones push (Firebase) están apagadas y son obligatorias en este entorno. " +
            "Configura Firebase:CredentialsPath (variable Firebase__CredentialsPath) con la ruta al JSON de la cuenta " +
            "de servicio de Firebase; el motivo exacto está en el Warning/Error anterior del log. " +
            "Para desarrollo sin credenciales, fija Firebase:Required=false.");
    }
}
