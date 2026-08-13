using FluentValidation;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Tickets.Validators;

public class SetTicketStatusRequestValidator : AbstractValidator<SetTicketStatusRequest>
{
    public SetTicketStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ServiceTicketStatus>(s, out _))
            .WithMessage($"Status debe ser uno de: {string.Join(", ", Enum.GetNames<ServiceTicketStatus>())}.");
    }
}
