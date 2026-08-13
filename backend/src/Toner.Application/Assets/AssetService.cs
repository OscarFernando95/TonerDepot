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
    // PendienteInstalacion solo se alcanza desde EnBodega (vía ContractAssetService.AddAsync) — esto
    // además de reflejar la realidad operativa (no tiene sentido "despachar" un activo que ya está
    // instalado en otro lado) hace que vincular a un contrato un activo que no está en bodega falle solo
    // con la validación de esta tabla, sin necesitar un chequeo aparte en ContractAssetService.
    private static readonly Dictionary<AssetLifecycleStatus, AssetLifecycleStatus[]> AllowedTransitions = new()
    {
        [AssetLifecycleStatus.EnBodega] = new[]
        {
            AssetLifecycleStatus.Instalado, AssetLifecycleStatus.PendienteInstalacion, AssetLifecycleStatus.DadoDeBaja
        },
        [AssetLifecycleStatus.Instalado] = new[]
        {
            AssetLifecycleStatus.EnMantenimiento, AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.DadoDeBaja
        },
        [AssetLifecycleStatus.EnMantenimiento] = new[]
        {
            AssetLifecycleStatus.Instalado, AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.DadoDeBaja
        },
        [AssetLifecycleStatus.PendienteInstalacion] = new[]
        {
            AssetLifecycleStatus.Instalado, AssetLifecycleStatus.EnBodega
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

        var items = await ProjectedFrom(query).OrderBy(a => a.Model).ToListAsync(cancellationToken);

        await AttachLastMeterReadingsAsync(items, cancellationToken);

        return items;
    }

    // Se resuelve en dos pasos (en vez de una subconsulta correlacionada dentro del Select principal)
    // para que el comportamiento sea idéntico bajo Npgsql (producción) y el proveedor InMemory que usan
    // los tests — una subconsulta OrderBy().FirstOrDefault() correlacionada no siempre traduce igual
    // entre proveedores.
    private async Task AttachLastMeterReadingsAsync(IReadOnlyList<AssetDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var assetIds = items.Select(i => i.Id).ToList();
        var readings = await _db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .Select(m => new { m.AssetId, m.ReadingDate, m.CounterValue })
            .ToListAsync(cancellationToken);

        var lastByAsset = readings
            .GroupBy(r => r.AssetId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingDate).First().CounterValue);

        foreach (var item in items)
        {
            item.LastMeterReading = lastByAsset.TryGetValue(item.Id, out var value) ? value : null;
        }
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

        asset.LastMeterReading = await GetLastMeterReadingAsync(id, cancellationToken);

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
        await PrepareStatusChangeAsync(id, request, changedByUserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    // Igual que ChangeStatusAsync pero sin guardar: deja el Asset trackeado y el AssetStatusLog en cola
    // para que el caller (ContractAssetService.AddAsync) los persista en el mismo SaveChangesAsync que su
    // propio ContractAsset, logrando una sola transacción implícita en vez de dos operaciones separadas.
    public async Task<Asset> PrepareStatusChangeAsync(
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

        ApplyStatusChange(asset, request, newStatus, changedByUserId);

        return asset;
    }

    private void ApplyStatusChange(Asset asset, ChangeAssetStatusRequest request, AssetLifecycleStatus newStatus, Guid changedByUserId)
    {
        var previousStatus = asset.LifecycleStatus;

        if (newStatus == AssetLifecycleStatus.Instalado)
        {
            // Reinstalar tras mantenimiento (o confirmar una instalación que estaba Pendiente) conserva
            // la sede ya asignada si no se especifica una nueva; la primera instalación directa desde
            // EnBodega sí exige indicarla.
            var targetLocationId = request.ClientLocationId ?? asset.CurrentClientLocationId;
            if (targetLocationId is null)
            {
                throw new ConflictException("Debe indicar ClientLocationId para instalar el activo en una sede.");
            }

            if (string.IsNullOrWhiteSpace(request.Area))
            {
                throw new ConflictException("El área es obligatoria al instalar el activo.");
            }

            asset.CurrentClientLocationId = targetLocationId;
            asset.Area = request.Area.Trim();
        }
        else if (newStatus == AssetLifecycleStatus.PendienteInstalacion)
        {
            // A diferencia de Instalado, la sede acá SIEMPRE es la nueva indicada (nunca se reusa la
            // actual) — es un destino distinto, no una reinstalación en el mismo lugar.
            if (request.ClientLocationId is null)
            {
                throw new ConflictException("Debe indicar ClientLocationId para mover el activo a Pendiente de instalar.");
            }

            asset.CurrentClientLocationId = request.ClientLocationId;
            asset.Area = null;
        }
        else if (newStatus == AssetLifecycleStatus.EnBodega)
        {
            asset.CurrentClientLocationId = null;
            asset.Area = null;
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
        var dto = await Projected(_db).FirstAsync(a => a.Id == id, cancellationToken);
        dto.LastMeterReading = await GetLastMeterReadingAsync(id, cancellationToken);
        return dto;
    }

    private Task<long?> GetLastMeterReadingAsync(Guid assetId, CancellationToken cancellationToken) =>
        _db.MeterReadings
            .Where(m => m.AssetId == assetId)
            .OrderByDescending(m => m.ReadingDate)
            .Select(m => (long?)m.CounterValue)
            .FirstOrDefaultAsync(cancellationToken);

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
            Area = a.Area,
            CurrentClientLocationId = a.CurrentClientLocationId,
            CurrentClientLocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
            CurrentClientId = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Id : (Guid?)null,
            CurrentClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
            CityName = a.CurrentClientLocation != null ? a.CurrentClientLocation.City.Name : null,
            CreatedAt = a.CreatedAt
        });
}
