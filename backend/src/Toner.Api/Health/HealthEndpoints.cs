using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Toner.Api.Health;

public static class HealthEndpoints
{
    // Etiqueta de los checks que cuentan para /health/ready.
    public const string ReadyTag = "ready";

    // AllowAnonymous explícito: el FallbackPolicy exige autenticación en TODO endpoint, y un orquestador
    // (App Service, Docker, K8s) que sondea salud no puede autenticarse.
    //
    //  /health/live  — solo "el proceso responde": sin ninguna dependencia. Es la ruta para el "Health
    //                  check" de Azure App Service: ahí un 503 saca la instancia de rotación, y que
    //                  Blob o SMTP estén caídos no debe tumbar todas las instancias a la vez.
    //  /health/ready — dependencias (Postgres, Blob, SMTP). 503 si algo crítico falla; SMTP degrada
    //                  (200 + "Degraded") porque los correos se reintentan por Hangfire. Para
    //                  monitoreo/alertas y para el calentamiento de un slot antes del swap.
    //  /health       — alias de /health/ready, se conserva por compatibilidad.
    public static IEndpointRouteBuilder MapTonerHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteAsync
        }).AllowAnonymous();

        var ready = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteAsync
        };
        app.MapHealthChecks("/health/ready", ready).AllowAnonymous();
        app.MapHealthChecks("/health", ready).AllowAnonymous();

        return app;
    }

    // Respuesta pública: nombre, estado y duración. Nunca la excepción ni su mensaje (podría traer
    // hosts, cadenas de conexión o rutas internas).
    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        }));
    }
}
