using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;

namespace Toner.Application.Users.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Cedula).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Cedula)
            .MustAsync(async (cedula, ct) => !await db.Users.AnyAsync(u => u.Cedula == cedula, ct))
            .WithMessage("Ya existe un usuario con esa cédula.")
            .When(x => !string.IsNullOrWhiteSpace(x.Cedula));

        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);

        RuleFor(x => x.CityId)
            .MustAsync(async (cityId, ct) => await db.Cities.AnyAsync(c => c.Id == cityId, ct))
            .WithMessage("El CityId especificado no existe.");

        RuleFor(x => x.RoleName)
            .NotEmpty()
            .Must(role => RoleNames.All.Contains(role))
            .WithMessage($"RoleName debe ser uno de: {string.Join(", ", RoleNames.All)}.");

        RuleFor(x => x.ClientId)
            .NotNull()
            .WithMessage("ClientId es obligatorio cuando RoleName es Cliente.")
            .When(x => x.RoleName == RoleNames.Cliente);

        RuleFor(x => x.ClientId)
            .MustAsync(async (clientId, ct) => await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            .WithMessage("El ClientId especificado no existe.")
            .When(x => x.ClientId.HasValue);
    }
}
