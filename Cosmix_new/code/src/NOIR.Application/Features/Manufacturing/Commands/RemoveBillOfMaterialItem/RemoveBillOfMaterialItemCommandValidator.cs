namespace NOIR.Application.Features.Manufacturing.Commands.RemoveBillOfMaterialItem;

/// <summary>
/// Validator for RemoveBillOfMaterialItemCommand.
/// </summary>
public class RemoveBillOfMaterialItemCommandValidator : AbstractValidator<RemoveBillOfMaterialItemCommand>
{
    public RemoveBillOfMaterialItemCommandValidator()
    {
        RuleFor(x => x.BillOfMaterialId)
            .NotEmpty().WithMessage("Bill of material ID is required.");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("BOM item ID is required.");
    }
}
