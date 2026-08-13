namespace Toner.Application.Common.Interfaces;

// Persiste una excepción no controlada en un almacén durable (tabla ExceptionLogs), independiente
// de los logs de consola/archivo. Application define el contrato; Infrastructure decide cómo y dónde
// guardarlo — así el middleware de la Api y los jobs en background no dependen de EF Core ni de Npgsql
// directamente, solo de esta abstracción.
public interface IExceptionLogger
{
    Task LogAsync(
        string source,
        Exception exception,
        string? requestMethod = null,
        string? requestPath = null,
        int? statusCode = null,
        Guid? userId = null,
        string? userEmail = null,
        CancellationToken cancellationToken = default);
}
