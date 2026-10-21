namespace NOIR.Application.Features.Manufacturing.Commands.CreateBillOfMaterial;

/// <summary>
/// Validator for CreateBillOfMaterialCommand.
/// </summary>
public class CreateBillOfMaterialCommandValidator : AbstractValidator<CreateBillOfMaterialCommand>
{
    public CreateBillOfMaterialCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("BOM code is required.")
            .MaximumLength(50).WithMessage("BOM code must not exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("BOM name is required.")
            .MaximumLength(200).WithMessage("BOM name must not exceed 200 characters.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.OutputQuantity)
            .GreaterThan(0).WithMessage("Output quantity must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(20).WithMessage("Unit must not exceed 20 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("BOM must have at least one component.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("Component product is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Component quantity must be greater than zero.");

            item.RuleFor(i => i.Unit)
                .NotEmpty().WithMessage("Component unit is required.")
                .MaximumLength(20).WithMessage("Component unit must not exceed 20 characters.");

            item.RuleFor(i => i.WastePercent)
                .GreaterThanOrEqualTo(0).WithMessage("Waste percent cannot be negative.")
                .When(i => i.WastePercent.HasValue);

            item.RuleFor(i => i.Notes)
                .MaximumLength(500).WithMessage("Component notes must not exceed 500 characters.")
                .When(i => !string.IsNullOrEmpty(i.Notes));
        });
    }
}
