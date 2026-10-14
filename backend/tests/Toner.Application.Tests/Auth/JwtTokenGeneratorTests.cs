using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Auth;

// AUDIT.md H-31: fija el contenido EXACTO del token (cabecera y payload, decodificados a mano, sin pasar por ningún
// handler de IdentityModel). Cambiar la librería que lo emite (JwtSecurityTokenHandler -> JsonWebTokenHandler) no
// debe cambiar ni un claim: SecurityStampValidator lee "security_stamp" y "jti", los servicios leen "client_id" y
// "technician_id", y el rol viaja con la URI larga de ClaimTypes.Role.
public class JwtTokenGeneratorTests
{
    private static readonly JwtSettings Settings = new()
    {
        Issuer = "test-issuer", Audience = "test-audience",
        SigningKey = "test-signing-key-at-least-32-characters-long", ExpiryMinutes = 60
    };

    private static JsonElement DecodeSegment(string segment)
    {
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64))).RootElement;
    }

    private static Dictionary<string, string> AsStrings(JsonElement obj) =>
        obj.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());

    [Fact]
    public void Token_TieneExactamenteLaCabeceraYLosClaimsEsperados()
    {
        var role = TestEntities.Role(RoleNames.Tecnico);
        var clientId = Guid.NewGuid();
        var user = TestEntities.User(role, email: "tecnico@test.local", clientId: clientId);
        user.Role = role;
        user.FullName = "Técnico Ñandú";
        user.MustChangePassword = false;
        user.Technician = TestEntities.Technician(user);
        var sessionId = Guid.NewGuid();

        var (token, expiresAtUtc) = new JwtTokenGenerator(Options.Create(Settings)).GenerateToken(user, sessionId);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        Assert.Equal(new Dictionary<string, string> { ["alg"] = "HS256", ["typ"] = "JWT" }, AsStrings(DecodeSegment(parts[0])));

        var payload = AsStrings(DecodeSegment(parts[1]));
        var expected = new Dictionary<string, string>
        {
            ["sub"] = user.Id.ToString(),
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] = user.Id.ToString(),
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] = "Técnico Ñandú",
            ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = RoleNames.Tecnico,
            ["must_change_password"] = "False",
            ["security_stamp"] = user.SecurityStamp.ToString(),
            ["jti"] = sessionId.ToString(),
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] = "tecnico@test.local",
            ["client_id"] = clientId.ToString(),
            ["technician_id"] = user.Technician.Id.ToString(),
            ["exp"] = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds().ToString(),
            ["iss"] = "test-issuer",
            ["aud"] = "test-audience"
        };
        Assert.Equal(expected.OrderBy(kv => kv.Key), payload.OrderBy(kv => kv.Key));
    }

    [Fact]
    public void Token_SinCorreoNiClienteNiTecnico_NoLlevaEsosClaims()
    {
        var role = TestEntities.Role(RoleNames.Coordinador);
        var user = TestEntities.User(role);
        user.Role = role;
        user.Email = null;

        var (token, _) = new JwtTokenGenerator(Options.Create(Settings)).GenerateToken(user, Guid.NewGuid());

        var payload = AsStrings(DecodeSegment(token.Split('.')[1]));
        Assert.DoesNotContain("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", payload.Keys);
        Assert.DoesNotContain("client_id", payload.Keys);
        Assert.DoesNotContain("technician_id", payload.Keys);
        Assert.Equal("True", payload["must_change_password"]);
    }
}
