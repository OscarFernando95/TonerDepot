namespace Toner.Application.Maintenance.Dtos;

// Filtros del listado de cronogramas, en el SERVIDOR y antes de paginar (AUDIT.md H-11, Paso 3). Opcionales.
public sealed record MaintenanceScheduleListFilter(
    string? Search = null, Guid? CityId = null, Guid? ClientId = null, Guid? ContractId = null, bool? IsActive = null);
