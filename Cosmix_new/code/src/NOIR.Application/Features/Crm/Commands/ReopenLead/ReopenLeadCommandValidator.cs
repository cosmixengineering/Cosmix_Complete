namespace NOIR.Application.Features.Crm.Commands.ReopenLead;

public sealed class ReopenLeadCommandValidator : AbstractValidator<ReopenLeadCommand>
{
    public ReopenLeadCommandValidator()
    {
        RuleFor(x => x.LeadId)
            .NotEmpty().WithMessage("Lead ID is required.");
    }
}
