namespace NOIR.Application.Features.Hr.Commands.ReactivateEmployee;

public sealed class ReactivateEmployeeCommandValidator : AbstractValidator<ReactivateEmployeeCommand>
{
    public ReactivateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Employee ID is required.");
    }
}
