namespace NOIR.Application.Features.Estimation.Commands.DeleteBoqItem;

/// <summary>
/// Validator for DeleteBoqItemCommand.
/// </summary>
public class DeleteBoqItemCommandValidator : AbstractValidator<DeleteBoqItemCommand>
{
    public DeleteBoqItemCommandValidator()
    {
        RuleFor(x => x.BoqId)
            .NotEmpty().WithMessage("BOQ ID is required.");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required.");
    }
}
