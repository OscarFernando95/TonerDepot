using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Inventory;

// Kit base: se define por marca; cada modelo lo hereda y lo ajusta (excluir, cambiar grupo/cantidad, agregar).
public class BaseKitServiceTests
{
    private sealed record Setup(string DbName, Guid BrandId, Guid ModelAId, Guid ModelBId, Guid Fusor, Guid Presor, Guid Cilindro, Guid Extra);

    private static async Task<Setup> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand("Ricoh");
        var modelA = TestEntities.AssetModel(brand);
        var modelB = TestEntities.AssetModel(brand);
        modelB.Name = "Otro modelo";
        var items = new[] { "Fusor", "Presor", "Cilindro", "Empaque" }
            .Select(n => new InventoryItem { Name = n, Category = InventoryCategory.ConsumibleBase }).ToArray();
        db.AddRange(brand, modelA, modelB);
        db.AddRange(items);
        await db.SaveChangesAsync();
        return new Setup(dbName, brand.Id, modelA.Id, modelB.Id, items[0].Id, items[1].Id, items[2].Id, items[3].Id);
    }

    private static BaseKitService Service(Infrastructure.Persistence.TonerDbContext db) => new(db);

    private static SetBrandKitRequest BrandKit(Setup s) => new()
    {
        Items = new[]
        {
            new BaseKitItemRequest { ItemId = s.Fusor, GroupName = "Unidad fusora", Quantity = 1 },
            new BaseKitItemRequest { ItemId = s.Presor, GroupName = "Unidad fusora", Quantity = 1 },
            new BaseKitItemRequest { ItemId = s.Cilindro, GroupName = "Unidad de imagen", Quantity = 1 }
        }
    };

    [Fact]
    public async Task KitDeLaMarca_SeReemplazaComoConjunto()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);

        var kit = await service.SetBrandKitAsync(s.BrandId, BrandKit(s));
        Assert.Equal(3, kit.Count);
        Assert.Equal(new[] { "Unidad de imagen", "Unidad fusora", "Unidad fusora" }, kit.Select(k => k.GroupName));

        // Quita el presor, cambia la cantidad del fusor.
        kit = await service.SetBrandKitAsync(s.BrandId, new SetBrandKitRequest
        {
            Items = new[]
            {
                new BaseKitItemRequest { ItemId = s.Fusor, GroupName = "Unidad fusora", Quantity = 2 },
                new BaseKitItemRequest { ItemId = s.Cilindro, GroupName = "Unidad de imagen", Quantity = 1 }
            }
        });
        Assert.Equal(2, kit.Count);
        Assert.Equal(2, kit.Single(k => k.ItemId == s.Fusor).Quantity);
    }

    [Fact]
    public async Task UnModeloSinAjustes_HeredaElKitDeLaMarcaTalCual()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.SetBrandKitAsync(s.BrandId, BrandKit(s));

        var kit = await service.GetModelKitAsync(s.ModelAId);

        Assert.Equal(3, kit.Count);
        Assert.All(kit, k => { Assert.Equal("Marca", k.Source); Assert.False(k.Excluded); });
    }

    [Fact]
    public async Task UnModelo_PuedeExcluirAjustarYAgregar_SinAfectarALosDemas()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.SetBrandKitAsync(s.BrandId, BrandKit(s));

        var kit = await service.SetModelKitAsync(s.ModelAId, new SetModelKitRequest
        {
            Overrides = new[]
            {
                new ModelKitOverrideRequest { ItemId = s.Presor, Excluded = true },
                new ModelKitOverrideRequest { ItemId = s.Fusor, Quantity = 2 },
                new ModelKitOverrideRequest { ItemId = s.Extra, GroupName = "Unidad fusora", Quantity = 1 }
            }
        });

        Assert.True(kit.Single(k => k.ItemId == s.Presor).Excluded);
        var fusor = kit.Single(k => k.ItemId == s.Fusor);
        Assert.Equal(2, fusor.Quantity);
        Assert.Equal("Modelo", fusor.Source);
        Assert.Equal("Unidad fusora", fusor.GroupName);   // conserva el grupo heredado
        var extra = kit.Single(k => k.ItemId == s.Extra);
        Assert.Equal("Modelo", extra.Source);

        // El otro modelo de la marca no se entera.
        var other = await service.GetModelKitAsync(s.ModelBId);
        Assert.Equal(3, other.Count);
        Assert.All(other, k => Assert.Equal("Marca", k.Source));
    }

    [Fact]
    public async Task SiLaMarcaCambiaSuKit_ElModeloLoHeredaDeNuevo_ConsusAjustes()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.SetBrandKitAsync(s.BrandId, BrandKit(s));
        await service.SetModelKitAsync(s.ModelAId, new SetModelKitRequest { Overrides = new[] { new ModelKitOverrideRequest { ItemId = s.Presor, Excluded = true } } });

        // La marca agrega el empaque: el modelo lo recibe; el presor sigue excluido en ese modelo.
        await service.SetBrandKitAsync(s.BrandId, new SetBrandKitRequest
        {
            Items = BrandKit(s).Items.Append(new BaseKitItemRequest { ItemId = s.Extra, GroupName = "Unidad de imagen", Quantity = 1 }).ToList()
        });

        var kit = await service.GetModelKitAsync(s.ModelAId);
        Assert.Equal(4, kit.Count);
        Assert.True(kit.Single(k => k.ItemId == s.Presor).Excluded);
        Assert.False(kit.Single(k => k.ItemId == s.Extra).Excluded);
    }

    [Fact]
    public async Task ReglasDeAjuste_NoSePuedeExcluirLoQueNoEstaYAgregarExigeGrupo()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.SetBrandKitAsync(s.BrandId, BrandKit(s));

        await Assert.ThrowsAsync<ConflictException>(() => service.SetModelKitAsync(s.ModelAId,
            new SetModelKitRequest { Overrides = new[] { new ModelKitOverrideRequest { ItemId = s.Extra, Excluded = true } } }));
        await Assert.ThrowsAsync<ConflictException>(() => service.SetModelKitAsync(s.ModelAId,
            new SetModelKitRequest { Overrides = new[] { new ModelKitOverrideRequest { ItemId = s.Extra } } }));
    }

    [Fact]
    public async Task MarcaModeloOItemInexistente_DaNotFound()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetBrandKitAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetModelKitAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetBrandKitAsync(s.BrandId,
            new SetBrandKitRequest { Items = new[] { new BaseKitItemRequest { ItemId = Guid.NewGuid(), GroupName = "G" } } }));
    }
}
