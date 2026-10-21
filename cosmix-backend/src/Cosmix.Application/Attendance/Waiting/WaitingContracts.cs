using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Waiting;

public static class WaitingPermissions
{
    public const string Pairs = "attendance.waiting.pairs.manage";
    public const string Submit = "attendance.waiting.submit";
    public const string Read = "attendance.waiting.read";
    public const string Decide = "attendance.waiting.decide";
    public const string EffectsPropose = "attendance.waiting.effects.propose";
    public const string EffectsDecide = "attendance.waiting.effects.decide";
    public const string EffectsRead = "attendance.waiting.effects.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Pairs, "Propose effective paired worker task dependencies", true, true, false),
        new(Submit, "Submit waiting facts and disputes within both workers' historical scopes", true, true, false),
        new(Read, "Read retained paired waiting evidence within all historical scopes", true, true, false),
        new(Decide, "Independently verify paired waiting using configured policy authority", true, true, false),
        new(EffectsPropose, "Propose a separate configured waiting interpretation", true, true, false),
        new(EffectsDecide, "Review an exact separate waiting effect proposal", true, true, false),
        new(EffectsRead, "Read separately approved waiting interpretation sources", true, true, false)
    ];
}

public sealed record WaitingInterval(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingPairInput(Guid WaitingEmployeeId, Guid CausingEmployeeId, Guid? SiteId,
    DateOnly EffectiveFrom, DateOnly EffectiveTo, [Required, StringLength(150)] string TaskDependencyReference,
    [Required, StringLength(4000)] string WrittenInstruction, [Required, StringLength(1000)] string Reason,
    Guid? PreviousPairId = null, string? ExpectedPreviousVersion = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWaitingPairRequest(WaitingPairInput Pair, [Required, StringLength(64)] string ExpectedSourceFingerprint);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingEvidenceInput(AdvancedEvidenceKind Kind, Guid SourceId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitWaitingRequest(Guid PairId, DateOnly BusinessDate, WaitingInterval ClaimedInterval,
    [Required] IReadOnlyList<WaitingInterval> ProductiveAlternateWork,
    [Required] IReadOnlyList<WaitingEvidenceInput> SupportingEvidence,
    [Required, StringLength(4000)] string WrittenEvidence, [Required, StringLength(1000)] string Reason,
    [Required, StringLength(64)] string ExpectedSourceFingerprint,
    Guid? PreviousRequestId = null, string? ExpectedPreviousVersion = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingReviewRequest([Required] string ExpectedVersion, ApprovalState Decision,
    [Required, StringLength(64)] string ExpectedProofFingerprint, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingDisputeRequest([Required] string ExpectedVersion,
    [Required, StringLength(4000)] string Statement, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingEffectRequest([Required] string ExpectedVersion,
    [Required, StringLength(64)] string ExpectedProofFingerprint, [Required, StringLength(1000)] string Reason);

// Public HTTP DTOs deliberately omit private storage references and exact calculation inputs.
public sealed record WaitingView(Guid Id, Guid RootId, int Revision, Guid WaitingEmployeeId,
    Guid CausingEmployeeId, DateOnly From, DateOnly To, string Kind, string State,
    string Version, Guid ApprovalRequestId, Guid? DecisionId, int? VerifiedMinutes, string Readiness);
public sealed record WaitingPreview(string State, string ProofFingerprint, int? VerifiedMinutes,
    int? CausalLossMinutes, IReadOnlyList<string> Reasons);
public sealed record WaitingDetail(WaitingView Record, string WrittenEvidence, IReadOnlyList<WaitingView> Revisions,
    IReadOnlyList<WaitingDisputeView> Disputes);
public sealed record WaitingDisputeView(Guid Id, Guid RequestId, Guid ActorAccountId, string Statement, DateTimeOffset AtUtc);
public sealed record WaitingEffectView(Guid Id, Guid RequestId, string State, string Version,
    WaitingEffect Effect, string MoneyState, decimal? Money, string? Currency, bool SettlementReady);

// Protected persistence contracts. Never bind these from an HTTP request.
public sealed record WaitingWorkerSource(Guid EmployeeId, DateOnly Date, OvertimeEmploymentSource? Employment,
    EffectiveRosterResponse Roster, IReadOnlyList<OvertimeSessionSource> Sessions,
    LeaveAttendanceContext Leave, AdvancedPolicyResolution Policy, IReadOnlyList<AttendanceResourceScope> Scopes);
public sealed record WaitingSource(WaitingWorkerSource Waiting, WaitingWorkerSource Causing, string Fingerprint);
public sealed record WaitingPairSnapshot(int Schema, Guid Id, Guid RootId, int Revision, Guid? PreviousId,
    Guid RequesterAccountId, WaitingPairInput Input, IReadOnlyList<WaitingSource> Sources);
public sealed record WaitingRequestSnapshot(int Schema, Guid Id, Guid RootId, int Revision, Guid? PreviousId,
    Guid RequesterAccountId, SubmitWaitingRequest Input, WaitingPairSnapshot Pair, Guid PairDecisionId,
    WaitingSource OriginalSource, string LossKey);
public sealed record WaitingPairDecision(int Schema, Guid PairId, string PreviewFingerprint,
    IReadOnlyList<WaitingSource> ReviewedSources, IReadOnlyList<AttendanceResourceScope> RetainedScopes);
public sealed record WaitingAttributionResult(IReadOnlyList<WaitingInterval> ProvenIntervals,
    long ProvenTicks, IReadOnlyList<WaitingInterval> GroupUnion, long PreviousLossTicks, long CurrentLossTicks, long DeltaTicks);
public sealed record WaitingDecisionProof(int Schema, int GroupSequence, Guid RequestId, Guid RootId, int Revision,
    string PreviewFingerprint, WaitingSource ReviewedSource, WaitingAttributionResult Attribution,
    long CapTicks, IReadOnlyList<Guid> GroupRequestIds, IReadOnlyList<WaitingDocumentProof> Documents,
    IReadOnlyList<AttendanceResourceScope> RetainedScopes);
public sealed record WaitingEffectSnapshot(int Schema, Guid Id, Guid RequestId, Guid FactualDecisionId,
    Guid RequesterAccountId, WaitingDecisionProof FactualProof, WaitingEffect Effect,
    decimal? ExactPoints, string? FinancialInterpretationReference, string MoneyState,
    IReadOnlyList<AttendanceResourceScope> RetainedScopes, string Fingerprint);
public sealed record WaitingEffectDecision(int Schema, Guid EffectId, Guid AssessmentId,
    string PreviewFingerprint, WaitingEffectSnapshot Proposal);
public sealed record WaitingStoredRecord
{
    public AuditEvent Audit { get; init; } = null!;
    public ApprovalRequest Approval { get; init; } = null!;
    public ApprovalDecision? Decision { get; init; }
    public int DecisionCount { get; init; }
    public WaitingStoredRecord() { }
    public WaitingStoredRecord(AuditEvent audit, ApprovalRequest approval, ApprovalDecision? decision, int decisionCount)
    { Audit=audit; Approval=approval; Decision=decision; DecisionCount=decisionCount; }
}
public sealed record WaitingDisputeSnapshot(int Schema, Guid Id, Guid RequestId, Guid RootId,
    int Revision, Guid ActorAccountId, string Statement, DateTimeOffset AtUtc,
    IReadOnlyList<AttendanceResourceScope> RetainedScopes);
public sealed record WaitingDocumentProof(Guid Id, Guid EmployeeId, string Fingerprint);

public interface IWaitingWorkflowRepository
{
    void RequireTransaction();
    Task<WaitingStoredRecord?> FindAsync(string kind, Guid id, bool tracked, CancellationToken ct);
    Task<IReadOnlyList<WaitingStoredRecord>> PairsAsync(Guid waitingEmployee, CancellationToken ct);
    Task<IReadOnlyList<WaitingStoredRecord>> LossAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<WaitingStoredRecord>> CausedAsync(Guid causingEmployee, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> DisputesAsync(Guid root, CancellationToken ct);
    Task<IReadOnlyList<WaitingStoredRecord>> EffectsAsync(Guid requestId, CancellationToken ct);
    Task<WaitingDocumentProof?> DocumentAsync(Guid id, Guid employeeId, DateOnly date, CancellationToken ct);
    Task<WaitingTimeEvidence?> VerifiedAsync(Guid approvalRequestId, CancellationToken ct);
    Task<PolicyImpactAssessment?> AssessmentAsync(Guid id, CancellationToken ct);
    void Add(ApprovalRequest approval, AuditEvent audit);
    void Add(AuditEvent audit);
    void Add(ApprovalDecision decision);
    void Add(WaitingTimeEvidence evidence);
    void Add(PolicyImpactAssessment assessment);
    void ExpectVersion(ApprovalRequest approval, byte[] expected);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record WaitingPerformancePoint(DateOnly BusinessDate, decimal ExactPoints,
    Guid RequestId, Guid WaitingEvidenceId, Guid AssessmentId, Guid PairId,
    Guid FactualApprovalRequestId, Guid FactualDecisionId, Guid EffectApprovalRequestId, Guid EffectDecisionId,
    Guid PolicyVersionId, IReadOnlyList<Guid> SourceIds, IReadOnlyList<AttendanceResourceScope> RetainedScopes);
public sealed record WaitingPerformanceEffects(string State, string ProofFingerprint,
    IReadOnlyList<WaitingPerformancePoint> Points, IReadOnlyList<AttendanceResourceScope> RetainedScopes,
    IReadOnlyList<string> Reasons);
public interface IWaitingPerformanceEffectReader
{
    // From inclusive, To exclusive; caller already owns authorization and transaction.
    Task<WaitingPerformanceEffects> ReadAsync(Guid causingEmployee, DateOnly from, DateOnly to, CancellationToken ct = default);
}
