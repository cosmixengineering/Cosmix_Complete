namespace NOIR.Application.Features.Manufacturing.Commands.DeactivateBillOfMaterial;

/// <summary>
/// Validator for DeactivateBillOfMaterialCommand.
/// </summary>
public class DeactivateBillOfMaterialCommandValidator : AbstractValidator<DeactivateBillOfMaterialCommand>
{
    public DeactivateBillOfMaterialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Bill of material ID is required.");
    }
}
