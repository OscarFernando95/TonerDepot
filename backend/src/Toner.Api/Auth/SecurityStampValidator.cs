using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;

namespace Toner.Api.Auth;

// Se engancha a JwtBearerEvents.OnTokenValidated (ver Program.cs) — corre después de que el token
// pasa validación de firma/expiración/issuer/audience, y rechaza cualquier token cuyo claim
// "security_stamp" ya no coincida con el valor actual en BD. Sin esto, desactivar un usuario o
// resetear/cambiar su contraseña no tenía ningún efecto sobre sesiones ya abiertas — el JWT seguía
// siendo válido hasta su expiración natural (hasta 8h), ver SECURITY_AUDIT.md hallazgo #6.
public static class SecurityStampValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stampClaim = context.Principal?.FindFirstValue("security_stamp");

        if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(stampClaim, out var tokenStamp))
        {
            context.Fail("Token inválido.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
        var currentStamp = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.SecurityStamp)
            .FirstOrDefaultAsync();

        if (currentStamp is null || currentStamp.Value != tokenStamp)
        {
            context.Fail("La sesión ya no es válida — la contraseña cambió, se reseteó, o la cuenta fue desactivada/reactivada.");
        }
    }
}
