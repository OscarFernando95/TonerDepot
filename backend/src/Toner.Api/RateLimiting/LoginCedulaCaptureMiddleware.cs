using System.Text.Json;

namespace Toner.Api.RateLimiting;

// El límite del login se reparte por IP + cédula (AUDIT.md H-13), pero la cédula viene en el cuerpo JSON y el
// particionador del rate limiter es síncrono. Este middleware, SOLO en POST /api/auth/login, lee el cuerpo
// (con buffering, para que el controller lo reciba entero), guarda la cédula normalizada y rebobina.
//
// Si el cuerpo no se puede leer (demasiado grande, no es JSON, sin cédula) no se guarda nada y el límite cae a
// solo IP: falla cerrado (más estricto), nunca abierto.
public sealed class LoginCedulaCaptureMiddleware
{
    private const string ItemKey = "RateLimiting.LoginCedula";
    private const int MaxBodyBytes = 4 * 1024;

    private readonly RequestDelegate _next;

    public LoginCedulaCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static string? GetCedula(HttpContext context) => context.Items[ItemKey] as string;

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            && context.Request.ContentLength is null or <= MaxBodyBytes)
        {
            context.Request.EnableBuffering(bufferThreshold: MaxBodyBytes, bufferLimit: MaxBodyBytes);
            try
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("cedula") || property.Name.Equals("cedula", StringComparison.OrdinalIgnoreCase))
                    {
                        var cedula = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()?.Trim() : null;
                        if (!string.IsNullOrEmpty(cedula))
                        {
                            context.Items[ItemKey] = cedula;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
            {
                // Cuerpo ilegible o por encima del límite: sin cédula, el límite queda por IP.
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        await _next(context);
    }
}
