namespace Toner.Api.Middleware;

// Refuerzo de backend del flujo de cambio de contraseña forzado: el guard de rutas del frontend es solo
// UX, cualquiera que hable directo con la API seguiría operando indefinidamente con la contraseña
// genérica sin esto. Bloquea con 403 cualquier endpoint fuera de /api/auth/* mientras el claim
// must_change_password del JWT sea true — /api/auth/me y /api/auth/change-password quedan como la única
// vía disponible hasta que el usuario cambie su contraseña.
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var mustChangePasswordClaim = context.User.FindFirst("must_change_password")?.Value;

        var mustBlock = isAuthenticated
            && bool.TryParse(mustChangePasswordClaim, out var mustChangePassword)
            && mustChangePassword
            && !context.Request.Path.StartsWithSegments("/api/auth");

        if (mustBlock)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                title = "Debes cambiar tu contraseña antes de continuar.",
                status = StatusCodes.Status403Forbidden
            });

            return;
        }

        await _next(context);
    }
}
