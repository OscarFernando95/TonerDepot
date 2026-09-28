using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Evidences.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Evidences;

public class EvidenceService : IEvidenceService
{
    private readonly IApplicationDbContext _db;
    private readonly IEvidenceStorage _storage;
    private readonly EvidenceOptions _options;
    private readonly ILogger<EvidenceService> _logger;

    public EvidenceService(IApplicationDbContext db, IEvidenceStorage storage, IOptions<EvidenceOptions> options, ILogger<EvidenceService> logger)
    {
        _db = db;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EvidenceDto> UploadAsync(
        Guid technicianId, EvidenceKind kind, Guid? serviceTicketId, Guid? maintenanceOrderId, Stream content,
        CancellationToken cancellationToken = default)
    {
        if (serviceTicketId.HasValue == maintenanceOrderId.HasValue)
        {
            throw new FluentValidation.ValidationException("Indica exactamente uno: ticket u orden de mantenimiento.");
        }

        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        Guid clientId;
        if (serviceTicketId is { } ticketId)
        {
            var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceTicket), ticketId);
            if (ticket.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Este ticket no está asignado a ti.");
            }
            clientId = ticket.ClientId;
        }
        else
        {
            var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == maintenanceOrderId!.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(MaintenanceOrder), maintenanceOrderId!.Value);
            if (order.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Esta orden no está asignada a ti.");
            }
            clientId = order.ClientId;
        }

        // Se lee con tope +1 byte: si excede el máximo se rechaza sin cargar un archivo arbitrariamente grande.
        var buffer = new MemoryStream();
        var limited = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(limited, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(limited.AsMemory(0, read), cancellationToken);
            if (buffer.Length > _options.MaxBytes)
            {
                throw new FluentValidation.ValidationException($"La foto supera el máximo de {_options.MaxBytes / (1024 * 1024)} MB.");
            }
        }

        // El tipo se decide por los primeros bytes del archivo, nunca por el Content-Type ni el nombre que
        // declare el cliente: un .html renombrado a .jpg no debe terminar servido como imagen.
        var image = ImageSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw new FluentValidation.ValidationException("El archivo no es una imagen válida (se aceptan JPEG, PNG y WebP).");

        var id = Guid.NewGuid();
        var key = $"{clientId}/{DateTime.UtcNow:yyyyMM}/{id}.{image.Extension}";

        buffer.Position = 0;
        await _storage.SaveAsync(key, buffer, image.ContentType, cancellationToken);

        var evidence = new Evidence
        {
            Id = id,
            ClientId = clientId,
            Kind = kind,
            ServiceTicketId = serviceTicketId,
            MaintenanceOrderId = maintenanceOrderId,
            FileUrl = key,
            // Nombre generado, nunca el que manda el cliente.
            FileName = $"{kind.ToString().ToLowerInvariant()}-{id:N}.{image.Extension}",
            ContentType = image.ContentType,
            SizeBytes = buffer.Length,
            UploadedByUserId = technician.UserId
        };
        _db.Evidences.Add(evidence);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sin fila en base el blob quedaría huérfano: se intenta limpiar (mejor esfuerzo).
            try { await _storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo borrar el blob huérfano {Key}", key); }
            throw;
        }

        _logger.LogInformation(
            "Evidencia {EvidenceId} ({Kind}, {Bytes} bytes) subida por técnico {TechnicianId}", id, kind, buffer.Length, technicianId);

        return ToDto(evidence);
    }

    public async Task<IReadOnlyList<EvidenceDto>> ListAsync(
        Guid? serviceTicketId, Guid? maintenanceOrderId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default)
    {
        if (serviceTicketId.HasValue == maintenanceOrderId.HasValue)
        {
            throw new FluentValidation.ValidationException("Indica exactamente uno: ticket u orden de mantenimiento.");
        }

        var query = _db.Evidences.Where(e => serviceTicketId != null ? e.ServiceTicketId == serviceTicketId : e.MaintenanceOrderId == maintenanceOrderId);
        var rows = await query.OrderBy(e => e.UploadedAt).ToListAsync(cancellationToken);

        if (requesterTechnicianId is { } techId)
        {
            await EnsureTechnicianOwnsTargetAsync(techId, serviceTicketId, maintenanceOrderId, cancellationToken);
        }

        return rows.Select(ToDto).ToList();
    }

    public async Task<EvidenceContent> GetContentAsync(Guid evidenceId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default)
    {
        var evidence = await _db.Evidences.FirstOrDefaultAsync(e => e.Id == evidenceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Evidence), evidenceId);

        if (requesterTechnicianId is { } techId)
        {
            await EnsureTechnicianOwnsTargetAsync(techId, evidence.ServiceTicketId, evidence.MaintenanceOrderId, cancellationToken);
        }

        var stream = await _storage.OpenReadAsync(evidence.FileUrl, cancellationToken)
            ?? throw new NotFoundException(nameof(Evidence), evidenceId);

        return new EvidenceContent(stream, evidence.ContentType, evidence.FileName);
    }

    // Un técnico solo ve la evidencia de lo que tiene (o tuvo) asignado.
    private async Task EnsureTechnicianOwnsTargetAsync(Guid technicianId, Guid? serviceTicketId, Guid? maintenanceOrderId, CancellationToken cancellationToken)
    {
        var owns = serviceTicketId.HasValue
            ? await _db.ServiceTickets.AnyAsync(t => t.Id == serviceTicketId && t.TechnicianId == technicianId, cancellationToken)
            : await _db.MaintenanceOrders.AnyAsync(o => o.Id == maintenanceOrderId && o.TechnicianId == technicianId, cancellationToken);

        if (!owns)
        {
            throw new ForbiddenException("No tienes acceso a esta evidencia.");
        }
    }

    private static EvidenceDto ToDto(Evidence e) => new()
    {
        Id = e.Id,
        Kind = e.Kind.ToString(),
        ContentType = e.ContentType,
        SizeBytes = e.SizeBytes,
        UploadedAt = e.UploadedAt,
        ServiceTicketId = e.ServiceTicketId,
        MaintenanceOrderId = e.MaintenanceOrderId,
        TimeLogId = e.TimeLogId
    };
}

public sealed record ImageType(string ContentType, string Extension);

public static class ImageSignature
{
    public static ImageType? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return new ImageType("image/jpeg", "jpg");
        }

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return new ImageType("image/png", "png");
        }

        // RIFF....WEBP
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
        {
            return new ImageType("image/webp", "webp");
        }

        return null;
    }
}
