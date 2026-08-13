using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Clients.Validators;

public class CreateClientLocationRequestValidator : AbstractValidator<CreateClientLocationRequest>
{
    public CreateClientLocationRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(50);

        RuleFor(x => x.CityId)
            .MustAsync(async (cityId, ct) => await db.Cities.AnyAsync(c => c.Id == cityId, ct))
            .WithMessage("El CityId especificado no existe.");
    }
}
