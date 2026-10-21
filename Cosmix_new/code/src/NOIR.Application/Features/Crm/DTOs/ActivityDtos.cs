namespace NOIR.Application.Features.Crm.DTOs;

/// <summary>
/// Full activity details.
/// </summary>
public sealed record ActivityDto(
    Guid Id,
    ActivityType Type,
    string Subject,
    string? Description,
    Guid? ContactId,
    string? ContactName,
    Guid? LeadId,
    string? LeadTitle,
    Guid PerformedById,
    string PerformedByName,
    DateTimeOffset PerformedAt,
    int? DurationMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

/// <summary>
/// Request body for creating an activity.
/// </summary>
public sealed record CreateActivityRequest(
    ActivityType Type,
    string Subject,
    Guid PerformedById,
    DateTimeOffset PerformedAt,
    string? Description = null,
    Guid? ContactId = null,
    Guid? LeadId = null,
    int? DurationMinutes = null);

/// <summary>
/// Request body for updating an activity.
/// </summary>
public sealed record UpdateActivityRequest(
    ActivityType Type,
    string Subject,
    DateTimeOffset PerformedAt,
    string? Description = null,
    Guid? ContactId = null,
    Guid? LeadId = null,
    int? DurationMinutes = null);
