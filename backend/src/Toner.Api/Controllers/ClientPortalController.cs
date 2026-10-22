using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Api.Auth;
using Toner.Application.ClientPortal;
using Toner.Application.ClientPortal.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/client-portal")]
[Authorize(Roles = RoleNames.Cliente)]
public class ClientPortalController : ControllerBase
{
    private readonly IClientPortalService _portalService;

    public ClientPortalController(IClientPortalService portalService) => _portalService = portalService;

    // Inicio del cliente: un solo llamado con el resumen de su servicio (tickets, mantenimientos e instalaciones).
    [HttpGet("home")]
    public async Task<ActionResult<ClientHomeDto>> GetHome(CancellationToken cancellationToken)
    {
        return Ok(await _portalService.GetHomeAsync(User.ToRequestingUser(), cancellationToken));
    }
}
