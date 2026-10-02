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

    public static AssetModel AssetModel(
        AssetBrand brand,
        string name = "MP 2555",
        int generalPrintThreshold = 30000,
        int generalMonthsInterval = 6,
        int unitsPrintThreshold = 30000,
        int unitsMonthsInterval = 6,
        int consumablesPrintThreshold = 60000) => new()
    {
        AssetBrandId = brand.Id,
        Name = name,
        GeneralPrintThreshold = generalPrintThreshold,
        GeneralMonthsInterval = generalMonthsInterval,
        UnitsPrintThreshold = unitsPrintThreshold,
        UnitsMonthsInterval = unitsMonthsInterval,
        ConsumablesPrintThreshold = consumablesPrintThreshold
    };

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

    public static ContractAsset ContractAsset(Contract contract, Asset asset, DateTime? startDate = null, DateTime? endDate = null) => new()
    {
        ContractId = contract.Id,
        AssetId = asset.Id,
        StartDate = startDate ?? DateTime.UtcNow,
        EndDate = endDate
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

    // El técnico cubre la ciudad a través de una zona propia que contiene solo esa ciudad. La ciudad queda marcada con
    // la zona (hay que agregarla al contexto, o ya estarlo, antes de guardar); la zona viaja en la asignación.
    public static TechnicianZone Coverage(Technician technician, City city)
    {
        var zone = new Zone { Name = $"Zona {city.Name} {Guid.NewGuid():N}" };
        city.ZoneId = zone.Id;
        return new TechnicianZone { TechnicianId = technician.Id, ZoneId = zone.Id, Zone = zone };
    }

    public static Asset Asset(AssetModel model, AssetLifecycleStatus status = AssetLifecycleStatus.EnBodega, Guid? locationId = null) => new()
    {
        AssetModelId = model.Id,
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
        Contract contract,
        DateTime? nextGeneralDueAt = null,
        long nextGeneralDueCounter = 1_000_000,
        DateTime? nextUnitsDueAt = null,
        long nextUnitsDueCounter = 1_000_000,
        long nextConsumablesDueCounter = 1_000_000,
        DateTime? lastGeneralMaintenanceAt = null,
        long? lastGeneralMaintenanceCounter = null,
        DateTime? lastUnitsMaintenanceAt = null,
        long? lastUnitsMaintenanceCounter = null,
        DateTime? lastConsumablesChangeAt = null,
        long? lastConsumablesChangeCounter = null,
        bool isActive = true) => new()
    {
        AssetId = asset.Id,
        ContractId = contract.Id,
        IsActive = isActive,
        LastGeneralMaintenanceAt = lastGeneralMaintenanceAt,
        LastGeneralMaintenanceCounter = lastGeneralMaintenanceCounter,
        NextGeneralDueAt = nextGeneralDueAt ?? DateTime.UtcNow.AddMonths(6),
        NextGeneralDueCounter = nextGeneralDueCounter,
        LastUnitsMaintenanceAt = lastUnitsMaintenanceAt,
        LastUnitsMaintenanceCounter = lastUnitsMaintenanceCounter,
        NextUnitsDueAt = nextUnitsDueAt ?? DateTime.UtcNow.AddMonths(6),
        NextUnitsDueCounter = nextUnitsDueCounter,
        LastConsumablesChangeAt = lastConsumablesChangeAt,
        LastConsumablesChangeCounter = lastConsumablesChangeCounter,
        NextConsumablesDueCounter = nextConsumablesDueCounter
    };

    public static MaintenanceOrder MaintenanceOrder(
        MaintenanceSchedule schedule,
        Asset asset,
        MaintenanceOrderStatus status = MaintenanceOrderStatus.Pendiente,
        Guid? technicianId = null,
        DateTime? scheduledDate = null,
        bool includesGeneral = true,
        bool includesUnits = false,
        bool includesConsumables = false) => new()
    {
        MaintenanceScheduleId = schedule.Id,
        AssetId = asset.Id,
        Status = status,
        TechnicianId = technicianId,
        ScheduledDate = scheduledDate ?? DateTime.UtcNow,
        IncludesGeneral = includesGeneral,
        IncludesUnits = includesUnits,
        IncludesConsumables = includesConsumables
    };
}
