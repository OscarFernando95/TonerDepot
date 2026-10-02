using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Push;
using Toner.Application.Push.Dtos;
using Toner.Application.Push.Validators;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Push;

public class DeviceTokenServiceTests
{
    private static (string Db, User UserA, User UserB) Seed()
    {
        var db = Guid.NewGuid().ToString();
        using var ctx = TonerTestDb.CreateContext(db);
        var role = TestEntities.Role(RoleNames.Tecnico);
        var a = TestEntities.User(role);
        var b = TestEntities.User(role);
        ctx.AddRange(role, a, b);
        ctx.SaveChanges();
        return (db, a, b);
    }

    private static async Task Register(string db, Guid userId, string platform, string token)
    {
        using var ctx = TonerTestDb.CreateContext(db);
        await new DeviceTokenService(ctx).RegisterAsync(userId, new RegisterDeviceTokenRequest(platform, token));
    }

    private static async Task<List<(Guid UserId, PushPlatform Platform, string Token)>> All(string db)
    {
        using var ctx = TonerTestDb.CreateContext(db);
        return (await ctx.DeviceTokens.ToListAsync()).Select(d => (d.UserId, d.Platform, d.Token)).ToList();
    }

    [Fact]
    public async Task UnAndroidYUnIPhone_Conviven()
    {
        var (db, a, _) = Seed();
        await Register(db, a.Id, "Android", "tok-android");
        await Register(db, a.Id, "iOS", "tok-ios");

        Assert.Equivalent(new[]
        {
            (a.Id, PushPlatform.Android, "tok-android"),
            (a.Id, PushPlatform.iOS, "tok-ios")
        }, await All(db), strict: true);
    }

    [Fact]
    public async Task OtroAndroid_ReemplazaAlAnterior_PeroNoTocaAlIPhone()
    {
        var (db, a, _) = Seed();
        await Register(db, a.Id, "Android", "tok-viejo");
        await Register(db, a.Id, "iOS", "tok-ios");
        await Register(db, a.Id, "Android", "tok-nuevo");

        Assert.Equivalent(new[]
        {
            (a.Id, PushPlatform.Android, "tok-nuevo"),
            (a.Id, PushPlatform.iOS, "tok-ios")
        }, await All(db), strict: true);
    }

    [Fact]
    public async Task RegistrarElMismoTokenDosVeces_NoDuplica()
    {
        var (db, a, _) = Seed();
        await Register(db, a.Id, "Android", "tok");
        await Register(db, a.Id, "Android", "tok");

        Assert.Single(await All(db));
    }

    [Fact]
    public async Task ElMismoTelefonoConOtroUsuario_PasaAlNuevoYSeRetiraDelAnterior()
    {
        var (db, a, b) = Seed();
        await Register(db, a.Id, "Android", "tok-telefono");
        await Register(db, b.Id, "Android", "tok-telefono");

        Assert.Equivalent(new[] { (b.Id, PushPlatform.Android, "tok-telefono") }, await All(db), strict: true);
    }

    [Fact]
    public async Task LaPlataformaSeNormalizaSinImportarMayusculas()
    {
        var (db, a, _) = Seed();
        await Register(db, a.Id, "ANDROID", "t1");
        await Register(db, a.Id, "ios", "t2");

        Assert.Equivalent(new[] { PushPlatform.Android, PushPlatform.iOS }, (await All(db)).Select(d => d.Platform), strict: true);
    }

    [Fact]
    public async Task Desregistrar_QuitaSoloEsaPlataforma()
    {
        var (db, a, _) = Seed();
        await Register(db, a.Id, "Android", "tok-a");
        await Register(db, a.Id, "iOS", "tok-i");

        using (var ctx = TonerTestDb.CreateContext(db))
        {
            await new DeviceTokenService(ctx).UnregisterAsync(a.Id, "Android");
        }

        Assert.Equivalent(new[] { (a.Id, PushPlatform.iOS, "tok-i") }, await All(db), strict: true);
    }

    [Theory]
    [InlineData("", "tok")]
    [InlineData("Windows", "tok")]
    [InlineData("7", "tok")]
    [InlineData("Android", "")]
    public void Validador_RechazaPlataformaOTokenInvalidos(string platform, string token)
    {
        Assert.False(new RegisterDeviceTokenRequestValidator().Validate(new RegisterDeviceTokenRequest(platform, token)).IsValid);
    }

    [Fact]
    public void Validador_RechazaTokenDemasiadoLargo()
    {
        Assert.False(new RegisterDeviceTokenRequestValidator()
            .Validate(new RegisterDeviceTokenRequest("Android", new string('x', 513))).IsValid);
    }

    [Fact]
    public async Task PlataformaInvalida_EnElServicio_EsErrorDeValidacionNoExcepcionCruda()
    {
        var (db, a, _) = Seed();
        using var ctx = TonerTestDb.CreateContext(db);
        await Assert.ThrowsAsync<ValidationException>(() =>
            new DeviceTokenService(ctx).RegisterAsync(a.Id, new RegisterDeviceTokenRequest("Symbian", "tok")));
    }
}
