using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Clients.Validators;

public class UpdateClientLocationRequestValidator : AbstractValidator<UpdateClientLocationRequest>
{
    public UpdateClientLocationRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(50);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage("La latitud debe estar entre -90 y 90.");
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage("La longitud debe estar entre -180 y 180.");
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue).WithMessage("Latitud y longitud van juntas: indica ambas o ninguna.");


        RuleFor(x => x.CityId)
            .MustAsync(async (cityId, ct) => await db.Cities.AnyAsync(c => c.Id == cityId, ct))
            .WithMessage("El CityId especificado no existe.");
    }
}
