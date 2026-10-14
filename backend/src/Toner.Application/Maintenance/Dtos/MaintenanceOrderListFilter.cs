namespace Toner.Application.Maintenance.Dtos;

// Filtros del listado de órdenes de mantenimiento, en el SERVIDOR y antes de paginar (AUDIT.md H-11, Paso 3).
// Opcionales; un AND con el alcance del rol (un Técnico solo ve las suyas pase lo que pase).
public sealed record MaintenanceOrderListFilter(string? Status = null, Guid? ClientId = null, string? Search = null);
