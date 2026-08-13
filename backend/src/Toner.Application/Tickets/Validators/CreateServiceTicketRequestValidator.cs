using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Tickets.Validators;

public class CreateServiceTicketRequestValidator : AbstractValidator<CreateServiceTicketRequest>
{
    public CreateServiceTicketRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);

        RuleFor(x => x.ClientLocationId)
            .MustAsync(async (id, ct) => await db.ClientLocations.AnyAsync(l => l.Id == id, ct))
            .WithMessage("El ClientLocationId especificado no existe.");

        RuleFor(x => x.Priority)
            .Must(p => Enum.TryParse<ServiceTicketPriority>(p, out _))
            .WithMessage($"Priority debe ser uno de: {string.Join(", ", Enum.GetNames<ServiceTicketPriority>())}.")
            .When(x => !string.IsNullOrEmpty(x.Priority));

        RuleFor(x => x)
            .MustAsync(async (request, ct) =>
            {
                if (!request.AssetId.HasValue)
                {
                    return true;
                }

                var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, ct);
                return asset is not null && asset.CurrentClientLocationId == request.ClientLocationId;
            })
            .WithMessage("El AssetId indicado no existe o no está instalado en la sede indicada.")
            .When(x => x.AssetId.HasValue);
    }
}
