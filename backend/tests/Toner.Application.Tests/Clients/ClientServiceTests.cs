using Microsoft.EntityFrameworkCore;
using Toner.Application.Clients;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;

namespace Toner.Application.Tests.Clients;

public class ClientServiceTests
{
    [Fact]
    public async Task CreateAsync_WithOneLocation_CreatesClientAndLocationAtomically()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        arrangeDb.Add(city);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ClientService(actDb);

        var result = await service.CreateAsync(new CreateClientRequest
        {
            Name = "Cliente Nuevo",
            Locations = new List<CreateClientLocationRequest>
            {
                new() { CityId = city.Id, Name = "Sede Principal", Address = "Calle 1" }
            }
        });

        Assert.Equal(1, result.LocationCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        Assert.Equal(1, await assertDb.Clients.CountAsync());
        var location = await assertDb.ClientLocations.SingleAsync(l => l.ClientId == result.Id);
        Assert.Equal("Sede Principal", location.Name);
        Assert.Equal(city.Id, location.CityId);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleLocations_CreatesAllAtomically()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var cityA = TestEntities.City("Bogotá", "Cundinamarca");
        var cityB = TestEntities.City("Medellín", "Antioquia");
        arrangeDb.AddRange(cityA, cityB);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ClientService(actDb);

        var result = await service.CreateAsync(new CreateClientRequest
        {
            Name = "Cliente Multi-sede",
            Locations = new List<CreateClientLocationRequest>
            {
                new() { CityId = cityA.Id, Name = "Sede Bogotá", Address = "Calle 1" },
                new() { CityId = cityB.Id, Name = "Sede Medellín", Address = "Calle 2" },
                new() { CityId = cityA.Id, Name = "Sede Bogotá Norte", Address = "Calle 3" }
            }
        });

        Assert.Equal(3, result.LocationCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var locations = await assertDb.ClientLocations.Where(l => l.ClientId == result.Id).ToListAsync();
        Assert.Equal(3, locations.Count);
    }

    [Fact]
    public async Task CreateAsync_WithNoLocations_ThrowsConflictException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = new ClientService(db);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateClientRequest
        {
            Name = "Cliente Sin Sede",
            Locations = new List<CreateClientLocationRequest>()
        }));

        Assert.Equal(0, await db.Clients.CountAsync());
    }
}
