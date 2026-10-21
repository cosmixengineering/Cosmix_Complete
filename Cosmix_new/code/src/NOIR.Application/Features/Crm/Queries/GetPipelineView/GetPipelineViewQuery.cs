namespace NOIR.Application.Features.Crm.Queries.GetPipelineView;

public sealed record GetPipelineViewQuery(
    Guid PipelineId,
    bool IncludeClosedDeals = false);
