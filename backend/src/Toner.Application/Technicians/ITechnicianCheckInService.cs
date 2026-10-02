using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Paging;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians;

public interface ITechnicianCheckInService
{
    Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default);
    // Instalaciones pendientes de las ciudades que cubre el técnico, marcando cuáles puede iniciar ahora por horario.
    Task<PagedResult<PendingInstallationDto>> ListPendingInstallationsAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default);
}
