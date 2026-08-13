using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts.Validators;

public class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.ClientId)
            .MustAsync(async (id, ct) => await db.Clients.AnyAsync(c => c.Id == id, ct))
            .WithMessage("El ClientId especificado no existe.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("EndDate no puede ser anterior a StartDate.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.IncludedPrintsPerMonth).GreaterThanOrEqualTo(0).When(x => x.IncludedPrintsPerMonth.HasValue);
        RuleFor(x => x.PricePerExtraPage).GreaterThanOrEqualTo(0).When(x => x.PricePerExtraPage.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
