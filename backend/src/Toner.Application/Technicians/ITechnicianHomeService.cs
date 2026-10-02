using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians;

public interface ITechnicianHomeService
{
    Task<TechnicianHomeDto> GetAsync(Guid technicianId, CancellationToken cancellationToken = default);
}
