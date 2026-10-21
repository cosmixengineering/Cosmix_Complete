namespace NOIR.Application.Features.Crm.Commands.ReorderLead;

public sealed class ReorderLeadCommandValidator : AbstractValidator<ReorderLeadCommand>
{
    public ReorderLeadCommandValidator()
    {
        RuleFor(x => x.LeadId)
            .NotEmpty().WithMessage("Lead ID is required.");
    }
}
