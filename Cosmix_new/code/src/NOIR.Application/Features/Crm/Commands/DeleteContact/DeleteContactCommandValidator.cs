namespace NOIR.Application.Features.Crm.Commands.DeleteContact;

public sealed class DeleteContactCommandValidator : AbstractValidator<DeleteContactCommand>
{
    public DeleteContactCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Contact ID is required.");
    }
}
