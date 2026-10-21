namespace NOIR.Application.Features.Pm.Commands.RemoveProjectMember;

public sealed class RemoveProjectMemberCommandValidator : AbstractValidator<RemoveProjectMemberCommand>
{
    public RemoveProjectMemberCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("Member ID is required.");
    }
}
