using Toner.Application.Common;
using Toner.Domain.Common;
using Toner.Domain.Entities;

namespace Toner.Application.Tests.TestSupport;

// El alcance por sede de un usuario Cliente se consulta en la base (ClientScope), y un usuario que no existe no ve nada.
// Las pruebas del rol Cliente necesitan, pues, un usuario real sembrado — no un RequestingUser con un id inventado.
public static class TestClientUsers
{
    public static async Task<RequestingUser> SeedAsync(
        string dbName, Guid clientId, bool restrictedToLocations = false, params Guid[] allowedLocationIds)
    {
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role, clientId: clientId);
        user.RestrictedToLocations = restrictedToLocations;
        db.AddRange(role, user);
        foreach (var locationId in allowedLocationIds)
        {
            db.Add(new UserClientLocation { UserId = user.Id, ClientLocationId = locationId, ClientId = clientId });
        }

        await db.SaveChangesAsync();
        return new RequestingUser(user.Id, RoleNames.Cliente, clientId, null);
    }
}
