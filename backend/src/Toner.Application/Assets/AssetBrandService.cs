using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Assets;

public class AssetBrandService : IAssetBrandService
{
    private readonly IApplicationDbContext _db;

    public AssetBrandService(IApplicationDbContext db)
    {
        _db = db;
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

        return new AssetBrandDto { Id = brand.Id, Name = brand.Name };
    }

    public async Task<IReadOnlyList<AssetBrandDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.AssetBrands
            .OrderBy(b => b.Name)
            .Select(b => new AssetBrandDto { Id = b.Id, Name = b.Name })
            .ToListAsync(cancellationToken);
    }
}
