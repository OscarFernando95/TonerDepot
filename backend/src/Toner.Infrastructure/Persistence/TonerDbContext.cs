using Microsoft.EntityFrameworkCore;
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

    public DbSet<City> Cities => Set<City>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientLocation> ClientLocations => Set<ClientLocation>();

    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<TechnicianCoverage> TechnicianCoverages => Set<TechnicianCoverage>();
    public DbSet<TechnicianAvailability> TechnicianAvailabilities => Set<TechnicianAvailability>();

    public DbSet<AssetBrand> AssetBrands => Set<AssetBrand>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TonerDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        TouchUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
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
}
