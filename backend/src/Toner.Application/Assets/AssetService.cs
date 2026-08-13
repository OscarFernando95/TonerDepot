using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Assets;

public class AssetService : IAssetService
{
    // Transiciones válidas del ciclo de vida. DadoDeBaja es terminal: ninguna transición sale de ahí.
    private static readonly Dictionary<AssetLifecycleStatus, AssetLifecycleStatus[]> AllowedTransitions = new()
    {
        [AssetLifecycleStatus.EnBodega] = new[] { AssetLifecycleStatus.Instalado, AssetLifecycleStatus.DadoDeBaja },
        [AssetLifecycleStatus.Instalado] = new[]
        {
            AssetLifecycleStatus.EnMantenimiento, AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.DadoDeBaja
        },
        [AssetLifecycleStatus.EnMantenimiento] = new[]
        {
            AssetLifecycleStatus.Instalado, AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.DadoDeBaja
        },
        [AssetLifecycleStatus.DadoDeBaja] = Array.Empty<AssetLifecycleStatus>()
    };

    private readonly IApplicationDbContext _db;

    public AssetService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default)
    {
        var serialNumber = request.SerialNumber.Trim();

        var serialTaken = await _db.Assets.AnyAsync(a => a.SerialNumber == serialNumber, cancellationToken);
        if (serialTaken)
        {
            throw new ConflictException($"Ya existe un activo con número de serie '{serialNumber}'.");
        }

        var asset = new Asset
        {
            AssetBrandId = request.AssetBrandId,
            Model = request.Model.Trim(),
            SerialNumber = serialNumber,
            Type = Enum.Parse<AssetType>(request.Type),
            LifecycleStatus = AssetLifecycleStatus.EnBodega
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(asset.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default)
    {
        var query = _db.Assets.AsQueryable();

        if (!requestingUser.IsStaff)
        {
            // Un Cliente solo ve activos instalados en alguna de sus sedes ahora mismo.
            query = query.Where(a => a.CurrentClientLocation != null && a.CurrentClientLocation.Client.Id == requestingUser.ClientId);
        }

        return await ProjectedFrom(query).OrderBy(a => a.Model).ToListAsync(cancellationToken);
    }

    public async Task<AssetDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await Projected(_db).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), id);

        if (!requestingUser.IsStaff && asset.CurrentClientLocationId is null)
        {
            throw new ForbiddenException("Este activo no está instalado en ninguna de tus sedes.");
        }

        if (!requestingUser.IsStaff)
        {
            var belongsToClient = await _db.Assets
                .AnyAsync(a => a.Id == id && a.CurrentClientLocation != null && a.CurrentClientLocation.Client.Id == requestingUser.ClientId, cancellationToken);
            if (!belongsToClient)
            {
                throw new ForbiddenException("Este activo no está instalado en ninguna de tus sedes.");
            }
        }

        return asset;
    }

    public async Task<AssetDto> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), id);

        var serialNumber = request.SerialNumber.Trim();
        var serialTaken = await _db.Assets.AnyAsync(a => a.SerialNumber == serialNumber && a.Id != id, cancellationToken);
        if (serialTaken)
        {
            throw new ConflictException($"Ya existe un activo con número de serie '{serialNumber}'.");
        }

        asset.AssetBrandId = request.AssetBrandId;
        asset.Model = request.Model.Trim();
        asset.SerialNumber = serialNumber;
        asset.Type = Enum.Parse<AssetType>(request.Type);

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<AssetDto> ChangeStatusAsync(
        Guid id,
        ChangeAssetStatusRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), id);

        var newStatus = Enum.Parse<AssetLifecycleStatus>(request.NewStatus);

        if (!AllowedTransitions[asset.LifecycleStatus].Contains(newStatus))
        {
            throw new ConflictException($"No se puede pasar de '{asset.LifecycleStatus}' a '{newStatus}'.");
        }

        var previousStatus = asset.LifecycleStatus;

        if (newStatus == AssetLifecycleStatus.Instalado)
        {
            // Reinstalar tras mantenimiento conserva la sede actual si no se especifica una nueva;
            // la primera instalación (desde EnBodega) sí exige indicarla.
            var targetLocationId = request.ClientLocationId ?? asset.CurrentClientLocationId;
            if (targetLocationId is null)
            {
                throw new ConflictException("Debe indicar ClientLocationId para instalar el activo en una sede.");
            }
            asset.CurrentClientLocationId = targetLocationId;
        }
        else if (newStatus == AssetLifecycleStatus.EnBodega)
        {
            asset.CurrentClientLocationId = null;
        }

        asset.LifecycleStatus = newStatus;

        _db.AssetStatusLogs.Add(new AssetStatusLog
        {
            AssetId = asset.Id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Notes = request.Notes?.Trim()
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetStatusLogDto>> GetStatusHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        return await _db.AssetStatusLogs
            .Include(l => l.ChangedByUser)
            .Where(l => l.AssetId == id)
            .OrderByDescending(l => l.ChangedAt)
            .Select(l => new AssetStatusLogDto
            {
                Id = l.Id,
                PreviousStatus = l.PreviousStatus.ToString(),
                NewStatus = l.NewStatus.ToString(),
                ChangedAt = l.ChangedAt,
                ChangedByUserName = l.ChangedByUser != null ? l.ChangedByUser.FullName : null,
                Notes = l.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MeterReadingDto> AddMeterReadingAsync(
        Guid id,
        CreateMeterReadingRequest request,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        var lastReading = await _db.MeterReadings
            .Where(m => m.AssetId == id)
            .OrderByDescending(m => m.ReadingDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastReading is not null && request.CounterValue < lastReading.CounterValue)
        {
            throw new ConflictException(
                $"La lectura ({request.CounterValue}) no puede ser menor a la última registrada ({lastReading.CounterValue}).");
        }

        var reading = new MeterReading
        {
            AssetId = id,
            ReadingDate = request.ReadingDate ?? DateTime.UtcNow,
            CounterValue = request.CounterValue,
            RegisteredByUserId = registeredByUserId
        };

        _db.MeterReadings.Add(reading);
        await _db.SaveChangesAsync(cancellationToken);

        return await _db.MeterReadings
            .Include(m => m.RegisteredByUser)
            .Where(m => m.Id == reading.Id)
            .Select(m => new MeterReadingDto
            {
                Id = m.Id,
                AssetId = m.AssetId,
                ReadingDate = m.ReadingDate,
                CounterValue = m.CounterValue,
                RegisteredByUserName = m.RegisteredByUser != null ? m.RegisteredByUser.FullName : null
            })
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MeterReadingDto>> GetMeterReadingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        return await _db.MeterReadings
            .Include(m => m.RegisteredByUser)
            .Where(m => m.AssetId == id)
            .OrderByDescending(m => m.ReadingDate)
            .Select(m => new MeterReadingDto
            {
                Id = m.Id,
                AssetId = m.AssetId,
                ReadingDate = m.ReadingDate,
                CounterValue = m.CounterValue,
                RegisteredByUserName = m.RegisteredByUser != null ? m.RegisteredByUser.FullName : null
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<AssetDto> ToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await Projected(_db).FirstAsync(a => a.Id == id, cancellationToken);
    }

    private static IQueryable<AssetDto> Projected(IApplicationDbContext db) => ProjectedFrom(db.Assets);

    private static IQueryable<AssetDto> ProjectedFrom(IQueryable<Asset> query) =>
        query.Select(a => new AssetDto
        {
            Id = a.Id,
            AssetBrandId = a.AssetBrandId,
            AssetBrandName = a.AssetBrand.Name,
            Model = a.Model,
            SerialNumber = a.SerialNumber,
            Type = a.Type.ToString(),
            LifecycleStatus = a.LifecycleStatus.ToString(),
            CurrentClientLocationId = a.CurrentClientLocationId,
            CurrentClientLocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
            CurrentClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
            CreatedAt = a.CreatedAt
        });
}
