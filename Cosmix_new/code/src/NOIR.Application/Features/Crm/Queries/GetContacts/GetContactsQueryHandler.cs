namespace NOIR.Application.Features.Crm.Queries.GetContacts;

public class GetContactsQueryHandler
{
    private readonly IRepository<CrmContact, Guid> _contactRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetContactsQueryHandler(IRepository<CrmContact, Guid> contactRepository, IUserDisplayNameService userDisplayNameService)
    {
        _contactRepository = contactRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<Features.Crm.DTOs.ContactListDto>>> Handle(
        GetContactsQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new Specifications.ContactsFilterSpec(
            query.Search, query.CompanyId, query.OwnerId, query.Source,
            skip, query.PageSize, query.OrderBy, query.IsDescending);

        var contacts = await _contactRepository.ListAsync(spec, cancellationToken);

        var countSpec = new Specifications.ContactsCountSpec(
            query.Search, query.CompanyId, query.OwnerId, query.Source);
        var totalCount = await _contactRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = contacts
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = contacts.Select(c => new Features.Crm.DTOs.ContactListDto(
            c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
            c.Company?.Name, c.Owner != null ? $"{c.Owner.FirstName} {c.Owner.LastName}" : null,
            c.Source, c.CustomerId.HasValue, c.CreatedAt, c.ModifiedAt,
            c.CreatedBy != null ? userNames.GetValueOrDefault(c.CreatedBy) : null,
            c.ModifiedBy != null ? userNames.GetValueOrDefault(c.ModifiedBy) : null)).ToList();

        return Result.Success(PagedResult<Features.Crm.DTOs.ContactListDto>.Create(
            items, totalCount, query.Page - 1, query.PageSize));
    }
}
