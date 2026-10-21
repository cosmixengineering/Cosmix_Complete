using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApproveAttendancePayrollFinancialRequest(
    [property: JsonRequired, Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [property: JsonRequired] DateOnly ExpectedTargetMonth,
    [property: JsonRequired] Guid? ExpectedPreviousAdjustmentId,
    [property: JsonRequired, Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record PostAttendancePayrollFinancialRequest(
    [property: JsonRequired, Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [property: JsonRequired] decimal SignedAmount,
    [property: JsonRequired, Required] string Currency,
    [property: JsonRequired] DateTimeOffset PostedAtUtc,
    [property: JsonRequired, Required, StringLength(100)] string ExternalSystem,
    [property: JsonRequired, Required, StringLength(200)] string ExternalReference,
    [property: JsonRequired, Required, StringLength(500)] string EvidenceReference,
    [property: JsonRequired, Required, StringLength(64, MinimumLength = 64)] string EvidenceSha256,
    [property: JsonRequired, Required, StringLength(4000)] string Statement,
    [property: JsonRequired, Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelAttendancePayrollFinancialRequest(
    [property: JsonRequired, Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [property: JsonRequired, Required, StringLength(1000)] string Reason);

public sealed record AttendancePayrollFinancialPreview(Guid EmployeeId, DateOnly SourceMonth,
    Guid SourceRevisionId, Guid ConfirmedPostingId, DateOnly TargetMonth, Guid TargetRevisionId,
    int ExpectedRevision, Guid? PreviousAdjustmentId, decimal ActuallyDeducted,
    decimal ConfirmedAdjustmentNet, decimal CorrectedDeduction, decimal SignedAmount,
    string Currency, string Direction, string PreviewFingerprint, bool CanApprove,
    IReadOnlyList<Guid> FactualAdjustmentIds, IReadOnlyList<AttendancePayrollInputLine> CorrectedLines,
    string LegalStatus = "activation_unavailable", bool PayrollReady = false, bool PaymentInstruction = false);

public sealed record AttendancePayrollFinancialResponse(Guid Id, int Revision,
    AttendancePayrollFinancialPreview Input, string Status, Guid ActorUserId, DateTimeOffset ApprovedAtUtc,
    Guid? ResolutionId, DateTimeOffset? PostedAtUtc, string? ExternalReference,
    string VerificationMode = "manual_authorized_attestation", bool PaymentInstruction = false);

/// <summary>Internal source boundary. Caller owns AttendanceOperation's transaction and current web authorization.</summary>
public interface IAttendancePayrollFinancialSourceReader
{
    Task<AttendancePayrollFinancialSources> ReadAsync(Guid employeeId, DateOnly sourceMonth,
        AttendanceActor actor, CancellationToken ct);
    Task<AttendancePayrollFinancialSources> ReadOpenTargetAsync(Guid employeeId, DateOnly sourceMonth,
        DateOnly targetMonth, AttendanceActor actor, CancellationToken ct);
}

public sealed record AttendancePayrollFinancialCorrection(Guid AdjustmentId, CarryForwardInterpretationSnapshot Evidence,
    IReadOnlyList<Guid>? PredecessorAdjustmentIds = null);
// Store DTOs, not EF entities with private setters/new-ID constructors, in protected JSON.
public sealed record AttendancePayrollFinancialRevision(Guid Id, Guid EmployeeId, DateOnly PeriodStart,
    int Revision, string InputFingerprint, string ProtectedEvidenceReference, DateTimeOffset CreatedAtUtc)
{
    public static AttendancePayrollFinancialRevision From(AttendancePayrollInputRevision row) =>
        new(row.Id, row.EmployeeId, row.PeriodStart, row.Revision, row.InputFingerprint, row.ProtectedEvidenceReference, row.CreatedAtUtc);
}
public sealed record AttendancePayrollFinancialSources(AttendancePayrollFinancialRevision Source,
    AttendancePayrollInputCalculation OriginalCalculation, AttendancePayrollAttestationResponse Posting,
    string PostingEvidenceFingerprint, AttendancePayrollFinancialRevision Target,
    IReadOnlyList<AttendancePayrollFinancialCorrection> Corrections,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes);

public sealed record AttendancePayrollFinancialHistoryRow(AttendancePayrollFinancialAdjustment Adjustment,
    AttendancePayrollFinancialResolution? Resolution);

public interface IAttendancePayrollFinancialRepository
{
    Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> HistoryAsync(Guid employeeId, DateOnly sourceMonth, CancellationToken ct);
    Task<bool> ReceiptExistsAsync(string receiptKey, CancellationToken ct);
    /// <summary>All bounded target rows, without employee filtering. The reader detects omitted population members.</summary>
    Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> TargetAsync(DateOnly targetMonth, CancellationToken ct);
    void Add(AttendancePayrollFinancialAdjustment adjustment);
    void Add(AttendancePayrollFinancialResolution resolution);
}

/// <summary>Positive signed amounts are recovery; negative amounts are credit. Already-posted entries
/// are retained evidence, never new amounts for another handoff. Status is approved or posted.
/// Cancelled rows are excluded. Approved zero is an exact no-change valuation, never a fabricated posting.
/// No legal/payment authority is conferred.</summary>
public sealed record AttendancePayrollFinancialMonthEntry(Guid AdjustmentId, Guid EmployeeId,
    DateOnly SourceMonth, DateOnly TargetMonth, int Revision, Guid SourceRevisionId, Guid ConfirmedPostingId,
    Guid TargetRevisionId, string TargetInputFingerprint, string PreviewFingerprint, string EvidenceFingerprint, string EvidenceReference,
    decimal SignedAmount, string Currency, string Status, Guid? PostingResolutionId,
    string? PostingEvidenceFingerprint, IReadOnlyList<Guid> FactualAdjustmentIds);

/// <summary>Transaction-local consumer boundary. Current session, input/attestation permissions and
/// original/current source and target scopes are checked; never nests AttendanceOperation.</summary>
public interface IAttendancePayrollFinancialMonthReader
{
    Task<IReadOnlyList<AttendancePayrollFinancialMonthEntry>> ReadAsync(DateOnly targetMonth,
        IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct);
}

/// <summary>Parent-owned lock consumer must compare exact immutable approval membership, including
/// evidence reference/hash, source/target identities, amount and factual correction identities.</summary>
public interface IAttendancePayrollFinancialTargetLockReader
{
    Task<bool> ContainsAsync(AttendancePayrollFinancialMonthEntry entry, AttendanceActor actor, CancellationToken ct);
}

internal sealed record AttendancePayrollFinancialEvidence(int SchemaVersion,
    AttendancePayrollFinancialPreview Preview, AttendancePayrollFinancialSources Sources,
    Guid ActorUserId, DateTimeOffset CreatedAtUtc, string Reason);
internal sealed record AttendancePayrollFinancialResolutionEvidence(int SchemaVersion, Guid AdjustmentId,
    string AdjustmentEvidenceFingerprint, string Status, Guid ActorUserId, DateTimeOffset CreatedAtUtc,
    PostAttendancePayrollFinancialRequest? Posting, string Reason);
