namespace NOIR.Application.Features.Crm.Commands.DeleteCompany;

public sealed class DeleteCompanyCommandValidator : AbstractValidator<DeleteCompanyCommand>
{
    public DeleteCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");
    }
}
