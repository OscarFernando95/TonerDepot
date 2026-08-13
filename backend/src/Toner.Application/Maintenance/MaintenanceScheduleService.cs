using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance;

public class MaintenanceScheduleService : IMaintenanceScheduleService
{
    private readonly IApplicationDbContext _db;

    public MaintenanceScheduleService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MaintenanceScheduleDto> CreateAsync(CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var frequencyType = Enum.Parse<MaintenanceFrequencyType>(request.FrequencyType);
        var now = DateTime.UtcNow;

        var schedule = new MaintenanceSchedule
        {
            AssetId = request.AssetId,
            ContractId = request.ContractId,
            FrequencyType = frequencyType,
            PrintThreshold = request.PrintThreshold,
            TimeIntervalDays = request.TimeIntervalDays,
            IsActive = true
        };

        if (frequencyType == MaintenanceFrequencyType.PorTiempo)
        {
            schedule.NextDueAt = now.AddDays(request.TimeIntervalDays!.Value);
        }
        else
        {
            var lastReading = await _db.MeterReadings
                .Where(m => m.AssetId == request.AssetId)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync(cancellationToken);

            schedule.NextDueCounter = (lastReading?.CounterValue ?? 0) + request.PrintThreshold!.Value;
        }

        _db.MaintenanceSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(schedule.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceScheduleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Projected(_db).OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<MaintenanceScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Projected(_db).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);
    }

    public async Task<MaintenanceScheduleDto> UpdateAsync(Guid id, UpdateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);

        if (schedule.FrequencyType == MaintenanceFrequencyType.PorContador)
        {
            if (!request.PrintThreshold.HasValue)
            {
                throw new ConflictException("PrintThreshold es obligatorio para un cronograma PorContador.");
            }

            schedule.PrintThreshold = request.PrintThreshold;
            schedule.NextDueCounter = (schedule.LastExecutedCounter ?? 0) + request.PrintThreshold.Value;
        }
        else
        {
            if (!request.TimeIntervalDays.HasValue)
            {
                throw new ConflictException("TimeIntervalDays es obligatorio para un cronograma PorTiempo.");
            }

            schedule.TimeIntervalDays = request.TimeIntervalDays;
            schedule.NextDueAt = (schedule.LastExecutedAt ?? DateTime.UtcNow).AddDays(request.TimeIntervalDays.Value);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<MaintenanceScheduleDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);

        schedule.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    private async Task<MaintenanceScheduleDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(s => s.Id == id, cancellationToken);

    private static IQueryable<MaintenanceScheduleDto> Projected(IApplicationDbContext db) =>
        db.MaintenanceSchedules.Select(s => new MaintenanceScheduleDto
        {
            Id = s.Id,
            AssetId = s.AssetId,
            AssetBrandName = s.Asset.AssetBrand.Name,
            AssetModel = s.Asset.Model,
            AssetSerialNumber = s.Asset.SerialNumber,
            ContractId = s.ContractId,
            FrequencyType = s.FrequencyType.ToString(),
            PrintThreshold = s.PrintThreshold,
            TimeIntervalDays = s.TimeIntervalDays,
            LastExecutedAt = s.LastExecutedAt,
            LastExecutedCounter = s.LastExecutedCounter,
            NextDueAt = s.NextDueAt,
            NextDueCounter = s.NextDueCounter,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        });
}
