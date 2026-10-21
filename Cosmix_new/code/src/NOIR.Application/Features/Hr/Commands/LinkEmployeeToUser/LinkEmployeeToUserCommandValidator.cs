namespace NOIR.Application.Features.Hr.Commands.LinkEmployeeToUser;

public sealed class LinkEmployeeToUserCommandValidator : AbstractValidator<LinkEmployeeToUserCommand>
{
    public LinkEmployeeToUserCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty().WithMessage("Employee ID is required.");
        RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("User ID is required.");
    }
}
