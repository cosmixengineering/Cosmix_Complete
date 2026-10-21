using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.HR.Organization;

namespace Cosmix.Application.HR.Organization;

public enum OrganizationMasterKind { Department, Designation, Branch, City, StaffCategory, EmploymentType }

public sealed class OrganizationListRequest
{
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Search { get; init; }
    public bool? IsActive { get; init; }
    [RegularExpression("^(code|name)$")] public string SortBy { get; init; } = "code";
    public bool Descending { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrganizationMasterRequest
{
    [Required, RegularExpression("^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$")] public string Code { get; init; } = "";
    [Required, StringLength(150)] public string Name { get; init; } = "";
    public Guid? CityId { get; init; }
    [StringLength(100)] public string? Province { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateOrganizationMasterRequest
{
    [Required, StringLength(150)] public string Name { get; init; } = "";
    public Guid? CityId { get; init; }
    [StringLength(100)] public string? Province { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SetOrganizationStatusRequest
{
    [Required] public bool? IsActive { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

public sealed record OrganizationMasterResponse(Guid Id, string Code, string Name, bool IsActive,
    string Version, Guid? CityId = null, string? CityName = null, string? Province = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class HolidayInput
{
    [Required] public DateOnly Date { get; init; }
    [Required, StringLength(200)] public string Name { get; init; } = "";
    [Required, StringLength(500)] public string NotificationReference { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateHolidayCalendarRequest
{
    [Required, RegularExpression("^[A-Za-z0-9][A-Za-z0-9_.-]{0,39}$")] public string Code { get; init; } = "";
    [Required, StringLength(200)] public string Name { get; init; } = "";
    [Required] public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    [Required, StringLength(500)] public string GovernmentNotificationReference { get; init; } = "";
    [Required, MaxLength(400)] public HolidayInput[] Holidays { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviseHolidayCalendarRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = "";
    [Required, StringLength(500)] public string GovernmentNotificationReference { get; init; } = "";
    [Required] public bool? IsActive { get; init; }
    [Required, MaxLength(400)] public HolidayInput[] Holidays { get; init; } = null!;
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

public sealed record HolidayResponse(Guid Id, DateOnly Date, string Name, string NotificationReference);
public sealed record HolidayCalendarSummary(Guid Id, string Code, string Name, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, int Revision, bool IsActive, Guid? SupersedesCalendarId, string Version);
public sealed record HolidayCalendarResponse(HolidayCalendarSummary Calendar,
    string GovernmentNotificationReference, IReadOnlyList<HolidayResponse> Holidays);
public sealed record OrganizationChangeResponse(Guid Id, string Operation, string Reason,
    DateTimeOffset OccurredAt, string ActorUserId, string ActorRoleCode, string? BeforeJson, string AfterJson);

public interface IOrganizationRepository
{
    Task<PagedResult<OrganizationMasterResponse>> ListAsync(OrganizationMasterKind kind, OrganizationListRequest request, CancellationToken ct);
    Task<OrganizationMasterResponse?> GetAsync(OrganizationMasterKind kind, Guid id, CancellationToken ct);
    Task<OrganizationMasterResponse> CreateAsync(OrganizationMasterKind kind, CreateOrganizationMasterRequest request, string? cityName, CancellationToken ct);
    Task<OrganizationMasterResponse> UpdateAsync(OrganizationMasterKind kind, Guid id, UpdateOrganizationMasterRequest request, string? cityName, CancellationToken ct);
    Task<OrganizationMasterResponse> SetStatusAsync(OrganizationMasterKind kind, Guid id, SetOrganizationStatusRequest request, CancellationToken ct);
    Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct);
    Task<OrganizationChange?> FindChangeAsync(Guid id, CancellationToken ct);
    Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(string resource, Guid id, OrganizationListRequest request, CancellationToken ct);
    void AddChange(OrganizationChange change, AuditEvent audit, IdempotencyReceipt receipt);
    Task SaveChangesAsync(CancellationToken ct);
    Task<PagedResult<HolidayCalendarSummary>> ListCalendarsAsync(OrganizationListRequest request, bool includeHistory, CancellationToken ct);
    Task<HolidayCalendar?> FindCalendarAsync(Guid id, CancellationToken ct);
    Task<bool> HasSuccessorAsync(Guid id, CancellationToken ct);
    Task<bool> CalendarOverlapsAsync(string code, DateOnly from, DateOnly? to, CancellationToken ct);
    Task<HolidayCalendarResponse> AddCalendarAsync(HolidayCalendar calendar, CancellationToken ct);
}
