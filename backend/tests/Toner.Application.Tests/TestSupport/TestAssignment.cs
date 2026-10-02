using Microsoft.Extensions.Options;
using Toner.Application.Assignment;
using Toner.Application.Calendar;
using Toner.Application.Assets;
using Toner.Application.Common.Interfaces;
using Toner.Application.Evidences;
using Toner.Application.Geo;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Tickets;

namespace Toner.Application.Tests.TestSupport;

public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    // Miércoles 2026-09-30 10:00 hora de Bogotá (15:00Z): día hábil y dentro del horario por defecto.
    public static FixedTimeProvider WorkingHours { get; } = new(new DateTimeOffset(2026, 9, 30, 15, 0, 0, TimeSpan.Zero));

    // Sábado 2026-10-03 10:00 hora de Bogotá.
    public static FixedTimeProvider Weekend { get; } = new(new DateTimeOffset(2026, 10, 3, 15, 0, 0, TimeSpan.Zero));
}

public static class TestAssignment
{
    public static AssignmentEngine Create(IApplicationDbContext db, TimeProvider? time = null) =>
        new(db, new WorkCalendarService(db, Options.Create(new WorkCalendarOptions())), time ?? FixedTimeProvider.WorkingHours);
}

public static class TestCalendar
{
    public static WorkCalendarService For(IApplicationDbContext db) =>
        new(db, Options.Create(new WorkCalendarOptions()));
}

public static class TestCheckIn
{
    // requirePhotos = false por defecto: los tests históricos del check-in no cubren la política de fotos;
    // los de la política (EvidenceAndLocationTests) la activan explícitamente.
    public static TechnicianCheckInService Create(
        Infrastructure.Persistence.TonerDbContext db, bool requirePhotos = false, double radiusMeters = 250, TimeProvider? time = null)
    {
        var scheduleEngine = new MaintenanceScheduleEngine(db);
        var assignmentEngine = TestAssignment.Create(db);
        return new(
            db,
            new ServiceTicketService(db, assignmentEngine),
            new MaintenanceOrderService(db, scheduleEngine, assignmentEngine),
            new AssetService(db, scheduleEngine, assignmentEngine),
            scheduleEngine,
            assignmentEngine,
            Options.Create(new GeoOptions { SiteRadiusMeters = radiusMeters }),
            Options.Create(new EvidenceOptions { RequirePhotos = requirePhotos }),
            TestCalendar.For(db),
            time ?? FixedTimeProvider.WorkingHours,
            new Toner.Application.Inventory.InventoryConsumptionService(db, new Toner.Application.Inventory.BaseKitService(db)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TechnicianCheckInService>.Instance);
    }
}
