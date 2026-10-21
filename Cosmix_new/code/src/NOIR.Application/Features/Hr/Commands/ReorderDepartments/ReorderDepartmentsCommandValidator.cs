namespace NOIR.Application.Features.Hr.Commands.ReorderDepartments;

public sealed class ReorderDepartmentsCommandValidator : AbstractValidator<ReorderDepartmentsCommand>
{
    public ReorderDepartmentsCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).NotEmpty().WithMessage("Department ID is required.");
            item.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0).WithMessage("Sort order must be non-negative.");
        });
    }
}
