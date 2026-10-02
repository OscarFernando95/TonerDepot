using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Zones.Dtos;

namespace Toner.Application.Zones.Validators;

public class CreateZoneRequestValidator : AbstractValidator<CreateZoneRequest>
{
    public CreateZoneRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateZoneRequestValidator : AbstractValidator<UpdateZoneRequest>
{
    public UpdateZoneRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class SetZoneCitiesRequestValidator : AbstractValidator<SetZoneCitiesRequest>
{
    public SetZoneCitiesRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.CityIds)
            .NotNull()
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("La lista de municipios tiene repetidos.")
            .MustAsync(async (ids, ct) =>
            {
                var distinct = ids.Distinct().ToList();
                return await db.Cities.CountAsync(c => distinct.Contains(c.Id), ct) == distinct.Count;
            })
            .WithMessage("Alguno de los municipios especificados no existe.");
    }
}
