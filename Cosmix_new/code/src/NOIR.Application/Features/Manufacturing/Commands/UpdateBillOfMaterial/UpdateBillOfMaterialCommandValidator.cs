namespace NOIR.Application.Features.Manufacturing.Commands.UpdateBillOfMaterial;

/// <summary>
/// Validator for UpdateBillOfMaterialCommand.
/// </summary>
public class UpdateBillOfMaterialCommandValidator : AbstractValidator<UpdateBillOfMaterialCommand>
{
    public UpdateBillOfMaterialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Bill of material ID is required.");

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
    }
}
