namespace NOIR.Application.Features.Crm.Queries.GetCompanies;

public class GetCompaniesQueryHandler
{
    private readonly IRepository<CrmCompany, Guid> _companyRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetCompaniesQueryHandler(IRepository<CrmCompany, Guid> companyRepository, IUserDisplayNameService userDisplayNameService)
    {
        _companyRepository = companyRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<Features.Crm.DTOs.CompanyListDto>>> Handle(
        GetCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new Specifications.CompaniesFilterSpec(query.Search, skip, query.PageSize, query.OrderBy, query.IsDescending);
        var companies = await _companyRepository.ListAsync(spec, cancellationToken);

        var countSpec = new Specifications.CompaniesCountSpec(query.Search);
        var totalCount = await _companyRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = companies
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = companies.Select(c => new Features.Crm.DTOs.CompanyListDto(
            c.Id, c.Name, c.Domain, c.Industry,
            c.Owner != null ? $"{c.Owner.FirstName} {c.Owner.LastName}" : null,
            c.Contacts.Count, c.CreatedAt, c.ModifiedAt,
            c.CreatedBy != null ? userNames.GetValueOrDefault(c.CreatedBy) : null,
            c.ModifiedBy != null ? userNames.GetValueOrDefault(c.ModifiedBy) : null)).ToList();

        return Result.Success(PagedResult<Features.Crm.DTOs.CompanyListDto>.Create(
            items, totalCount, query.Page - 1, query.PageSize));
    }
}
