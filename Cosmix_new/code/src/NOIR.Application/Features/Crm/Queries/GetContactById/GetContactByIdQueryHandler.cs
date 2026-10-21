namespace NOIR.Application.Features.Crm.Queries.GetContactById;

public class GetContactByIdQueryHandler
{
    private readonly IRepository<CrmContact, Guid> _contactRepository;

    public GetContactByIdQueryHandler(IRepository<CrmContact, Guid> contactRepository)
    {
        _contactRepository = contactRepository;
    }

    public async Task<Result<Features.Crm.DTOs.ContactDto>> Handle(
        GetContactByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new Specifications.ContactByIdReadOnlySpec(query.Id);
        var contact = await _contactRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (contact is null)
        {
            return Result.Failure<Features.Crm.DTOs.ContactDto>(
                Error.NotFound($"Contact with ID '{query.Id}' not found.", "NOIR-CRM-002"));
        }

        return Result.Success(MapToDto(contact));
    }

    private static Features.Crm.DTOs.ContactDto MapToDto(CrmContact c) =>
        new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
            c.CompanyId, c.Company?.Name, c.OwnerId,
            c.Owner != null ? $"{c.Owner.FirstName} {c.Owner.LastName}" : null,
            c.Source, c.CustomerId, c.Notes,
            c.Leads.Select(l => new Features.Crm.DTOs.LeadBriefDto(
                l.Id, l.Title, l.Value, l.Currency, l.Status, l.Stage?.Name)).ToList(),
            c.CreatedAt, c.ModifiedAt);
}
