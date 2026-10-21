namespace NOIR.Application.Features.Estimation.Queries.GetBoqById;

/// <summary>
/// Query to get a BOQ by ID (includes line items and revisions).
/// </summary>
public sealed record GetBoqByIdQuery(Guid Id);
