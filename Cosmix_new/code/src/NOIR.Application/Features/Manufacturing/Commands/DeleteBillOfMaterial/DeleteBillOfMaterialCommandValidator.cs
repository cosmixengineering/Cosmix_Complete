namespace NOIR.Application.Features.Manufacturing.Commands.DeleteBillOfMaterial;

/// <summary>
/// Validator for DeleteBillOfMaterialCommand.
/// </summary>
public class DeleteBillOfMaterialCommandValidator : AbstractValidator<DeleteBillOfMaterialCommand>
{
    public DeleteBillOfMaterialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Bill of material ID is required.");
    }
}
