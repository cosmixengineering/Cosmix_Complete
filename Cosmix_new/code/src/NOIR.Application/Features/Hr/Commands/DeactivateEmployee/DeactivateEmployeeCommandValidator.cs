namespace NOIR.Application.Features.Hr.Commands.DeactivateEmployee;

public sealed class DeactivateEmployeeCommandValidator : AbstractValidator<DeactivateEmployeeCommand>
{
    public DeactivateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Status)
            .Must(s => s == EmployeeStatus.Resigned || s == EmployeeStatus.Terminated)
            .WithMessage("Status must be Resigned or Terminated for deactivation.");
    }
}
