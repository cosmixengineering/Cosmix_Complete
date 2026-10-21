namespace NOIR.Application.Features.Crm.Queries.GetLeads;

public sealed record GetLeadsQuery(
    Guid? PipelineId = null,
    Guid? StageId = null,
    Guid? OwnerId = null,
    LeadStatus? Status = null,
    int Page = 1,
    int PageSize = 20);
