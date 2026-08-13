using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.TestSupport;

// Fábricas mínimas de entidades para armar el grafo que cada test necesita, sin repetir
// los campos obligatorios de cada entidad en cada archivo de test.
public static class TestEntities
{
    public static City City(string name = "Bogotá", string stateOrProvince = "Cundinamarca") =>
        new() { Name = name, StateOrProvince = stateOrProvince };

    public static AssetBrand AssetBrand(string name = "Ricoh") => new() { Name = name };

    public static Client Client(string name = "Cliente Test") => new() { Name = name };

    public static ClientLocation ClientLocation(Client client, City city, string name = "Sede Principal") => new()
    {
        ClientId = client.Id,
        CityId = city.Id,
        Name = name,
        Address = "Calle 123"
    };

    public static Contract Contract(Client client, ContractStatus status = ContractStatus.Activo) => new()
    {
        ClientId = client.Id,
        StartDate = DateTime.UtcNow,
        Status = status
    };

    public static Role Role(string name) => new() { Name = name };

    public static User User(Role role, string? email = null, Guid? clientId = null, string? cedula = null) => new()
    {
        Cedula = cedula ?? Guid.NewGuid().ToString("N")[..10],
        Email = email ?? $"{Guid.NewGuid():N}@test.local",
        PasswordHash = "hash",
        FullName = "Usuario Test",
        RoleId = role.Id,
        ClientId = clientId
    };

    public static Technician Technician(User user, bool isActive = true, TechnicianStatus status = TechnicianStatus.Disponible) => new()
    {
        UserId = user.Id,
        IsActive = isActive,
        Status = status
    };

    public static TechnicianCoverage Coverage(Technician technician, City city) => new()
    {
        TechnicianId = technician.Id,
        CityId = city.Id
    };

    public static Asset Asset(AssetBrand brand, AssetLifecycleStatus status = AssetLifecycleStatus.EnBodega, Guid? locationId = null) => new()
    {
        AssetBrandId = brand.Id,
        Model = "MP 2555",
        SerialNumber = Guid.NewGuid().ToString("N")[..10],
        Type = AssetType.Impresora,
        LifecycleStatus = status,
        CurrentClientLocationId = locationId
    };

    public static ServiceTicket ServiceTicket(
        ClientLocation location,
        User reportedBy,
        ServiceTicketStatus status = ServiceTicketStatus.Abierto,
        ServiceTicketPriority priority = ServiceTicketPriority.Media,
        Guid? technicianId = null) => new()
    {
        ClientLocationId = location.Id,
        ReportedByUserId = reportedBy.Id,
        Description = "Impresora atascada",
        Status = status,
        Priority = priority,
        TechnicianId = technicianId
    };

    public static MaintenanceSchedule MaintenanceSchedule(
        Asset asset,
        MaintenanceFrequencyType frequencyType,
        int? printThreshold = null,
        int? timeIntervalDays = null) => new()
    {
        AssetId = asset.Id,
        FrequencyType = frequencyType,
        PrintThreshold = printThreshold,
        TimeIntervalDays = timeIntervalDays
    };

    public static MaintenanceOrder MaintenanceOrder(
        MaintenanceSchedule schedule,
        Asset asset,
        MaintenanceOrderStatus status = MaintenanceOrderStatus.Pendiente,
        Guid? technicianId = null,
        DateTime? scheduledDate = null) => new()
    {
        MaintenanceScheduleId = schedule.Id,
        AssetId = asset.Id,
        Status = status,
        TechnicianId = technicianId,
        ScheduledDate = scheduledDate ?? DateTime.UtcNow
    };
}
