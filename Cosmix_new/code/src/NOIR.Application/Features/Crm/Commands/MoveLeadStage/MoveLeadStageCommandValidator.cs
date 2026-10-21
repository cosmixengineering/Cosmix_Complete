namespace NOIR.Application.Features.Crm.Commands.MoveLeadStage;

public sealed class MoveLeadStageCommandValidator : AbstractValidator<MoveLeadStageCommand>
{
    public MoveLeadStageCommandValidator()
    {
        RuleFor(x => x.LeadId)
            .NotEmpty().WithMessage("Lead ID is required.");

        RuleFor(x => x.NewStageId)
            .NotEmpty().WithMessage("New stage ID is required.");
    }
}
