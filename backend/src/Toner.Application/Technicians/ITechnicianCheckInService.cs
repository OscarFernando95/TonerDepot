using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians;

public interface ITechnicianCheckInService
{
    Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default);
    Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default);
}
