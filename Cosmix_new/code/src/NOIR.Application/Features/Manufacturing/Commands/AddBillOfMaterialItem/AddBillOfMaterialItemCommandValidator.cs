namespace NOIR.Application.Features.Manufacturing.Commands.AddBillOfMaterialItem;

/// <summary>
/// Validator for AddBillOfMaterialItemCommand.
/// </summary>
public class AddBillOfMaterialItemCommandValidator : AbstractValidator<AddBillOfMaterialItemCommand>
{
    public AddBillOfMaterialItemCommandValidator()
    {
        RuleFor(x => x.BillOfMaterialId)
            .NotEmpty().WithMessage("Bill of material ID is required.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Component product is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(20).WithMessage("Unit must not exceed 20 characters.");

        RuleFor(x => x.WastePercent)
            .GreaterThanOrEqualTo(0).WithMessage("Waste percent cannot be negative.")
            .When(x => x.WastePercent.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
