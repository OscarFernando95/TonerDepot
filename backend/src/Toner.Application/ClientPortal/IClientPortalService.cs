using Toner.Application.ClientPortal.Dtos;
using Toner.Application.Common;

namespace Toner.Application.ClientPortal;

public interface IClientPortalService
{
    // Resumen del inicio del cliente, con el alcance de su empresa y de sus sedes (ClientScope).
    Task<ClientHomeDto> GetHomeAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
}
