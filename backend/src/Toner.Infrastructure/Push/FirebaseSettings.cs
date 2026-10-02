namespace Toner.Infrastructure.Push;

public sealed class FirebaseSettings
{
    public const string SectionName = "Firebase";

    // Ruta al JSON de la cuenta de servicio de Firebase. Es un secreto: nunca va versionado, solo por
    // configuración/variable de entorno (Firebase__CredentialsPath). Sin ella las notificaciones push quedan apagadas.
    public string? CredentialsPath { get; set; }
}
