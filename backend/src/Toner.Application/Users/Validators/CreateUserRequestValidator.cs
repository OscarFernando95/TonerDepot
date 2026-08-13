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
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

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
