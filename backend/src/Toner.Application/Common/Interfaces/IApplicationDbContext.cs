using Microsoft.EntityFrameworkCore;
using Toner.Domain.Entities;

namespace Toner.Application.Common.Interfaces;

// Abstrae el DbContext para que Application no dependa de Npgsql/EF Core Design.
// Se amplía con más DbSet a medida que cada módulo del MVP los necesita.
public interface IApplicationDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<City> Cities { get; }
    DbSet<Client> Clients { get; }
    DbSet<ClientLocation> ClientLocations { get; }
    DbSet<Technician> Technicians { get; }
    DbSet<TechnicianCoverage> TechnicianCoverages { get; }
    DbSet<AssetBrand> AssetBrands { get; }
    DbSet<Asset> Assets { get; }
    DbSet<AssetStatusLog> AssetStatusLogs { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<ContractAsset> ContractAssets { get; }
    DbSet<MeterReading> MeterReadings { get; }
    DbSet<MaintenanceSchedule> MaintenanceSchedules { get; }
    DbSet<MaintenanceOrder> MaintenanceOrders { get; }
    DbSet<ServiceTicket> ServiceTickets { get; }
    DbSet<AssignmentHistory> AssignmentHistories { get; }
    DbSet<TimeLog> TimeLogs { get; }
    DbSet<TechnicianAvailability> TechnicianAvailabilities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
