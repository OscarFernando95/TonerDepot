namespace Toner.Application.Assignment;

// Motor de asignación de técnicos: por ciudad de cobertura + menor carga de trabajo actual,
// excluyendo técnicos en estado Ocupado. Usado tanto al crear un ServiceTicket como al generar
// una MaintenanceOrder (mismo criterio, mismo AssignmentHistory).
public interface IAssignmentEngine
{
    // Devuelve el TechnicianId asignado, o null si no había ningún candidato (el ticket queda "Sin asignar").
    Task<Guid?> AssignServiceTicketAsync(Guid serviceTicketId, CancellationToken cancellationToken = default);

    // Devuelve el TechnicianId asignado, o null si no había ningún candidato (la orden queda sin asignar).
    Task<Guid?> AssignMaintenanceOrderAsync(Guid maintenanceOrderId, CancellationToken cancellationToken = default);
}
