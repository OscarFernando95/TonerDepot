using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Toner.Api.RateLimiting;

// Rate limiting de la API (SECURITY_AUDIT.md hallazgo #2; partición por usuario: AUDIT.md H-13).
//
// Antes todo se repartía por IP: una oficina detrás de NAT, o técnicos en CGNAT móvil, compartían un único cupo y
// 10 personas entrando a la vez recibían 429. Ahora:
//   - global:          user:{sub} si la request está autenticada; ip:{ip} si es anónima.
//   - login:           ip + cédula — compañeros de la misma oficina no se bloquean entre sí, insistir sobre una
//                      cédula sí se corta. El global anónimo por IP sigue de tope contra barrer muchas cédulas, y
//                      el bloqueo de cuenta (AuthService, 5 fallos) sigue intacto.
//   - password-change: por usuario (endpoint autenticado).
//
// UseTonerRateLimiting va DESPUÉS de UseAuthentication: el sub sale SOLO de un token ya validado; nunca de un JWT
// sin verificar (un atacante rotaría el sub para esquivar el límite).
public static class RateLimitingSetup
{
    public const string LoginPolicy = "login";
    public const string PasswordChangePolicy = "password-change";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddTonerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var globalLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
        var loginLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
        var passwordChangeLimit = configuration.GetValue("RateLimiting:PasswordChangePermitLimit", 5);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { title = "Demasiadas solicitudes. Intenta de nuevo más tarde.", status = StatusCodes.Status429TooManyRequests },
                    options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            };

            // Sin cola: el exceso se rechaza de inmediato con 429 en vez de esperar turno.
            static RateLimitPartition<string> Fixed(string key, int limit) =>
                RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = Window,
                    QueueLimit = 0
                });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                Fixed(UserId(context) is { } sub ? $"user:{sub}" : $"ip:{Ip(context)}", globalLimit));

            options.AddPolicy(LoginPolicy, context =>
                Fixed(LoginCedulaCaptureMiddleware.GetCedula(context) is { } cedula
                    ? $"login:{Ip(context)}:{cedula}"
                    : $"login:{Ip(context)}", loginLimit));

            options.AddPolicy(PasswordChangePolicy, context =>
                Fixed(UserId(context) is { } sub ? $"pwd:user:{sub}" : $"pwd:ip:{Ip(context)}", passwordChangeLimit));
        });

        return services;
    }

    // Debe ir después de UseAuthentication y antes de UseAuthorization (ver comentario de la clase).
    public static IApplicationBuilder UseTonerRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<LoginCedulaCaptureMiddleware>().UseRateLimiter();

    // IP real del cliente: UseForwardedHeaders ya corrió (ver ReverseProxy en Program.cs y README).
    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string? UserId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
