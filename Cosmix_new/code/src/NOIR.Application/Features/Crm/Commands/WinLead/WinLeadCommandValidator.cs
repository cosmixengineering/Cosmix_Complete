namespace NOIR.Application.Features.Crm.Commands.WinLead;

public sealed class WinLeadCommandValidator : AbstractValidator<WinLeadCommand>
{
    public WinLeadCommandValidator()
    {
        RuleFor(x => x.LeadId)
            .NotEmpty().WithMessage("Lead ID is required.");
    }
}
