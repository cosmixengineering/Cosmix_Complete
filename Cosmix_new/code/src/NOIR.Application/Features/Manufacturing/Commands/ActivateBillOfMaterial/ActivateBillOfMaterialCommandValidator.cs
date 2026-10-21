namespace NOIR.Application.Features.Manufacturing.Commands.ActivateBillOfMaterial;

/// <summary>
/// Validator for ActivateBillOfMaterialCommand.
/// </summary>
public class ActivateBillOfMaterialCommandValidator : AbstractValidator<ActivateBillOfMaterialCommand>
{
    public ActivateBillOfMaterialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Bill of material ID is required.");
    }
}
