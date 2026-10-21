namespace NOIR.Application.Features.Crm.Commands.DeleteActivity;

public sealed class DeleteActivityCommandValidator : AbstractValidator<DeleteActivityCommand>
{
    public DeleteActivityCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Activity ID is required.");
    }
}
