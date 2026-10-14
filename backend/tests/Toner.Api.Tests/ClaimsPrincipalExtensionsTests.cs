using System.Security.Claims;
using Toner.Api.Auth;
using Toner.Application.Common.Exceptions;
using Toner.Domain.Common;

namespace Toner.Api.Tests;

// AUDIT.md H-25.
public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Staff_SinClientNiTechnicianId()
    {
        var userId = Guid.NewGuid();

        var user = Principal((ClaimTypes.NameIdentifier, userId.ToString()), (ClaimTypes.Role, RoleNames.Coordinador))
            .ToRequestingUser();

        Assert.Equal(userId, user.UserId);
        Assert.Equal(RoleNames.Coordinador, user.Role);
        Assert.Null(user.ClientId);
        Assert.Null(user.TechnicianId);
    }

    [Fact]
    public void Cliente_ConClientId()
    {
        var clientId = Guid.NewGuid();

        var user = Principal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), (ClaimTypes.Role, RoleNames.Cliente),
            ("client_id", clientId.ToString())).ToRequestingUser();

        Assert.Equal(clientId, user.ClientId);
    }

    [Fact]
    public void Tecnico_ConTechnicianId()
    {
        var technicianId = Guid.NewGuid();

        var user = Principal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), (ClaimTypes.Role, RoleNames.Tecnico),
            ("technician_id", technicianId.ToString())).ToRequestingUser();

        Assert.Equal(technicianId, user.TechnicianId);
    }

    [Fact]
    public void SinIdentidad_LanzaForbidden()
    {
        var principal = Principal((ClaimTypes.Role, RoleNames.Cliente));
        Assert.Throws<ForbiddenException>(() => principal.ToRequestingUser());
    }

    [Fact]
    public void SinRol_LanzaForbidden()
    {
        var principal = Principal((ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        Assert.Throws<ForbiddenException>(() => principal.ToRequestingUser());
    }

    [Theory]
    [InlineData("client_id")]
    [InlineData("technician_id")]
    public void ClaimOpcionalMalformado_LanzaForbidden_NoLoTrataComoAusente(string claim)
    {
        var principal = Principal(
            (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), (ClaimTypes.Role, RoleNames.Cliente), (claim, "basura"));

        Assert.Throws<ForbiddenException>(() => principal.ToRequestingUser());
    }
}
