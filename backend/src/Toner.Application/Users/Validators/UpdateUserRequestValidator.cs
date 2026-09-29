using FluentValidation;
using Toner.Application.Common;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Users.Dtos;

namespace Toner.Application.Users.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Cedula).NotEmpty().MaximumLength(20);

        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches(PhoneRules.Pattern).WithMessage(PhoneRules.Message);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);

        RuleFor(x => x.CityId)
            .MustAsync(async (cityId, ct) => await db.Cities.AnyAsync(c => c.Id == cityId, ct))
            .WithMessage("El CityId especificado no existe.");
    }
}
