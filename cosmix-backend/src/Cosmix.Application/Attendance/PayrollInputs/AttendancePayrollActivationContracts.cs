using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.PayrollInputs;

public static class AttendancePayrollActivationPermissions
{
    public const string Read = "attendance.payroll-activation.read";
    public const string Request = "attendance.payroll-activation.request";
    public const string Approve = "attendance.payroll-activation.approve";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read exact-source company payroll activation evidence; no legal certification", true, true, false),
        new(Request, "Request company operational payroll activation for an exact saved source with declared documentary evidence", true, true, false),
        new(Approve, "Independently approve company operational payroll activation; cannot approve own request or own payroll", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestAttendancePayrollActivation(
    [property: JsonRequired] [Range(1, int.MaxValue)] int ExpectedSourceRevision,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedSourceFingerprint,
    [property: JsonRequired] Guid? ExpectedLatestRequestId,
    [Required, StringLength(500)] string EvidenceReference,
    [Required, StringLength(64, MinimumLength = 64)] string EvidenceSha256,
    [Required, StringLength(4000, MinimumLength = 20)] string Statement,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideAttendancePayrollActivation(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [property: JsonRequired] [Range(1, 3)] int Decision,
    [Required, StringLength(1000)] string Reason);

public sealed record AttendancePayrollActivationBinding(Guid EmployeeId, DateOnly MonthStart,
    Guid SourceRevisionId, int SourceRevision, string SourceFingerprint, string SourceEvidenceSha256,
    string CalculationVersion, DateOnly SourceFrom, DateOnly SourceTo, string CoverageFingerprint,
    IReadOnlyList<Guid> PolicyVersionIds, IReadOnlyList<AttendancePayrollJurisdictionContext> Jurisdictions);
public sealed record AttendancePayrollActivationRequestView(Guid Id, string Version, string State,
    Guid RequesterAccountId, DateTimeOffset RequestedAtUtc, string EvidenceReference, string EvidenceSha256,
    string Statement, string Reason, Guid? ReviewerAccountId, DateTimeOffset? DecidedAtUtc, string? DecisionReason);
public sealed record AttendancePayrollActivationStatus(AttendancePayrollActivationBinding Binding,
    bool SourceCurrent, string State, Guid? LatestRequestId, string? ActivationFingerprint,
    IReadOnlyList<AttendancePayrollActivationRequestView> Requests,
    bool LegalCertification = false, bool PaymentInstruction = false);

public sealed record AttendancePayrollActivationRow(ApprovalRequest Request, IReadOnlyList<ApprovalDecision> Decisions);
public interface IAttendancePayrollActivationRepository
{
    Task<IReadOnlyList<AttendancePayrollActivationRow>> HistoryAsync(Guid sourceRevisionId, CancellationToken ct);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveAsync(CancellationToken ct);
}

internal sealed record AttendancePayrollActivationRequestEvidence(int SchemaVersion,
    AttendancePayrollActivationBinding Binding, Guid RequesterAccountId, DateTimeOffset RequestedAtUtc,
    string EvidenceReference, string EvidenceSha256, string Statement, string Reason);
internal sealed record AttendancePayrollActivationDecisionEvidence(int SchemaVersion, Guid RequestId,
    string RequestEvidenceSha256, string BindingFingerprint, int Decision, Guid ReviewerAccountId,
    Guid ReviewerEmployeeId, string ReviewerRoleCode, DateTimeOffset DecidedAtUtc, string Reason);
