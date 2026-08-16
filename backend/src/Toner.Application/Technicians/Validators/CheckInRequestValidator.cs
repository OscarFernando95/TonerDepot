using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians.Validators;

public class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x)
            .Must(x => (x.ServiceTicketId.HasValue ? 1 : 0) + (x.MaintenanceOrderId.HasValue ? 1 : 0) + (x.AssetId.HasValue ? 1 : 0) == 1)
            .WithMessage("Debes indicar exactamente uno: ServiceTicketId, MaintenanceOrderId o AssetId.");

        RuleFor(x => x.ServiceTicketId)
            .MustAsync(async (id, ct) => await db.ServiceTickets.AnyAsync(t => t.Id == id, ct))
            .WithMessage("El ServiceTicketId especificado no existe.")
            .When(x => x.ServiceTicketId.HasValue);

        RuleFor(x => x.MaintenanceOrderId)
            .MustAsync(async (id, ct) => await db.MaintenanceOrders.AnyAsync(o => o.Id == id, ct))
            .WithMessage("El MaintenanceOrderId especificado no existe.")
            .When(x => x.MaintenanceOrderId.HasValue);

        RuleFor(x => x.AssetId)
            .MustAsync(async (id, ct) => await db.Assets.AnyAsync(a => a.Id == id, ct))
            .WithMessage("El AssetId especificado no existe.")
            .When(x => x.AssetId.HasValue);
    }
}
