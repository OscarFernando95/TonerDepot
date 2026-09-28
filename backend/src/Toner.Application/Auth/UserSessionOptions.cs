namespace Toner.Application.Auth;

public class UserSessionOptions
{
    // Inactividad máxima para los roles SIN sesión única (todos menos Tecnico). 0 = sin límite.
    public int IdleTimeoutMinutes { get; set; } = 30;

    // Cada cuánto se refresca LastSeenAt como máximo (evita un UPDATE por request).
    public int TouchIntervalSeconds { get; set; } = 60;
}
