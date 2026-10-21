namespace NOIR.Application.Features.Crm.Queries.GetCompanyById;

public class GetCompanyByIdQueryHandler
{
    private readonly IRepository<CrmCompany, Guid> _companyRepository;

    public GetCompanyByIdQueryHandler(IRepository<CrmCompany, Guid> companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Result<Features.Crm.DTOs.CompanyDto>> Handle(
        GetCompanyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new Specifications.CompanyByIdReadOnlySpec(query.Id);
        var company = await _companyRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (company is null)
        {
            return Result.Failure<Features.Crm.DTOs.CompanyDto>(
                Error.NotFound($"Company with ID '{query.Id}' not found.", "NOIR-CRM-011"));
        }

        return Result.Success(new Features.Crm.DTOs.CompanyDto(
            company.Id, company.Name, company.Domain, company.Industry,
            company.Address, company.Phone, company.Website,
            company.OwnerId,
            company.Owner != null ? $"{company.Owner.FirstName} {company.Owner.LastName}" : null,
            company.TaxId, company.EmployeeCount, company.Notes,
            company.Contacts.Count, company.CreatedAt, company.ModifiedAt));
    }
}
