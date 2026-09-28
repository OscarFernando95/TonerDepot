using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Toner.Application.Auth;
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

        // Se trae también IsActive en vez de solo el stamp: que desactivar una cuenta corte las
        // sesiones vivas no debe depender de que UserService.SetActiveStatusAsync se acuerde de
        // regenerar el stamp. Cualquier otra vía que ponga IsActive = false (un script de ops, un
        // UPDATE directo en BD, un endpoint futuro) dejaría los tokens existentes plenamente válidos
        // hasta 8 horas. Es la misma fila indexada por PK, así que verificarlo no cuesta nada más
        // (SECURITY_AUDIT_V2.md hallazgo N4).
        var account = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.SecurityStamp, u.IsActive })
            .FirstOrDefaultAsync();

        if (account is null || !account.IsActive || account.SecurityStamp != tokenStamp)
        {
            context.Fail("La sesión ya no es válida — la contraseña cambió, se reseteó, o la cuenta fue desactivada/reactivada.");
            return;
        }

        // Segundo control, independiente del stamp: la sesión (jti) debe existir y seguir viva. Es lo que
        // hace efectivos el logout, el cierre por administrador, la sesión única del técnico y el
        // timeout por inactividad. Un token sin jti válido (emitido antes de existir UserSessions) se
        // rechaza: obliga a iniciar sesión una vez más tras el despliegue.
        var jtiClaim = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!Guid.TryParse(jtiClaim, out var sessionId))
        {
            context.Fail("Token sin sesión asociada.");
            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
        var result = await sessions.ValidateAsync(sessionId, userId);
        if (result != SessionValidationResult.Valid)
        {
            context.Fail(result == SessionValidationResult.IdleTimeout
                ? "La sesión se cerró por inactividad."
                : "La sesión ya no está activa.");
        }
    }
}
