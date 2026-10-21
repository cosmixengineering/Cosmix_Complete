namespace NOIR.Application.Features.Crm.Commands.LoseLead;

public sealed class LoseLeadCommandValidator : AbstractValidator<LoseLeadCommand>
{
    public LoseLeadCommandValidator()
    {
        RuleFor(x => x.LeadId)
            .NotEmpty().WithMessage("Lead ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.")
            .When(x => x.Reason is not null);
    }
}
