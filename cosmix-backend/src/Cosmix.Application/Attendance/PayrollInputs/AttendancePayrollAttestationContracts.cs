using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

public static class AttendancePayrollAttestationPermissions
{
    public const string Read = "attendance.payroll-attestations.read";
    public const string Attest = "attendance.payroll-attestations.attest";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read private manual payroll attestation evidence in every source scope", true, true, false),
        new(Attest, "Owner-authorized Accounts & Finance personnel only: manually attest handoff/posting/settlement evidence; no payment release", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CreateAttendancePayrollAttestationRequest(
    [property: JsonRequired] [Range(1, int.MaxValue)] int ExpectedSourceRevision,
    [property: JsonRequired] [Required, StringLength(64, MinimumLength = 64)] string ExpectedSourceFingerprint,
    [property: JsonRequired] Guid? ExpectedPreviousAttestationId,
    [property: JsonRequired] [Required] string Status,
    [property: JsonRequired] DateTimeOffset OccurredAtUtc,
    [property: JsonRequired] DateTimeOffset? PostedAtUtc,
    [property: JsonRequired] [Required, MaxLength(732)] IReadOnlyList<AttendancePayrollAttestationLine> Lines,
    [property: JsonRequired] [Required, StringLength(100)] string ExternalSystem,
    [property: JsonRequired] [Required, StringLength(200)] string ExternalReference,
    [property: JsonRequired] [Required, StringLength(500)] string EvidenceReference,
    [property: JsonRequired] [Required, StringLength(64, MinimumLength = 64)] string EvidenceSha256,
    [property: JsonRequired] [Required, StringLength(4000)] string Statement,
    [property: JsonRequired] [Required, StringLength(1000)] string Reason);

public sealed record AttendancePayrollAttestationResponse(Guid Id, Guid EmployeeId, DateOnly MonthStart,
    Guid SourceRevisionId, int SourceRevision, string SourceFingerprint, Guid? PreviousAttestationId,
    string Status, bool ConfirmsPosting, DateTimeOffset OccurredAtUtc, DateTimeOffset? PostedAtUtc,
    Guid ActorUserId, DateTimeOffset CreatedAtUtc, IReadOnlyList<AttendancePayrollAttestationLine> Lines,
    string ExternalSystem, string ExternalReference, string EvidenceReference, string EvidenceSha256,
    string Statement, string Reason, string VerificationMode = "manual_authorized_attestation",
    bool PayrollReady = false, bool PaymentInstruction = false);

public interface IAttendancePayrollAttestationRepository
{
    /// <summary>Called inside AttendanceOperation's serializable authorization transaction.</summary>
    Task<IReadOnlyList<AttendancePayrollAttestation>> HistoryAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct);
    Task<bool> ReceiptExistsAsync(string externalReceiptKey, int step, CancellationToken ct);
    void Add(AttendancePayrollAttestation attestation);
}

/// <summary>Internal consumer boundary, not an HTTP endpoint. Requires the same source-read,
/// attestation-read, MFA and current/original scopes as retained history. Null means no posting proof
/// for a saved or handed-off source, including a verified empty no-deduction handoff.
/// A returned receipt is a manual assertion only; it does not authorize credits or reconciliation.</summary>
public interface IAttendanceConfirmedPayrollPostingReader
{
    Task<AttendancePayrollAttestationResponse?> ReadConfirmedPostingAsync(Guid employeeId, DateOnly monthStart,
        Guid sourceRevisionId, Cosmix.Application.Authentication.AuthRequestContext context, CancellationToken ct);
}

internal sealed record AttendancePayrollAttestationEvidence(int SchemaVersion, Guid EmployeeId, DateOnly MonthStart,
    Guid SourceRevisionId, int SourceRevision, string SourceFingerprint, string SourceProtectedEvidenceReference,
    Guid? PreviousAttestationId, string Status, DateTimeOffset OccurredAtUtc, DateTimeOffset? PostedAtUtc,
    IReadOnlyList<AttendancePayrollAttestationLine> Lines, string ExternalSystem, string ExternalReference,
    string EvidenceReference, string EvidenceSha256, string Statement, string Reason,
    Guid ActorUserId, DateTimeOffset CreatedAtUtc, IReadOnlyList<AttendanceResourceScope> OriginalScopes);
