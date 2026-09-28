using Toner.Application.Evidences.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Evidences;

public sealed record EvidenceContent(Stream Content, string ContentType, string FileName);

public interface IEvidenceService
{
    // Sube la foto de un técnico para el ticket u orden que tiene asignados. Exactamente uno de los dos ids.
    Task<EvidenceDto> UploadAsync(
        Guid technicianId, EvidenceKind kind, Guid? serviceTicketId, Guid? maintenanceOrderId, Stream content,
        CancellationToken cancellationToken = default);

    // requesterTechnicianId null = staff (ve todo); un técnico solo ve lo suyo o lo de sus asignaciones.
    Task<IReadOnlyList<EvidenceDto>> ListAsync(
        Guid? serviceTicketId, Guid? maintenanceOrderId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default);

    Task<EvidenceContent> GetContentAsync(Guid evidenceId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default);
}
