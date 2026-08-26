using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Caching;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Assets;

public class AssetBrandService : IAssetBrandService
{
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public AssetBrandService(IApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<AssetBrandDto> CreateAsync(CreateAssetBrandRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        var exists = await _db.AssetBrands.AnyAsync(b => b.Name == name, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Ya existe una marca llamada '{name}'.");
        }

        var brand = new AssetBrand { Name = name };
        _db.AssetBrands.Add(brand);
        await _db.SaveChangesAsync(cancellationToken);

        // Invalidación activa: no esperamos al TTL. Solo se tumba la lista de marcas — una marca
        // nueva no tiene modelos, así que las entradas catalog:asset-models:{brandId} siguen válidas.
        _cache.Remove(CacheKeys.AssetBrands);

        return new AssetBrandDto { Id = brand.Id, Name = brand.Name };
    }

    // Sin RLS (AssetBrands no está entre las tablas con políticas), así que una entrada compartida
    // es correcta para cualquier usuario. Alimenta los desplegables de marca del módulo de activos.
    public async Task<IReadOnlyList<AssetBrandDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKeys.AssetBrands, out IReadOnlyList<AssetBrandDto>? cached) && cached is not null)
        {
            return cached;
        }

        var brands = await _db.AssetBrands
            .OrderBy(b => b.Name)
            .Select(b => new AssetBrandDto { Id = b.Id, Name = b.Name })
            .ToListAsync(cancellationToken);

        _cache.Set(CacheKeys.AssetBrands, (IReadOnlyList<AssetBrandDto>)brands, CacheDurations.Catalog);

        return brands;
    }
}
