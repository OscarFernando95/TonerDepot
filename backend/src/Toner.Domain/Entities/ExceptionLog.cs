using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Registro persistente de errores no controlados (ver Toner.Api.Middleware.ExceptionHandlingMiddleware
// y Toner.Infrastructure.Jobs.MaintenanceScheduleEvaluationJob). Existe porque los logs de
// consola/archivo y el dashboard de Hangfire no son accesibles en producción, y un 500 sin rastro
// persistente es prácticamente indiagnosticable después del hecho.
public class ExceptionLog : BaseEntity
{
    // Dónde ocurrió: "Api" para requests HTTP, "Jobs.<NombreDelJob>" para trabajos en background.
    public string Source { get; set; } = string.Empty;

    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }

    // Solo aplican cuando Source es una request HTTP.
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }

    // Denormalizado (no FK) a propósito: un log de errores debe seguir siendo válido aunque el
    // usuario referenciado cambie o se desactive más adelante.
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
}
