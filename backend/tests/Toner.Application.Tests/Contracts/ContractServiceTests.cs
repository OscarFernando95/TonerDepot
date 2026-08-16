using Toner.Application.Common;
using Toner.Application.Contracts;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;

namespace Toner.Application.Tests.Contracts;

public class ContractServiceTests
{
    [Fact]
    public async Task ListAsync_ContractClientWithLocationsInMultipleCities_ReturnsDistinctSortedCityNames()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var cityA = TestEntities.City("Medellín", "Antioquia");
        var cityB = TestEntities.City("Bogotá", "Cundinamarca");
        var client = TestEntities.Client();
        var locationA = TestEntities.ClientLocation(client, cityA);
        var locationB = TestEntities.ClientLocation(client, cityB, "Sede Bogotá");
        var contract = TestEntities.Contract(client);
        arrangeDb.AddRange(cityA, cityB, client, locationA, locationB, contract);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractService(actDb);
        var staffUser = new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null);

        var result = await service.GetByIdAsync(staffUser, contract.Id);

        Assert.Equal(new[] { "Bogotá", "Medellín" }, result.CityNames);
    }
}
