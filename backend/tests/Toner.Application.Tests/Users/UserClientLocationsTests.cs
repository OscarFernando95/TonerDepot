using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Users;
using Toner.Application.Users.Dtos;
using Toner.Application.Users.Validators;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Users;

// El administrador decide a qué sedes de su empresa accede un usuario Cliente, al crearlo o después.
public class UserClientLocationsTests
{
    private sealed record World(string DbName, Client Empresa, Client Otra, ClientLocation Norte, ClientLocation Sur, ClientLocation Ajena, City City, User ClientUser, User Tecnico);

    private static async Task<World> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var empresa = TestEntities.Client("Empresa");
        var otra = TestEntities.Client("Otra");
        var norte = TestEntities.ClientLocation(empresa, city, "Norte");
        var sur = TestEntities.ClientLocation(empresa, city, "Sur");
        var ajena = TestEntities.ClientLocation(otra, city, "Ajena");
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var adminRole = TestEntities.Role(RoleNames.Administrador);
        var clientUser = TestEntities.User(clientRole, clientId: empresa.Id);
        var tecnico = TestEntities.User(techRole);
        db.AddRange(city, empresa, otra, norte, sur, ajena, clientRole, techRole, adminRole, clientUser, tecnico);
        await db.SaveChangesAsync();
        return new World(dbName, empresa, otra, norte, sur, ajena, city, clientUser, tecnico);
    }

    private static UserService Service(TonerDbContext db) =>
        new(db, new BCryptPasswordHasher(), new FakeBackgroundJobScheduler(), NullLogger<UserService>.Instance, TestSessions.Create(db));

    private static CreateUserRequest NewClientUser(World w, IReadOnlyList<Guid>? allowed) => new()
    {
        Cedula = "9" + Random.Shared.Next(100_000_000, 999_999_999), FullName = "Cliente Nuevo", Phone = "3001234567",
        Address = "Calle 1", CityId = w.City.Id, RoleName = RoleNames.Cliente, ClientId = w.Empresa.Id, AllowedLocationIds = allowed
    };

    [Fact]
    public async Task Crear_ConSedes_QuedaRestringidoASoloEsas()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var created = await Service(db).CreateAsync(NewClientUser(w, new[] { w.Norte.Id }));

        Assert.True(created.RestrictedToLocations);
        Assert.Equal(new[] { w.Norte.Id }, created.AllowedLocationIds);
        var row = await db.UserClientLocations.SingleAsync(l => l.UserId == created.Id);
        Assert.Equal(w.Empresa.Id, row.ClientId); // capturado de la empresa, para la política RLS
    }

    [Fact]
    public async Task Crear_SinSedes_VeTodasLasDeSuEmpresa()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var created = await Service(db).CreateAsync(NewClientUser(w, null));

        Assert.False(created.RestrictedToLocations);
        Assert.Empty(created.AllowedLocationIds);
        Assert.Empty(db.UserClientLocations);
    }

    [Fact]
    public async Task Asignar_ReemplazaLasSedes_SinDuplicarLasQueSeConservan()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);
        await service.SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id } });

        var dto = await service.SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id, w.Sur.Id } });
        Assert.Equivalent(new[] { w.Norte.Id, w.Sur.Id }, dto.AllowedLocationIds);

        dto = await service.SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Sur.Id } });
        Assert.Equal(new[] { w.Sur.Id }, dto.AllowedLocationIds);
        Assert.Equal(1, await db.UserClientLocations.CountAsync());
    }

    [Fact]
    public async Task Asignar_Null_QuitaLaRestriccion_YLimpiaLasFilas()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);
        await service.SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id } });

        var dto = await service.SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = null });

        Assert.False(dto.RestrictedToLocations);
        Assert.Empty(dto.AllowedLocationIds);
        Assert.Empty(db.UserClientLocations);
    }

    [Fact]
    public async Task Asignar_ListaVacia_SeRechaza_NoSeConvierteEnTodas()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(db).SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = Array.Empty<Guid>() }));
    }

    [Fact]
    public async Task Asignar_UnaSedeDeOtraEmpresa_SeRechaza()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(db).SetClientLocationsAsync(w.ClientUser.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id, w.Ajena.Id } }));
        Assert.Empty(db.UserClientLocations);
    }

    [Fact]
    public async Task Asignar_ASoloUsuariosCliente()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(db).SetClientLocationsAsync(w.Tecnico.Id, new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id } }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).SetClientLocationsAsync(Guid.NewGuid(), new SetUserClientLocationsRequest { AllowedLocationIds = new[] { w.Norte.Id } }));
    }

    [Fact]
    public async Task Validador_AllowedLocationIds_SoloParaClienteYDeSuEmpresa()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var validator = new CreateUserRequestValidator(db);

        Assert.True((await validator.ValidateAsync(NewClientUser(w, new[] { w.Norte.Id }))).IsValid);
        Assert.True((await validator.ValidateAsync(NewClientUser(w, null))).IsValid);

        var vacia = await validator.ValidateAsync(NewClientUser(w, Array.Empty<Guid>()));
        Assert.Contains(vacia.Errors, e => e.PropertyName == nameof(CreateUserRequest.AllowedLocationIds));

        var ajena = await validator.ValidateAsync(NewClientUser(w, new[] { w.Ajena.Id }));
        Assert.Contains(ajena.Errors, e => e.ErrorMessage.Contains("no pertenece a la empresa"));

        var tecnico = NewClientUser(w, new[] { w.Norte.Id });
        tecnico.RoleName = RoleNames.Tecnico;
        tecnico.ClientId = null;
        Assert.Contains((await validator.ValidateAsync(tecnico)).Errors, e => e.ErrorMessage.Contains("solo aplica cuando RoleName es Cliente"));
    }
}
