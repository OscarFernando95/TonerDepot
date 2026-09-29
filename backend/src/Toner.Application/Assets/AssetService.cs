using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assignment;
using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Domain.Common;
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
    private readonly IMaintenanceScheduleEngine _scheduleEngine;
    private readonly IAssignmentEngine _assignmentEngine;

    public AssetService(IApplicationDbContext db, IMaintenanceScheduleEngine scheduleEngine, IAssignmentEngine assignmentEngine)
    {
        _db = db;
        _scheduleEngine = scheduleEngine;
        _assignmentEngine = assignmentEngine;
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
            AssetModelId = request.AssetModelId,
            SerialNumber = serialNumber,
            Type = EnumParsing.ParseOrThrow<AssetType>(request.Type, nameof(request.Type)),
            LifecycleStatus = AssetLifecycleStatus.EnBodega
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(asset.Id, cancellationToken);
    }

    public async Task<PagedResult<AssetDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.Assets.AsQueryable();

        if (!requestingUser.IsStaff)
        {
            // Un Cliente solo ve activos instalados en alguna de sus sedes ahora mismo.
            var clientId = requestingUser.RequireClientId();
            query = query.Where(a => a.CurrentClientLocation != null && a.CurrentClientLocation.Client.Id == clientId);
        }

        // La paginación va ANTES de adjuntar las últimas lecturas: así el trabajo extra se hace solo
        // sobre la página, no sobre el listado completo.
        var result = await ProjectedFrom(query)
            .OrderBy(a => a.AssetBrandName).ThenBy(a => a.Model)
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        await AttachLastMeterReadingsAsync(result.Items, cancellationToken);

        return result;
    }

    private async Task AttachLastMeterReadingsAsync(IReadOnlyList<AssetDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var assetIds = items.Select(i => i.Id).ToList();
        var lastByAsset = await MeterReadingQueries.GetLastReadingsByAssetAsync(_db, assetIds, cancellationToken);

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
            var clientId = requestingUser.RequireClientId();
            // El DTO ya proyectado trae CurrentClientId — evita repetir la consulta que Projected()
            // acaba de resolver (CODE_QUALITY_AUDIT.md hallazgo #26).
            if (asset.CurrentClientId != clientId)
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

        asset.AssetModelId = request.AssetModelId;
        asset.SerialNumber = serialNumber;
        asset.Type = EnumParsing.ParseOrThrow<AssetType>(request.Type, nameof(request.Type));

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

        var newStatus = EnumParsing.ParseOrThrow<AssetLifecycleStatus>(request.NewStatus, nameof(request.NewStatus));

        if (!AllowedTransitions[asset.LifecycleStatus].Contains(newStatus))
        {
            throw new ConflictException($"No se puede pasar de '{asset.LifecycleStatus}' a '{newStatus}'.");
        }

        ApplyStatusChange(asset, request, newStatus, changedByUserId);

        if (newStatus == AssetLifecycleStatus.EnBodega)
        {
            // Invariante: un activo EnBodega nunca puede tener un vínculo de contrato activo. Volver a
            // bodega (desde Instalado o EnMantenimiento) cierra automáticamente cualquier ContractAsset
            // que siga abierto, en vez de dejarlo huérfano a la espera de que alguien lo cierre a mano.
            var activeLinks = await _db.ContractAssets
                .Where(ca => ca.AssetId == asset.Id && ca.EndDate == null)
                .ToListAsync(cancellationToken);

            foreach (var link in activeLinks)
            {
                link.EndDate = DateTime.UtcNow;
            }

            // Un activo que ya no está instalado bajo ningún contrato no debe seguir generando órdenes de
            // mantenimiento — se pausa su cronograma (si tenía uno). Se reactiva solo al reinstalarse
            // (MaintenanceScheduleEngine.UpsertForInstallationAsync lo reactiva explícitamente).
            var schedule = await _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.AssetId == asset.Id, cancellationToken);
            if (schedule is not null)
            {
                schedule.IsActive = false;
            }
        }

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
            // Captura al escribir DESPUÉS de la mutación de arriba: refleja a quién pertenece el activo
            // como RESULTADO de la transición. En una vuelta a bodega queda NULL y ningún cliente ve el
            // log — coherente, porque en ese mismo momento el activo también sale de su vista (fase 3b).
            ClientId = asset.ClientId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Notes = request.Notes?.Trim()
        });
    }

    public async Task<PagedResult<AssetStatusLogDto>> GetStatusHistoryAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        var query = _db.AssetStatusLogs.Where(l => l.AssetId == id);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(l => l.ChangedAt < ts || (l.ChangedAt == ts && l.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(l => l.ChangedAt).ThenByDescending(l => l.Id)
            .Select(l => new AssetStatusLogDto
            {
                Id = l.Id,
                PreviousStatus = l.PreviousStatus.ToString(),
                NewStatus = l.NewStatus.ToString(),
                ChangedAt = l.ChangedAt,
                ChangedByUserName = l.ChangedByUser != null ? l.ChangedByUser.FullName : null,
                Notes = l.Notes
            })
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.ChangedAt, last.Id), cancellationToken);
    }

    public async Task<MeterReadingDto> AddMeterReadingAsync(
        Guid id,
        CreateMeterReadingRequest request,
        RequestingUser requestingUser,
        CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        var registeredByUserId = requestingUser.UserId;

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
            // Captura al escribir desde el activo. Nullable: hoy se puede registrar una lectura de un
            // activo en bodega (este método solo comprueba que exista, no su LifecycleStatus), y esa
            // lectura no pertenece a ningún cliente — fail-closed (fase 3b).
            ClientId = await _db.Assets.Where(a => a.Id == id).Select(a => a.ClientId).FirstOrDefaultAsync(cancellationToken),
            ReadingDate = request.ReadingDate ?? DateTime.UtcNow,
            CounterValue = request.CounterValue,
            RegisteredByUserId = registeredByUserId
        };

        _db.MeterReadings.Add(reading);

        var order = await _scheduleEngine.EvaluateAsync(id, request.CounterValue, reading.ReadingDate, cancellationToken);

        // La asignación corre ANTES del SaveChanges, sobre la orden todavía sin guardar: lectura,
        // orden y asignación caen en un único commit. Antes eran dos, y si el segundo fallaba
        // quedaba una orden Pendiente huérfana que además bloquea para siempre la generación de
        // órdenes de ese cronograma — EvaluateAllDueAsync salta los que ya tienen orden abierta
        // (CODE_QUALITY_AUDIT.md hallazgo #8).
        if (order is not null)
        {
            await _assignmentEngine.AssignMaintenanceOrderAsync(order, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await _db.MeterReadings
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

    public async Task<PagedResult<MeterReadingDto>> GetMeterReadingsAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == id, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), id);
        }

        var query = _db.MeterReadings.Where(m => m.AssetId == id);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(m => m.ReadingDate < ts || (m.ReadingDate == ts && m.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(m => m.ReadingDate).ThenByDescending(m => m.Id)
            .Select(m => new MeterReadingDto
            {
                Id = m.Id,
                AssetId = m.AssetId,
                ReadingDate = m.ReadingDate,
                CounterValue = m.CounterValue,
                RegisteredByUserName = m.RegisteredByUser != null ? m.RegisteredByUser.FullName : null
            })
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.ReadingDate, last.Id), cancellationToken);
    }

    public async Task<PagedResult<PendingInstallationDto>> ListPendingInstallationsAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var coveredCityIds = await _db.TechnicianCoverages
            .Where(c => c.TechnicianId == technicianId)
            .Select(c => c.CityId)
            .ToListAsync(cancellationToken);

        if (coveredCityIds.Count == 0)
        {
            return PagedResultFactory.Empty<PendingInstallationDto>(pageSize);
        }

        var result = await _db.Assets
            .Where(a => a.LifecycleStatus == AssetLifecycleStatus.PendienteInstalacion
                && a.CurrentClientLocation != null
                && coveredCityIds.Contains(a.CurrentClientLocation.CityId))
            .Select(a => new PendingInstallationDto
            {
                AssetId = a.Id,
                AssetBrandName = a.AssetModel.AssetBrand.Name,
                Model = a.AssetModel.Name,
                SerialNumber = a.SerialNumber,
                ClientId = a.CurrentClientLocation!.Client.Id,
                ClientName = a.CurrentClientLocation!.Client.Name,
                ClientLocationId = a.CurrentClientLocation!.Id,
                ClientLocationName = a.CurrentClientLocation!.Name,
                CityName = a.CurrentClientLocation!.City.Name
            })
            .OrderBy(p => p.ClientName)
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        var pending = result.Items;
        if (pending.Count == 0)
        {
            return result;
        }

        var assetIds = pending.Select(p => p.AssetId).ToList();
        var activeContractLinks = await _db.ContractAssets
            .Where(ca => assetIds.Contains(ca.AssetId) && ca.EndDate == null)
            .Select(ca => new { ca.AssetId, ca.ContractId })
            .ToListAsync(cancellationToken);

        var contractByAsset = activeContractLinks.ToDictionary(l => l.AssetId, l => l.ContractId);

        var contractIds = activeContractLinks.Select(l => l.ContractId).Distinct().ToList();
        var contractDatesById = await _db.Contracts
            .Where(c => contractIds.Contains(c.Id))
            .Select(c => new { c.Id, c.StartDate, c.EndDate })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // Un check-in abierto de CUALQUIER técnico (no solo el que consulta) marca el activo como tomado —
        // evita que un segundo técnico intente el check-in y solo se entere del conflicto al fallar.
        var takenAssetIds = await _db.TimeLogs
            .Where(tl => tl.AssetId != null && assetIds.Contains(tl.AssetId!.Value) && tl.EndTime == null)
            .Select(tl => tl.AssetId!.Value)
            .ToListAsync(cancellationToken);
        var takenSet = takenAssetIds.ToHashSet();

        foreach (var item in pending)
        {
            item.ContractId = contractByAsset.TryGetValue(item.AssetId, out var contractId) ? contractId : null;
            if (item.ContractId.HasValue && contractDatesById.TryGetValue(item.ContractId.Value, out var contractDates))
            {
                item.ContractStartDate = contractDates.StartDate;
                item.ContractEndDate = contractDates.EndDate;
            }
            item.TakenByAnotherTechnician = takenSet.Contains(item.AssetId);
        }

        return result;
    }

    public async Task<PagedResult<MeterReadingAssetDto>> ListForMeterReadingAsync(
        RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.Assets.Where(a => a.LifecycleStatus == AssetLifecycleStatus.Instalado);

        // Admin/Coordinador ven todos los activos instalados. Un Técnico solo ve los que un
        // administrador le vinculó explícitamente (TechnicianAsset) — decisión de arquitectura del
        // 2026-09-27: la cobertura por ciudad (TechnicianCoverage) no otorga visibilidad de activos
        // por sí sola, así que un técnico recién asignado a una ciudad no ve nada hasta que se le
        // vinculen sus activos. Para el caso "el técnico titular no está disponible" existe una lista
        // de respaldo aparte, ver ListForMeterReadingByCoverageAsync — nunca se fusionan.
        if (requestingUser.IsTechnician)
        {
            var technicianId = requestingUser.TechnicianId
                ?? throw new ForbiddenException("Tu usuario no tiene un técnico asociado.");

            query = query.Where(a => a.TechnicianAssets.Any(ta => ta.TechnicianId == technicianId));
        }

        return await ProjectMeterReadingAssetsAsync(query, page, pageSize, cancellationToken);
    }

    // Respaldo cuando el técnico titular de un activo no está disponible (vacaciones, incapacidad,
    // renuncia/despido): además de sus activos vinculados explícitamente (ListForMeterReadingAsync),
    // un técnico puede ver y registrar lecturas de TODOS los activos instalados en las ciudades de su
    // cobertura (TechnicianCoverage), estén o no vinculados a él o a otro técnico — decisión de
    // producto 2026-09-28. La vinculación explícita sigue siendo la lista "principal"; esta es una
    // pestaña de respaldo separada en la app, nunca se fusionan las dos. Solo tiene sentido para
    // Técnico — Admin/Coordinador ya ven todo en ListForMeterReadingAsync.
    public async Task<PagedResult<MeterReadingAssetDto>> ListForMeterReadingByCoverageAsync(
        RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var technicianId = requestingUser.TechnicianId
            ?? throw new ForbiddenException("Tu usuario no tiene un técnico asociado.");

        var query = _db.Assets.Where(a =>
            a.LifecycleStatus == AssetLifecycleStatus.Instalado &&
            a.CurrentClientLocation != null &&
            a.CurrentClientLocation.City.TechnicianCoverages.Any(tc => tc.TechnicianId == technicianId));

        return await ProjectMeterReadingAssetsAsync(query, page, pageSize, cancellationToken);
    }

    // Compartido por ListForMeterReadingAsync y ListForMeterReadingByCoverageAsync — solo cambia el
    // filtro previo (por vínculo explícito vs. por cobertura de ciudad), la proyección y el adjunto de
    // última lectura son idénticos.
    private async Task<PagedResult<MeterReadingAssetDto>> ProjectMeterReadingAssetsAsync(
        IQueryable<Asset> query, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var result = await query
            .Select(a => new MeterReadingAssetDto
            {
                AssetId = a.Id,
                AssetBrandName = a.AssetModel.AssetBrand.Name,
                Model = a.AssetModel.Name,
                SerialNumber = a.SerialNumber,
                ClientId = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Id : (Guid?)null,
                ClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
                ClientLocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
                Area = a.Area,
                CityName = a.CurrentClientLocation != null ? a.CurrentClientLocation.City.Name : null
            })
            .OrderBy(a => a.ClientName)
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        if (result.Items.Count > 0)
        {
            var assetIds = result.Items.Select(i => i.AssetId).ToList();
            var lastByAsset = await MeterReadingQueries.GetLastReadingsByAssetAsync(_db, assetIds, cancellationToken);

            foreach (var item in result.Items)
            {
                item.LastMeterReading = lastByAsset.TryGetValue(item.AssetId, out var value) ? value : null;
            }
        }

        return result;
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
            AssetModelId = a.AssetModelId,
            AssetBrandName = a.AssetModel.AssetBrand.Name,
            Model = a.AssetModel.Name,
            SerialNumber = a.SerialNumber,
            Type = a.Type.ToString(),
            LifecycleStatus = a.LifecycleStatus.ToString(),
            Area = a.Area,
            CurrentClientLocationId = a.CurrentClientLocationId,
            CurrentClientLocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
            CurrentClientId = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Id : (Guid?)null,
            CurrentClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
            CityName = a.CurrentClientLocation != null ? a.CurrentClientLocation.City.Name : null,
            // A lo sumo un ContractAsset activo por activo (invariante que ya valida ContractAssetService.AddAsync),
            // así que este FirstOrDefault no depende de ningún orden — no hay ambigüedad de cuál "primero" elegir.
            ActiveContractId = a.ContractAssets.Where(ca => ca.EndDate == null).Select(ca => (Guid?)ca.ContractId).FirstOrDefault(),
            CreatedAt = a.CreatedAt
        });
}
