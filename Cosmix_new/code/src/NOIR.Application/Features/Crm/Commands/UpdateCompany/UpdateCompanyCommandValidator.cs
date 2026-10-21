namespace NOIR.Application.Features.Crm.Commands.UpdateCompany;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters.");

        RuleFor(x => x.Domain)
            .MaximumLength(100).WithMessage("Domain cannot exceed 100 characters.")
            .When(x => x.Domain is not null);

        RuleFor(x => x.Industry)
            .MaximumLength(100).WithMessage("Industry cannot exceed 100 characters.")
            .When(x => x.Industry is not null);

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.")
            .When(x => x.Address is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone cannot exceed 20 characters.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Website)
            .MaximumLength(256).WithMessage("Website cannot exceed 256 characters.")
            .When(x => x.Website is not null);

        RuleFor(x => x.TaxId)
            .MaximumLength(50).WithMessage("Tax ID cannot exceed 50 characters.")
            .When(x => x.TaxId is not null);

        RuleFor(x => x.EmployeeCount)
            .GreaterThanOrEqualTo(0).WithMessage("Employee count cannot be negative.")
            .When(x => x.EmployeeCount is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}
