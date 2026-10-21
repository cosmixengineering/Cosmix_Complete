namespace NOIR.Application.Features.Pm.Commands.ChangeProjectMemberRole;

public sealed class ChangeProjectMemberRoleCommandValidator : AbstractValidator<ChangeProjectMemberRoleCommand>
{
    public ChangeProjectMemberRoleCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("Member ID is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid project member role.");
    }
}
