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
            .Must(x => x.ServiceTicketId.HasValue ^ x.MaintenanceOrderId.HasValue)
            .WithMessage("Debes indicar exactamente uno: ServiceTicketId o MaintenanceOrderId.");

        RuleFor(x => x.ServiceTicketId)
            .MustAsync(async (id, ct) => await db.ServiceTickets.AnyAsync(t => t.Id == id, ct))
            .WithMessage("El ServiceTicketId especificado no existe.")
            .When(x => x.ServiceTicketId.HasValue);

        RuleFor(x => x.MaintenanceOrderId)
            .MustAsync(async (id, ct) => await db.MaintenanceOrders.AnyAsync(o => o.Id == id, ct))
            .WithMessage("El MaintenanceOrderId especificado no existe.")
            .When(x => x.MaintenanceOrderId.HasValue);
    }
}
