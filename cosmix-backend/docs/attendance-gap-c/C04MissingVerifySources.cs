#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
var h = new MissingLeaveSources(); await h.InitializeAsync();
var original = await h.MakeAsync(LeaveKind.Emergency, null, []);
h.Original = original;
var absent = await h.Reader.ReadAsync(h.Employee, original.Request.Id, h.Date, default);
Check(absent.State == "eligible_for_review" && absent.Duration == "half_day", "real typed emergency allocation and protected empty evidence produce explicit half-day eligibility");
Check(absent.LeaveAllocationId == original.Request.Days.Single().Id && absent.LeaveApprovalId == original.Approval!.Id &&
    absent.PolicyVersionId == h.Policy.PolicyVersionId, "actual source uses real leave/allocation/approval and published policy identities");
var oldFingerprint = absent.Fingerprint;
var corrected = await h.MakeAsync(LeaveKind.Emergency, original.Request.Id,
    [new(LeaveEvidenceKind.Emergency, "evidence.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7 synthetic document"))]);
h.Successor = corrected; h.Original = original with { SupersededByRequestId = corrected.Request.Id };
var changed = await h.Reader.ReadAsync(h.Employee, original.Request.Id, h.Date, default);
Check(changed.State == "withdrawal_required" && changed.Fingerprint != oldFingerprint && changed.EvidenceFingerprint != absent.EvidenceFingerprint &&
    changed.RelatedSourceIds?.Contains(corrected.Request.Id) == true && changed.RelatedSourceIds?.Contains(corrected.Approval!.Id) == true,
    "authoritative returned-to-corrected link retains actual new document submission identities and requires explicit withdrawal review");
Check(KpiMissingEvidenceEvidenceReader.RevisionCount(changed) == 0, "positively verified correction is reviewable zero, not an unknown-source default");
var provided = await h.Reader.ReadAsync(h.Employee, corrected.Request.Id, h.Date, default);
Check(provided.State == "evidence_present" && provided.EvidenceFingerprint != absent.EvidenceFingerprint,
    "actual scanned protected emergency document is downloaded and verified before evidence-present success");
try { corrected.Request.AttachEvidence("client-asserted"); throw new Exception("Submitted evidence was mutable"); }
catch (InvalidOperationException) { checks++; }
var stored = (await h.Evidence.ReadAsync(corrected.Request, default)).Single();
var bytes = h.Files[stored.File.StorageKey]; h.Files[stored.File.StorageKey] = (bytes.Purpose, Encoding.ASCII.GetBytes("%PDF-corrupted"));
try { await h.Reader.ReadAsync(h.Employee, original.Request.Id, h.Date, default); throw new Exception("Corrupt correction document accepted"); }
catch (AuthenticationFailure failure) { Check(failure.Code == "leave_evidence_unavailable", "actual new document hash mismatch fails closed before withdrawal"); }
h.Files[stored.File.StorageKey] = bytes;
h.Original = original with { SupersededByRequestId = Guid.NewGuid() };
var unlinked = await h.Reader.ReadAsync(h.Employee, original.Request.Id, h.Date, default);
Check(unlinked.State == "pending" && unlinked.Issues.Contains("leave_correction_link_provenance_required"), "unknown correction cannot authorize zero");
h.Original = original;
var retainedPolicy = h.Policy;
h.Policy = h.Policy with { Rules = h.Policy.Rules! with { Kpi = h.Policy.Rules!.Kpi! with { MissingEvidenceEligibility = null } } };
var unspecified = await h.Reader.ReadAsync(h.Employee, original.Request.Id, h.Date, default);
Check(unspecified.State == "pending" && unspecified.Issues.Contains("kpi_missing_evidence_eligibility_configuration_required"), "actual source never invents eligibility when the typed published setting is absent");
h.Policy = retainedPolicy;
var other = await h.MakeAsync(LeaveKind.Other, null, []); h.Original = other;
var unknown = await h.Reader.ReadAsync(h.Employee, other.Request.Id, h.Date, default);
Check(unknown.State == "pending" && unknown.Issues.Contains("leave_typed_current_eligibility_required"), "Other is not guessed medical or emergency from names or codes");
var medical = await h.MakeAsync(LeaveKind.Sick, null,
    [new(LeaveEvidenceKind.Medical, "medical.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7 synthetic medical"))]);
