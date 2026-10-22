using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Domain.Entities;

namespace Toner.Application.Assets;

// Punto único para que cualquier evento que traiga un contador de la máquina (hoy: registrar un tóner) deje también la
// lectura que alimenta "último contador" en activos, órdenes, contratos y pantallas de lectura. No guarda: el caller
// lo combina con su propio SaveChangesAsync, igual que PrepareStatusChangeAsync.
public interface IMeterReadingRecorder
{
    // Devuelve null si ya existe una lectura igual vigente a esa fecha (no se duplica).
    Task<MeterReading?> PrepareAsync(
        Guid assetId, long counter, DateTime readingDate, Guid? registeredByUserId, CancellationToken cancellationToken = default);
}

public class MeterReadingRecorder : IMeterReadingRecorder
{
    private readonly IApplicationDbContext _db;
    private readonly IMaintenanceScheduleEngine _scheduleEngine;
    private readonly IAssignmentEngine _assignmentEngine;

    public MeterReadingRecorder(IApplicationDbContext db, IMaintenanceScheduleEngine scheduleEngine, IAssignmentEngine assignmentEngine)
    {
        _db = db;
        _scheduleEngine = scheduleEngine;
        _assignmentEngine = assignmentEngine;
    }

    public async Task<MeterReading?> PrepareAsync(
        Guid assetId, long counter, DateTime readingDate, Guid? registeredByUserId, CancellationToken cancellationToken = default)
    {
        var previous = await _db.MeterReadings
            .Where(m => m.AssetId == assetId && m.ReadingDate <= readingDate)
            .OrderByDescending(m => m.ReadingDate)
            .Select(m => (long?)m.CounterValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (previous.HasValue && counter < previous.Value)
        {
            throw new ConflictException(
                $"El contador ({counter}) es menor a la última lectura registrada de esta máquina ({previous.Value}). Revisa el número.");
        }

        var next = await _db.MeterReadings
            .Where(m => m.AssetId == assetId && m.ReadingDate > readingDate)
            .OrderBy(m => m.ReadingDate)
            .Select(m => (long?)m.CounterValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (next.HasValue && counter > next.Value)
        {
            throw new ConflictException(
                $"El contador ({counter}) es mayor a una lectura posterior de esta máquina ({next.Value}). Revisa el número o la fecha.");
        }

        if (previous == counter)
        {
            return null;
        }

        var reading = new MeterReading
        {
            AssetId = assetId,
            ClientId = await _db.Assets.Where(a => a.Id == assetId).Select(a => a.ClientId).FirstOrDefaultAsync(cancellationToken),
            ReadingDate = readingDate,
            CounterValue = counter,
            RegisteredByUserId = registeredByUserId
        };
        _db.MeterReadings.Add(reading);

        var order = await _scheduleEngine.EvaluateAsync(assetId, counter, readingDate, cancellationToken);
        if (order is not null)
        {
            await _assignmentEngine.AssignMaintenanceOrderAsync(order, cancellationToken);
        }

        return reading;
    }
}
