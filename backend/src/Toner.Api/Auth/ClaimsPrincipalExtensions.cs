using System.Security.Claims;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;

namespace Toner.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    // Única traducción claims -> RequestingUser (antes, 6 controllers tenían su propia copia con
    // Guid.Parse(...!): un claim ausente o malformado terminaba en 500 — AUDIT.md H-25).
    //
    // Falla cerrado: sub y rol son obligatorios; client_id y technician_id son opcionales, pero si
    // vienen y no son un GUID válido se rechaza (403) en vez de tratarlos como "no hay".
    public static RequestingUser ToRequestingUser(this ClaimsPrincipal user)
    {
        var userId = ParseGuid(user.FindFirstValue(ClaimTypes.NameIdentifier));
        var role = user.FindFirstValue(ClaimTypes.Role);

        if (userId is null || string.IsNullOrWhiteSpace(role))
        {
            throw new ForbiddenException("La sesión no tiene una identidad válida.");
        }

        return new RequestingUser(
            userId.Value,
            role,
            ParseOptionalGuid(user, "client_id"),
            ParseOptionalGuid(user, "technician_id"));
    }

    private static Guid? ParseOptionalGuid(ClaimsPrincipal user, string claimType)
    {
        var raw = user.FindFirstValue(claimType);
        if (raw is null)
        {
            return null;
        }

        return ParseGuid(raw) ?? throw new ForbiddenException("La sesión tiene un claim de identidad malformado.");
    }

    private static Guid? ParseGuid(string? raw) =>
        Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
}