h.Original = medical;
var medicalSource = await h.Reader.ReadAsync(h.Employee, medical.Request.Id, h.Date, default);
Check(medicalSource.State == "evidence_present", "real typed medical evidence is positively verified, not inferred from HasEvidence");
Console.WriteLine($"PASS: {checks} C04 actual typed leave/evidence/provenance/summary source assertions; in-memory IO, no SQL or host.");

public class MissingSourceProxy : DispatchProxy {
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var value = Create<T, MissingSourceProxy>(); ((MissingSourceProxy)(object)value).Handler = handler; return value;
    }
}
public sealed class MissingLeaveSources {
    public Guid Employee = Guid.NewGuid(), Requester = Guid.NewGuid(), LeaveReviewer = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 13);
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public LeaveRow? Original, Successor;
    public AdvancedPolicyResolution Policy;
    public KpiMissingEvidenceSourceReader Reader;
    public LeaveEvidence Evidence;
    private readonly LeaveProvenance provenance;
    private readonly IPrivateFileStore files;
    private readonly AttendanceSummaryService summaries;
    private DailyAttendanceSummary? saved;
    private readonly EffectiveRosterResponse roster;
    private readonly DateTimeOffset at = new(2026, 10, 13, 2, 0, 0, TimeSpan.Zero);
    public MissingLeaveSources() {
        files = MissingSourceProxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") {
                var bytes = (byte[])args[0]!; var key = Guid.NewGuid().ToString("N"); Files[key] = ((string)args[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var item = Files[((PrivateFileReference)args[0]!).StorageKey];
            if (item.Purpose != (string)args[1]!) throw new Exception("Private source purpose mismatch"); return Task.FromResult(item.Bytes);
        });
        T NoIO<T>() where T : class => MissingSourceProxy.Make<T>((method, _) => throw new Exception("Forbidden boundary " + method.Name));
        var operation = new AttendanceOperation(NoIO<ICurrentAuthentication>(), NoIO<IAccountAccessReader>(), NoIO<IAccessManagementRepository>(),
            NoIO<ISchedulingJournalRepository>(), NoIO<IAuthenticationRepository>(), TimeProvider.System, files);
        Evidence = new(operation, files, MissingSourceProxy.Make<IEmployeeFileScanner>((_, _) => Task.FromResult(new FileScanResult(DocumentScanStatus.Clean, "synthetic-scanner-v1"))));
        provenance = new(operation, files);
        var resourceScopes = new AttendanceResourceScope[] { new([new(EmployeeId: Employee, DepartmentId: Guid.NewGuid())]) };
        var summaryRepo = MissingSourceProxy.Make<IAttendanceSummaryRepository>((method, _) => method.Name switch {
            "IsLockedAsync" => Task.FromResult(false), "LatestAsync" => Task.FromResult(saved),
            "SessionsAsync" => Task.FromResult<IReadOnlyList<WorkSession>>([]), "ExceptionCountAsync" => Task.FromResult(0),
            "ResourceScopesAsync" => Task.FromResult<IReadOnlyList<AttendanceResourceScope>>(resourceScopes), _ => throw new Exception(method.Name)
        });
        roster = new(Employee, Date, "scheduled", "Asia/Karachi", ShiftVersionId: Guid.NewGuid(),
            StartsAt: at.AddHours(1), EndsAt: at.AddHours(9), ConfiguredScheduledMinutes: 480);
        summaries = new(operation, summaryRepo, MissingSourceProxy.Make<IIndividualRosterResolver>((_, _) => Task.FromResult(roster)),
            new UnavailableAttendancePolicyCalculator(), files);
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["OFFICE"],
            new("Synthetic independently approved explicit settings", KpiPermissions.Decide, false, []),
            Kpi: new(100, 1, 2, AdvancedRounding.ToEven, 0, 100, KpiLateBasis.PercentagePoints, 0.8m, 0.5m,
                KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, true, 1) {
                Composition = KpiCompositionStrategy.Additive,
                MissingEvidenceEligibility = new([KpiMissingCaseSetting.MedicalSickLeave, KpiMissingCaseSetting.EmergencyHalfDay],
                    KpiMissingAssessmentSetting.AtIndependentReview, KpiMissingDurationSetting.DatedRosterWindow, [ApprovalState.Pending, ApprovalState.Returned])
            });
        var publication = AdvancedAttendanceRulesCodec.Validate(rules);
        if (!publication.IsValid) throw new Exception("Invalid typed publication fixture");
        Policy = new("configured", Employee, AdvancedAttendanceFamily.Kpi, Date, Guid.NewGuid(), [Guid.NewGuid()],
            AdvancedAttendanceRulesCodec.Parse(publication.CanonicalJson).Rules, new string('A', 64));
        var leaves = MissingSourceProxy.Make<ILeaveRepository>((method, args) => method.Name switch {
            "FindAsync" => Task.FromResult(Original?.Request.Id == (Guid)args[0]! ? Original : Successor?.Request.Id == (Guid)args[0]! ? Successor : null),
            "OverlappingAsync" => Task.FromResult<IReadOnlyList<LeaveRow>>(Original is null ? [] : [Original!]), _ => throw new Exception(method.Name)
        });
        var repository = MissingSourceProxy.Make<IKpiMissingEvidenceRepository>((method, args) => method.Name switch {
            "RequireTransaction" => null, "AccountEmployeeAsync" => Task.FromResult<Guid?>((Guid)args[0]! == Requester ? Employee : Guid.NewGuid()),
            _ => throw new Exception(method.Name)
        });
        Reader = new(leaves, Evidence, provenance, summaries, summaryRepo,
            MissingSourceProxy.Make<IAdvancedAttendancePolicyReader>((_, _) => Task.FromResult(Policy)), repository, operation);
    }
    public async Task InitializeAsync() {
        var day = await summaries.LoadAuthorizedDayAsync(Employee, Date, default);
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { day.Facts, day.Roster, day.Policy, day.ExceptionCount })));
        saved = new(Employee, Date, 1, AttendanceDayStatus.Unverified, null, roster.ShiftVersionId, "synthetic-factual-source", digest,
            480, 0, null, null, null, 0, at);
    }
    public async Task<LeaveRow> MakeAsync(LeaveKind kind, Guid? previous, IReadOnlyList<LeaveAttachmentRequest> attachments) {
        var type = new LeaveType("SYNTHETIC", "Synthetic typed leave", kind == LeaveKind.Sick, false, kind == LeaveKind.Sick, kind);
        LeaveRules.Evidence(type, attachments);
        var leave = new LeaveRequest(Employee, type.Id, Date, Date, 240, "Synthetic source", null, null, null, null,
            LeavePayTreatment.PendingReview, null, Requester.ToString(), at, previousRequestId: previous);
        leave.Days.Add(new(leave.Id, Date, at.AddHours(1), at.AddHours(5), roster.ShiftVersionId));
        leave.AttachEvidence(await Evidence.StoreAsync(leave.Id, Employee, attachments, default));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Requester.ToString(), await provenance.FreezeAsync(leave, default), 1, at);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Returned, LeavePayTreatment.PendingReview, null,
            LeaveProvenance.SubmissionDigest(leave), null, null, null, null, null);
        var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id), default));
        var decision = approval.Decide(ApprovalState.Returned, LeaveReviewer.ToString(), "Admin", "Synthetic returned correction", at, reference);
        return new(leave, type, approval, [decision], [], null);
    }
}
