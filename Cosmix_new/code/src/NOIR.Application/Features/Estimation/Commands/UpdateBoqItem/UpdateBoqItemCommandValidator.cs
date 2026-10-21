namespace NOIR.Application.Features.Estimation.Commands.UpdateBoqItem;

/// <summary>
/// Validator for UpdateBoqItemCommand.
/// </summary>
public class UpdateBoqItemCommandValidator : AbstractValidator<UpdateBoqItemCommand>
{
    public UpdateBoqItemCommandValidator()
    {
        RuleFor(x => x.BoqId)
            .NotEmpty().WithMessage("BOQ ID is required.");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required.");

        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Section is required.")
            .MaximumLength(100).WithMessage("Section must not exceed 100 characters.");

        RuleFor(x => x.ItemCode)
            .NotEmpty().WithMessage("Item code is required.")
            .MaximumLength(50).WithMessage("Item code must not exceed 50 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(20).WithMessage("Unit must not exceed 20 characters.");

        RuleFor(x => x.UnitRate)
            .GreaterThanOrEqualTo(0).WithMessage("Unit rate cannot be negative.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order cannot be negative.");
    }
}
