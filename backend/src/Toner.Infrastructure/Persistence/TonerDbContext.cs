using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence;

public class TonerDbContext : DbContext, IApplicationDbContext
{
    public TonerDbContext(DbContextOptions<TonerDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<TechnicianWorkInterval> TechnicianWorkIntervals => Set<TechnicianWorkInterval>();
    public DbSet<TechnicianTimeOff> TechnicianTimeOffs => Set<TechnicianTimeOff>();
    public DbSet<CompanyHolidayOverride> CompanyHolidayOverrides => Set<CompanyHolidayOverride>();

    public DbSet<City> Cities => Set<City>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientLocation> ClientLocations => Set<ClientLocation>();

    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<TechnicianZone> TechnicianZones => Set<TechnicianZone>();
    public DbSet<TechnicianAsset> TechnicianAssets => Set<TechnicianAsset>();
    public DbSet<TechnicianAvailability> TechnicianAvailabilities => Set<TechnicianAvailability>();

    public DbSet<AssetBrand> AssetBrands => Set<AssetBrand>();
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetStatusLog> AssetStatusLogs => Set<AssetStatusLog>();

    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractAsset> ContractAssets => Set<ContractAsset>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();

    public DbSet<ServiceTicket> ServiceTickets => Set<ServiceTicket>();
    public DbSet<AssignmentHistory> AssignmentHistories => Set<AssignmentHistory>();

    public DbSet<TimeLog> TimeLogs => Set<TimeLog>();
    public DbSet<Evidence> Evidences => Set<Evidence>();

    public DbSet<ExceptionLog> ExceptionLogs => Set<ExceptionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TonerDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        TouchUpdatedAt();
        try
        {
            return base.SaveChanges();
        }
        catch (DbUpdateException ex) when (TryGetUniqueViolationMessage(ex, out var message))
        {
            throw new ConflictException(message);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryGetUniqueViolationMessage(ex, out var message))
        {
            throw new ConflictException(message);
        }
    }

    private void TouchUpdatedAt()
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // Traduce una violación de índice único de Postgres (SqlState 23505) a la excepción de negocio
    // que Application ya entiende, en vez de dejar escapar un DbUpdateException/PostgresException
    // específico de Npgsql hacia capas que no deberían conocer el proveedor de base de datos.
    // Cubre las condiciones de carrera del patrón "verificar-y-luego-insertar" que usan los servicios
    // (dos requests concurrentes pasan el chequeo en memoria antes de que cualquiera inserte); otros
    // tipos de DbUpdateException (ej. violación de llave foránea) siguen siendo un 500 genuino.
    private static bool TryGetUniqueViolationMessage(DbUpdateException ex, out string message)
    {
        if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pgEx)
        {
            var field = pgEx.ColumnName ?? pgEx.ConstraintName ?? "un valor único";
            message = $"Ya existe un registro con ese valor en '{field}'.";
            return true;
        }

        message = string.Empty;
        return false;
    }
}
