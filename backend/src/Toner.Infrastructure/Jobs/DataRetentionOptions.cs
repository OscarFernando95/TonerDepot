namespace Toner.Infrastructure.Jobs;

// Sección "Retention" de la configuración (AUDIT.md H-27).
public class DataRetentionOptions
{
    // El mínimo evita que un "0" o un typo vacíe la tabla completa en la primera corrida.
    public const int MinimumDays = 7;

    // UserSessions: solo se purgan sesiones ya terminadas (revocadas, o vencidas sin revocar) cuyo fin
    // supera este plazo. Una sesión viva nunca se toca, sea cual sea su antigüedad.
    public int UserSessionsDays { get; set; } = 90;

    // ExceptionLogs: se purgan las filas con CreatedAt anterior al corte.
    public int ExceptionLogsDays { get; set; } = 90;

    // Falla rápido al arrancar, igual que los demás validadores de configuración (CLAUDE.md, Secretos).
    public void EnsureValid()
    {
        if (UserSessionsDays < MinimumDays)
        {
            throw new InvalidOperationException(
                $"Retention:UserSessionsDays debe ser al menos {MinimumDays} (valor actual: {UserSessionsDays}).");
        }

        if (ExceptionLogsDays < MinimumDays)
        {
            throw new InvalidOperationException(
                $"Retention:ExceptionLogsDays debe ser al menos {MinimumDays} (valor actual: {ExceptionLogsDays}).");
        }
    }
}
