using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common.Exceptions;
using Toner.Application.Evidences;
using Toner.Application.Evidences.Dtos;
using Toner.Domain.Common;

using Toner.Api.Middleware;

namespace Toner.Api.Controllers;

// Consulta de las fotos de evidencia de un ticket u orden. Staff ve todo; un técnico solo lo que tiene
// asignado (lo hace cumplir EvidenceService). El contenido se sirve aquí, autenticado — el contenedor de
// blobs es privado y no hay URLs firmadas.
[ApiController]
[Route("api/evidence")]
[Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
public class EvidenceController : ControllerBase
{
    private readonly IEvidenceService _service;

    public EvidenceController(IEvidenceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EvidenceDto>>> List(
        [FromQuery] Guid? ticketId, [FromQuery] Guid? orderId, CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(ticketId, orderId, RequesterTechnicianId, cancellationToken));

    [HttpGet("{id:guid}/content")]
    [NoRequestScopedDbConnection] // no retener una conexión del pool mientras se descarga la foto (AUDIT.md H-14)
    public async Task<IActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var content = await _service.GetContentAsync(id, RequesterTechnicianId, cancellationToken);

        // El tipo lo fijó el servidor por la firma real del archivo; nosniff impide que un navegador lo reinterprete.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=3600";
        return File(content.Content, content.ContentType);
    }

    // null = staff (ve todo). Un usuario con rol Tecnico queda restringido a su propio técnico y, si su
    // token no trae un technician_id válido, se rechaza: devolver null aquí lo trataría como staff y le
    // mostraría las evidencias de todos los clientes (falla abierto — AUDIT.md H-09).
    private Guid? RequesterTechnicianId
    {
        get
        {
            if (!User.IsInRole(RoleNames.Tecnico))
            {
                return null;
            }

            return Guid.TryParse(User.FindFirstValue("technician_id"), out var id) && id != Guid.Empty
                ? id
                : throw new ForbiddenException("Tu usuario no tiene un perfil de técnico asociado.");
        }
    }
}
