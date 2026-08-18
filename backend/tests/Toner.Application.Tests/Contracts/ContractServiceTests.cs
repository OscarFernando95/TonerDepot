using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
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

    [Fact]
    public async Task GetByIdAsync_ClienteConClientIdNulo_Deniega()
    {
        // SECURITY_AUDIT.md hallazgo #20: un ClientId nulo debe denegar explícitamente, no colar
        // como si fuera un filtro vacío.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        arrangeDb.AddRange(client, contract);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractService(actDb);
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(malformedClientUser, contract.Id));
    }

    [Fact]
    public async Task ListAsync_ClienteConClientIdNulo_Deniega()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        arrangeDb.AddRange(client, contract);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractService(actDb);
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(malformedClientUser));
    }

    // CODE_QUALITY_AUDIT.md hallazgo #20: un Status que no corresponde a ningún valor de
    // ContractStatus debe mapear a 400 (ValidationException), no explotar como 500.
    [Fact]
    public async Task SetStatusAsync_InvalidStatus_ThrowsValidationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        arrangeDb.AddRange(client, contract);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractService(actDb);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.SetStatusAsync(contract.Id, "NoExiste"));
    }
}
