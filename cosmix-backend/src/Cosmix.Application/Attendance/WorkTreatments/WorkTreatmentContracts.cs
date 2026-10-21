using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.PolicyEffects;

namespace Cosmix.Application.Attendance.WorkTreatments;

public enum WorkTreatmentKind { EarlyInstruction = 1, StudyInstruction = 2, EarlyCredit = 3, MakeUp = 4,
    CompanyTrip = 5, PersonalHomeTravel = 6, ContinuousDuty = 7, AllegedFakePunch = 8, AnnualLate = 9 }
public enum WorkCreditRole { EarlyCredit = 1, MakeUp = 2, NormalWorkTravel = 3, OrdinaryPay = 4, Overtime = 5, LeaveRecovery = 6 }
public enum WorkCycleEventKind { Break = 1, HomeVisit = 2 }
public enum WorkCaseOutcome { FactualFinding = 1, Unsubstantiated = 2, ReferredForFurtherReview = 3 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkTreatmentInterval(Guid SessionId, DateOnly BusinessDate, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkCycleEvent(WorkCycleEventKind Kind, DateTimeOffset StartUtc, DateTimeOffset EndUtc, Guid DocumentId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkInstruction(Guid WrittenDocumentId, string NamedInstruction, long? RequiredMakeUpTicks, DateOnly? FulfillmentDeadline);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkTreatmentRequest(WorkTreatmentKind Kind, DateOnly From, DateOnly To,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, string ExpectedSourceFingerprint, string SourceTicket,
    IReadOnlyList<WorkTreatmentInterval> Intervals, IReadOnlyList<Guid> DocumentIds,
    IReadOnlyList<WorkCycleEvent> CycleEvents, WorkInstruction? Instruction, Guid? InstructionRequestId,
    Guid? AnnualLateSourceId, Guid? ResubmissionOf, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideWorkTreatmentRequest(string ExpectedVersion, ApprovalState Decision,
    WorkCaseOutcome? CaseOutcome, IReadOnlyList<Guid> AddressedDisputeIds, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DisputeWorkTreatmentRequest(string ExpectedVersion, string Statement, IReadOnlyList<Guid> DocumentIds);

public sealed record WorkTreatmentSession(Guid Id, Guid Key, int Revision, DateOnly BusinessDate,
    DateTimeOffset StartUtc, DateTimeOffset? EndUtc, Guid? SiteId, Guid? BranchId, Guid ShiftVersionId,
    Guid? PolicyVersionId, Guid? AssignmentId, Guid? OfficeRosterId, Guid? GeofenceId, Guid? HolidayId,
    Guid? CheckInId, Guid? CheckOutId, Guid? CheckInAssessmentId, Guid? CheckOutAssessmentId,
    Guid? CorrectionId, Guid? CorrectionDecisionId, bool Verified, bool EndsContinuousDuty);
public sealed record WorkTreatmentEmployment(Guid AssignmentId, Guid CategoryId, string CategoryCode,
    Guid? SiteId, Guid? BranchId, DateOnly From, DateOnly? To);
public sealed record WorkTreatmentDocument(Guid Id, string Sha256, int Revision, string Version,
    DateTimeOffset VerifiedAt, string VerifiedBy);
public sealed record WorkTreatmentDay(DateOnly BusinessDate, WorkTreatmentEmployment? Employment,
    EffectiveRosterResponse Roster, AdvancedPolicyResolution Policy, IReadOnlyList<WorkTreatmentSession> Sessions, bool Locked);
public sealed record WorkTreatmentSource(Guid EmployeeId, WorkTreatmentKind Kind, DateOnly From, DateOnly To,
    string State, string Explanation, IReadOnlyList<WorkTreatmentDay> Days, IReadOnlyList<AttendanceResourceScope> Scopes,
    string Fingerprint) {
    public WorkAnnualLateEvidence? AnnualLate { get; init; }
}
public sealed record WorkTreatmentSourceResponse(WorkTreatmentSource Source, string SourceTicket, DateTimeOffset ExpiresAtUtc);
public sealed record WorkTreatmentTicket(Guid AccountId, Guid SessionId, Guid EmployeeId, WorkTreatmentKind Kind,
    DateOnly From, DateOnly To, string Fingerprint, DateTimeOffset ExpiresAtUtc);
public interface IWorkTreatmentProposalProtector {
    string Protect(WorkTreatmentTicket ticket);
    WorkTreatmentTicket Unprotect(string ticket);
}

public sealed record WorkCreditRange(Guid EmployeeId, Guid SourceId, WorkCreditRole Role, Guid SessionKey,
    Guid SessionId, DateOnly BusinessDate, DateTimeOffset StartUtc, DateTimeOffset EndUtc) {
    public long Ticks => checked(EndUtc.UtcTicks - StartUtc.UtcTicks);
}
public sealed record WorkContinuousCycle(DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ActualTicks,
    long OutsideRosterTicks, bool ResetReached, bool ExtraBoundaryReached, long NextDayRequiredTicks);
public sealed record WorkTreatmentResult(string State, long ExactTicks, bool TdaExcluded,
    StudyKpiTreatment? StudyKpiTreatment, IReadOnlyList<WorkCreditRange> Reservations,
    IReadOnlyList<WorkContinuousCycle> Cycles, string Explanation);
public sealed record WorkTreatmentView(Guid Id, Guid EmployeeId, WorkTreatmentKind Kind, ApprovalState State,
    string Version, DateTimeOffset SubmittedAtUtc, WorkTreatmentResult? ReviewedResult,
    IReadOnlyList<Guid> DisputeIds, Guid? ClosedDisciplinaryCaseId);
public sealed record WorkTreatmentDetail(WorkTreatmentView Request, WorkTreatmentRequest Proposal,
    WorkTreatmentSource RetainedSource, IReadOnlyList<WorkTreatmentDispute> Disputes);

// These are server-written private payloads, never HTTP input or generic JSON extension bags.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkTreatmentSubmission(int Schema, Guid Id, Guid EmployeeId, Guid RequesterAccountId,
    Guid RequesterSessionId, string SubmissionKey, DateTimeOffset SubmittedAtUtc, WorkTreatmentRequest Proposal,
    WorkTreatmentSource Source, IReadOnlyList<WorkTreatmentDocument> Documents);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkTreatmentDecision(int Schema, Guid Id, Guid EmployeeId, Guid OutcomeId,
    Guid ReviewerAccountId, Guid ReviewerEmployeeId, Guid ReviewerSessionId, string ReviewPermission, string PolicyFingerprint,
    string SourceFingerprint, ApprovalState State, WorkCaseOutcome? CaseOutcome, WorkTreatmentResult? Result,
    IReadOnlyList<Guid> AddressedDisputeIds, Guid? DisciplinaryCaseId, DateTimeOffset DecidedAtUtc);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkTreatmentDispute(int Schema, Guid Id, Guid RequestId, Guid EmployeeId,
    Guid AccountId, Guid SessionId, DateTimeOffset AtUtc, string Statement, IReadOnlyList<WorkTreatmentDocument> Documents);
public sealed record WorkTreatmentRow(ApprovalRequest Approval, ApprovalDecision? Decision);

/// <summary>Read inside a caller's authorized consistent transaction. Includes every approved reservation,
/// not only one session/source key or one credit role. This interface does not authorize recovery or pay.</summary>
public interface IWorkTreatmentCreditReader {
    Task<IReadOnlyList<WorkTreatmentCreditEvidence>> ReadAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<WorkStudyEvidence>> StudyAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<WorkContinuousEvidence>> ContinuousAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task CheckExclusiveAsync(Guid employeeId, IReadOnlyList<WorkCreditRange> proposed,
        IReadOnlyList<WorkCreditRange> otherVerifiedClaims, CancellationToken ct);
}
public sealed record WorkTreatmentCreditEvidence(Guid RequestId, Guid ApprovalId, Guid DecisionId, Guid ReviewerAccountId,
    string SourceFingerprint, string PolicyFingerprint, WorkCreditRange Range, Guid? ObligationRequestId) {
    public Guid PolicyVersionId { get; init; }
    public IReadOnlyList<AttendanceResourceScope> OriginalScopes { get; init; } = [];
    public WorkApprovedInstructionProof? InstructionProof { get; init; }
}
public sealed record WorkApprovedInstructionProof(Guid RequestId, Guid ApprovalId, Guid DecisionId, Guid EmployeeId,
    Guid ReviewerAccountId, WorkTreatmentKind Kind, DateOnly BusinessDate, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    string NamedWrittenInstruction, Guid PolicyVersionId, string SourceFingerprint, string PolicyFingerprint,
    Guid WrittenDocumentId, string WrittenDocumentSha256, IReadOnlyList<AttendanceResourceScope> OriginalScopes);
public sealed record WorkStudyEvidence(Guid RequestId, Guid ApprovalId, Guid DecisionId, Guid EmployeeId,
    DateTimeOffset ExcusedStartUtc, DateTimeOffset ExcusedEndUtc, long RequiredMakeUpTicks, DateOnly Deadline,
    Guid PolicyVersionId, string PolicyFingerprint, StudyMakeUpRules Rules, Guid WrittenDocumentId,
    string WrittenDocumentSha256, string SourceFingerprint) {
    public WorkApprovedInstructionProof? InstructionProof { get; init; }
}
/// <summary>Whole reviewed cycles, never clipped into a new calculation source. Caller covers all returned dates/scopes.</summary>
public sealed record WorkContinuousEvidence(Guid RequestId, Guid ApprovalId, Guid DecisionId, Guid EmployeeId,
    Guid ReviewerAccountId, DateOnly From, DateOnly To, Guid PolicyVersionId, string SourceFingerprint, string PolicyFingerprint,
    ContinuousDutyRules Rules, IReadOnlyList<WorkTreatmentInterval> Intervals, IReadOnlyList<WorkTreatmentSession> Sessions,
    IReadOnlyList<WorkCycleEvent> CycleEvents, IReadOnlyList<WorkTreatmentDocument> Documents,
    IReadOnlyList<WorkContinuousCycle> Cycles, IReadOnlyList<AttendanceResourceScope> OriginalScopes) {
    public IReadOnlyList<WorkContinuousTargetDay> TargetDays { get; init; } = [];
}
/// <summary>Target uses the ending actual interval's source business date, not a UTC/local midnight guess.
/// Boundary flags remain evidence; the consuming approved rule decides whether a treatment applies.</summary>
public sealed record WorkContinuousTargetDay(int CycleIndex, DateOnly SourceBusinessDate, DateOnly TargetBusinessDate,
    long NextDayRequiredTicks, bool ResetReached, bool ExtraBoundaryReached);
public sealed record WorkAnnualLateEvidence(Guid SourceId, Guid EmployeeId, DateOnly From, DateOnly To,
    int ExactLateCount, Guid PolicyVersionId, string PolicyFingerprint, string SourceFingerprint,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes);
/// <summary>SourceId is the actual pending C04 KPI approval-request ID. To is inclusive.
/// Parent C04 verifies its immutable private snapshot and current sources; no finalized score is needed.</summary>
public interface IWorkTreatmentAnnualLateReader {
    Task<WorkAnnualLateEvidence?> ReadAsync(Guid employeeId, Guid sourceId, CancellationToken ct);
}
public sealed record WorkTreatmentDisciplinaryOutcome(Guid RequestId, Guid ApprovalId, Guid DecisionId, Guid ClosedCaseId,
    Guid EmployeeId, Guid AnnualLateSourceId, string AnnualLateSourceFingerprint, string WorkSourceFingerprint,
    WorkCaseOutcome Outcome, Guid ReviewerAccountId, DateTimeOffset ReviewedAtUtc,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes);
/// <summary>Caller C04 owns live authorization and includes both returned original scopes and current scopes.
/// Requires an actual approved C06 closure for this exact pending KPI request and immutable source digest.</summary>
public interface IWorkTreatmentDisciplinaryOutcomeReader {
    Task<WorkTreatmentDisciplinaryOutcome?> ReadAsync(Guid employeeId, Guid annualLateSourceId,
        string annualLateSourceFingerprint, CancellationToken ct);
}

public interface IWorkTreatmentRepository {
    void RequireTransaction();
    Task<IReadOnlyList<WorkTreatmentSession>> SessionsAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<WorkTreatmentEmployment?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<WorkTreatmentDocument>> DocumentsAsync(Guid employeeId, IReadOnlyList<Guid> ids, DateOnly from, DateOnly to, CancellationToken ct);
    Task<bool> LockedAsync(DateOnly date, CancellationToken ct);
    Task<WorkTreatmentRow?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> FactsAsync(Guid employeeId, string operation, CancellationToken ct);
    Task<IReadOnlyList<WorkCreditRange>> ApprovedOvertimeAsync(Guid employeeId, CancellationToken ct);
    Task<DisciplinaryCase?> ClosedCaseAsync(Guid employeeId, Guid approvalId, Guid caseId, CancellationToken ct);
    void Add(ApprovalRequest approval);
    void Add(ApprovalDecision decision);
    void Add(AuditEvent fact);
    void Add(DisciplinaryCase closedCase);
    void ExpectVersion(ApprovalRequest approval, byte[] version);
    Task SaveAsync(CancellationToken ct);
}
