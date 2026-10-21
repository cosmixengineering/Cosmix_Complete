using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.EmployeeAssignments;

namespace Cosmix.Application.Attendance.SiteReports;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitSiteReportRequest(Guid SiteId, SiteReportKind Kind,
    [Required, StringLength(4000)] string Notes,
    [StringLength(200)] string? TaskReference = null,
    DateTimeOffset? DeviceCapturedAtUtc = null,
    decimal? Latitude = null, decimal? Longitude = null, decimal? AccuracyMeters = null,
    bool IsOffline = false, SiteReportAttachmentRequest? Attachment = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SiteReportAttachmentRequest(
    [Required, StringLength(180)] string FileName,
    [Required, StringLength(100)] string ContentType,
    [Required, MinLength(1), MaxLength(SiteReportRules.MaximumAttachmentBytes)] byte[] Content);

public sealed record SiteReportListRequest(DateOnly From, DateOnly To,
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public sealed record SiteReportQueueListRequest(
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public sealed record SiteReportQueueSiteResponse(Guid SiteId, string Code, string Name, bool IsActive);

public sealed record SiteReportSummaryResponse(Guid Id, Guid EmployeeId, Guid SiteId,
    SiteReportKind Kind, string? TaskReference, DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? DeviceCapturedAtUtc, bool IsOffline, string SubmissionStatus,
    string EvidenceStatus, bool HasAttachment, int ReviewSequence, int ResponseSequence = 0);

public sealed record SiteReportReviewResponse(int Sequence, SiteReportReviewOutcome Outcome,
    string Reviewer, string ReviewerRole, string Reason, DateTimeOffset ReviewedAtUtc, Guid? Id = null);

public sealed record SiteReportDetailResponse(SiteReportSummaryResponse Report,
    Guid EmployeeAssignmentId, Guid RegisteredDeviceId, string Notes,
    decimal? Latitude, decimal? Longitude, decimal? AccuracyMeters,
    string ReviewWorkflowStatus, IReadOnlyList<SiteReportReviewResponse> Reviews,
    IReadOnlyList<SiteReportEmployeeResponse>? Responses = null);

/// <summary>Current retained detail, independently of the original frozen submission receipt.</summary>
public sealed record SiteReportReconciliationResponse(string ResultKind, SiteReportDetailResponse Detail);

public sealed record SiteReportEvidenceDownload(byte[] Content, string FileName, string ContentType);

/// <summary>Module reads are bounded and scoped before pagination. No update/delete of received reports.</summary>
public interface ISiteReportRepository
{
    Task<SiteReportAuthor?> FindAuthorAsync(Guid accountId, Guid deviceId, CancellationToken ct);
    Task<IReadOnlyList<SiteReportAssignment>> AssignmentsAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<SiteReportReadModel?> FindAsync(Guid reportId, Guid? employeeId, Guid? siteId, CancellationToken ct);
    Task<SiteReportReadModel?> FindSubmissionAsync(Guid employeeId, Guid accountId, string key, CancellationToken ct);
    Task<PagedResult<SiteReportReadModel>> ListAsync(Guid? employeeId, Guid? siteId,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, SiteReportListRequest request, CancellationToken ct);
    Task<PagedResult<SiteReportQueueSiteResponse>> QueueSitesAsync(bool allSites, IReadOnlyList<Guid> siteIds,
        SiteReportQueueListRequest request, CancellationToken ct);
    Task<bool> HasSubmissionKeyAsync(Guid employeeId, string key, CancellationToken ct);
    void Add(SiteReport report);
}

public sealed record SiteReportAuthor(UserAccount Account, RegisteredDevice Device);
public sealed record SiteReportAssignment(EmployeeAssignment Assignment, bool SiteActive,
    DateOnly? SiteStartsOn, DateOnly? SiteEndsOn);
public sealed record SiteReportReadModel(SiteReport Report, IReadOnlyList<SiteReportReview> Reviews,
    IReadOnlyList<SiteReportResponse>? Responses = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideSiteReportRequest([property: JsonRequired] int ExpectedReviewSequence,
    [property: JsonRequired] int ExpectedResponseSequence, [property: JsonRequired] SiteReportReviewOutcome Outcome,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RespondToSiteReportRequest([property: JsonRequired] int ExpectedReviewSequence,
    [property: JsonRequired] int ExpectedResponseSequence, [property: JsonRequired] Guid ReturnedReviewId,
    [Required, StringLength(4000)] string WrittenResponse, SiteReportAttachmentRequest? Attachment = null);

public sealed record SiteReportEmployeeResponse(Guid Id, int Sequence, Guid ReturnedReviewId,
    DateTimeOffset SubmittedAtUtc, string WrittenResponse, bool HasAttachment);

public interface ISiteReportReviewRepository
{
    void Add(SiteReportReview review);
    void Add(SiteReportResponse response);
}
