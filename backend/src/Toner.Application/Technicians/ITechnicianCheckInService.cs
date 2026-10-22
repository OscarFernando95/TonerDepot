using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Paging;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians;

public interface ITechnicianCheckInService
{
    Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default);
    // Instalaciones pendientes de las ciudades que cubre el técnico, marcando cuáles puede iniciar ahora por horario.
    Task<PagedResult<PendingInstallationDto>> ListPendingInstallationsAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default);

    // El técnico inicia por su cuenta un mantenimiento a demanda de un equipo de su zona (o vinculado a él): la orden
    // nace ya asignada a él y sigue el camino de siempre (check-in con foto "antes", check-out con contador, que es
    // quien reinicia en el cronograma lo que la orden incluyó).
    //
    // Con request.PlannedAt la orden queda PROGRAMADA para esa fecha (no se exige estar libre ni en horario ahora: eso se
    // valida en el check-in, cuando la haga); sin ella, el técnico la empieza ahora.
    Task<MaintenanceOrderDto> StartMaintenanceAsync(Guid technicianId, StartSelfMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default);
}
