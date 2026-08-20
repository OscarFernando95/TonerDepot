using Toner.Domain.Entities;

namespace Toner.Application.Assignment;

// Motor de asignación de técnicos: por ciudad de cobertura + menor carga de trabajo actual,
// excluyendo técnicos en estado Ocupado. Usado tanto al crear un ServiceTicket como al generar
// una MaintenanceOrder (mismo criterio, mismo AssignmentHistory).
//
// Recibe la ENTIDAD TRACKEADA y NO hace SaveChangesAsync — el caller decide cuándo persistir
// (mismo patrón que AssetService.PrepareStatusChangeAsync e IMaintenanceScheduleEngine).
// CODE_QUALITY_AUDIT.md hallazgo #8: antes recibía un Guid, releía la entidad de la base y guardaba
// por su cuenta, así que toda operación que creara un ticket u orden y luego la asignara hacía DOS
// commits independientes. Si el segundo fallaba quedaba un ticket en Abierto sin asignar, o —peor—
// una orden Pendiente huérfana que bloquea para siempre la generación de órdenes de ese cronograma
// (EvaluateAllDueAsync salta los cronogramas con orden abierta, así que ni el job diario lo cura).
//
// Tomar la entidad en vez del Guid no es un detalle de estilo: es lo que hace posible asignar ANTES
// de guardar. Un Guid obligaría a releer con FirstAsync, que va a la base y no vería una entidad
// todavía sin persistir. Las entidades ya traen Id propio desde el cliente (BaseEntity.Id con
// ValueGeneratedNever), así que no hace falta guardar para tener identidad.
public interface IAssignmentEngine
{
    // Devuelve el TechnicianId asignado, o null si no había ningún candidato (el ticket queda "Sin asignar").
    Task<Guid?> AssignServiceTicketAsync(ServiceTicket ticket, CancellationToken cancellationToken = default);

    // Devuelve el TechnicianId asignado, o null si no había ningún candidato (la orden queda sin asignar).
    Task<Guid?> AssignMaintenanceOrderAsync(MaintenanceOrder order, CancellationToken cancellationToken = default);
}
