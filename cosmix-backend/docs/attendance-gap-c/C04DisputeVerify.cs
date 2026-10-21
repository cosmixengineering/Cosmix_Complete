#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Policies;

// Execute only in the parent's released serial build slot, with process TEMP/artifacts on D:.
if (AccessPermissionCatalog.Find(KpiDisputePermissionDefs.Self)?.SelfService != true ||
    AccessPermissionCatalog.Find(KpiDisputePermissionDefs.Decide)?.Management != true)
    throw new Exception("Parent must register KpiDisputePermissionDefs.Definitions before this standalone verification.");
var checks = 0;
void Check(bool condition, string explanation) { if (!condition) throw new Exception(explanation); checks++; }
void SafeJson<T>(T result) {
    var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "ActorAccountId", "Requester", "RequesterAccountId", "OriginalScopes", "Scopes", "PrivateReference",
        "SourceAtSubmission", "SourceManifest", "VerifiedSource", "VerifiedFinal", "Authority", "RegisteredDeviceId", "SessionId", "Documents", "VerifiedBy"
    };
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    void Walk(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) {
            if (forbidden.Contains(property.Name)) throw new Exception("Unsafe self JSON property: " + property.Name);
            Walk(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Walk(item);
    }
    Walk(json.RootElement); checks++;
}
async Task Failure(Func<Task> action, int status, string code) {
    try { await action(); throw new Exception("Expected failure: " + code); }
    catch (AuthenticationFailure failure) { Check(failure.StatusCode == status && failure.Code == code, code); }
}
var f = new DisputeFixture();
var score = await f.Service.MyScoreAsync(f.From, f.Context, default);
SafeJson(score);
Check(AccessPermissionCatalog.All.Count(x => x.Code == KpiDisputePermissionDefs.Self) == 1 &&
    AccessPermissionCatalog.All.Count(x => x.Code == KpiDisputePermissionDefs.Decide) == 1, "Actual parent catalog registers each separate permission once.");
Check(score.ExactScore == 99.995m && score.DisplayScore == 100m && score.Revision == 1, "Exact finalized score is independent of display.");
var safe = JsonSerializer.Serialize(score);
Check(!safe.Contains("OriginalScopes") && !safe.Contains("ActorAccountId") && !safe.Contains("SourceManifest"), "No confidential final-reader fields in self DTO.");
Check(!typeof(CreateKpiDisputeRequest).GetProperties().Any(x => x.Name == "EmployeeId"), "No client employee selector in submission.");
try {
    JsonSerializer.Deserialize<CreateKpiDisputeRequest>("{\"EmployeeId\":\"" + Guid.NewGuid() + "\"}");
    throw new Exception("Unknown employee selector must be rejected.");
}
catch (JsonException) { checks++; }
var input = f.Input();
foreach (var selector in new[] { "employeeId", "accountId", "sessionId", "registeredDeviceId" }) {
    var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var body = JsonNode.Parse(JsonSerializer.Serialize(input, webOptions))!.AsObject();
    body[selector] = Guid.NewGuid().ToString();
    try { JsonSerializer.Deserialize<CreateKpiDisputeRequest>(body.ToJsonString(), webOptions); throw new Exception("Unknown identity selector accepted: " + selector); }
    catch (JsonException) { checks++; }
}
var submitted = await f.Service.SubmitAsync(input, "c04-submit-0001", f.Context, default);
SafeJson(submitted);
Check(submitted.State == "PendingReview" && submitted.Response is null && f.Rows.Count == 1 && f.Decisions.Count == 0, "Immutable draft is a review request, never a score decision.");
var snapshotReference = f.Rows.Single().SnapshotPrivateReference;
var replay = await f.Service.SubmitAsync(input, "c04-submit-0001", f.Context, default);
Check(replay.Id == submitted.Id && f.Rows.Count == 1, "Identical submission retry retains one draft.");
await Failure(() => f.Service.SubmitAsync(input with { Statement = "Changed statement" }, "c04-submit-0001", f.Context, default), 409, "idempotency_key_reused");
await Failure(() => f.Service.SubmitAsync(input, "c04-submit-0002", f.Context, default), 409, "kpi_dispute_duplicate_final_version");
await Failure(() => f.Service.SubmitAsync(input with { ExpectedFinalRevision = 9 }, "c04-submit-stale", f.Context, default), 409, "kpi_dispute_final_version_changed");
f.UseWeb(f.Owner, f.Employee);
await Failure(() => f.Service.ReviewDetailAsync(f.Employee, submitted.Id, f.Context, default), 403, "access_denied");
f.UseWeb(f.Owner, Guid.NewGuid());
await Failure(() => f.Service.RespondAsync(f.Employee, submitted.Id, new(submitted.Version, KpiDisputeOutcome.ReviewedNoChange, "Requester cannot decide"), "c04-review-own-01", f.Context, default), 403, "access_denied");
f.UseReviewer();
await Failure(() => f.Service.RespondAsync(f.Employee, submitted.Id, new(Convert.ToBase64String(new byte[8]), KpiDisputeOutcome.ReviewedNoChange, "Stale version"), "c04-review-stale", f.Context, default), 409, "stale_kpi_dispute_version");
var responded = await f.Service.RespondAsync(f.Employee, submitted.Id, new(submitted.Version, KpiDisputeOutcome.ReviewedNoChange, "Independent source matches retained final"), "c04-review-0001", f.Context, default);
Check(responded.State == "ReviewedNoChange" && f.Decisions.Count == 1 && f.ParentAudits.Count == 1, "Review does not create or replace finalized score.");
Check(f.Rows.Single().SnapshotPrivateReference == snapshotReference, "Draft reference is immutable after response.");
Check(f.Jobs.Count == 1, "Decision notification staged once.");
var notice = await NotificationOutbox.ReadAsync(f.Jobs.Single(), f.PrivateFiles, default);
Check(notice.EventId == f.Decisions.Single().Id && notice.RecipientAccountId == f.Owner &&
    notice.ResourceId == submitted.Id && notice.TemplateCode == NotificationTemplates.Decision, "Generic notification binds immutable decision/resource and actual requester.");
var replayResponse = await f.Service.RespondAsync(f.Employee, submitted.Id, new(submitted.Version, KpiDisputeOutcome.ReviewedNoChange, "Independent source matches retained final"), "c04-review-0001", f.Context, default);
Check(replayResponse.Response == responded.Response && f.Jobs.Count == 1 && f.Decisions.Count == 1, "Review retry appends neither a decision nor a notification.");
f.UseWeb(f.Owner, f.Employee);
await Failure(() => f.Service.RespondAsync(f.Employee, submitted.Id, new(submitted.Version, KpiDisputeOutcome.ReviewedNoChange, "Independent source matches retained final"), "c04-review-0001", f.Context, default), 403, "access_denied");
f.UseReviewer(); f.Grants.Clear();
await Failure(() => f.Service.RespondAsync(f.Employee, submitted.Id, new(submitted.Version, KpiDisputeOutcome.ReviewedNoChange, "Independent source matches retained final"), "c04-review-0001", f.Context, default), 403, "access_denied");
f.AllowAll(); f.Identity = f.Identity with { MfaSatisfied = false };
await Failure(() => f.Service.ReviewDetailAsync(f.Employee, submitted.Id, f.Context, default), 403, "access_denied");
f.UseSelf();
var own = await f.Service.MyDetailAsync(submitted.Id, f.Context, default);
SafeJson(own);
Check(own.Response?.Reason == responded.Response?.Reason && !JsonSerializer.Serialize(own).Contains("Actor"), "Own response projection contains reason without confidential actor proof.");
f.SessionCurrent = false;
await Failure(() => f.Service.MyDetailAsync(submitted.Id, f.Context, default), 401, "invalid_authentication");

var missing = new DisputeFixture(); missing.ParentAudits.Clear();
await Failure(() => missing.Service.MyScoreAsync(missing.From, missing.Context, default), 404, "kpi_finalized_score_unavailable");
var device = new DisputeFixture(); device.Device.Revoke("Synthetic revoked device", device.Clock.Now);
await Failure(() => device.Service.MyScoreAsync(device.From, device.Context, default), 401, "invalid_authentication");
var binding = new DisputeFixture(); DisputeFixture.Set(binding.MobileSession, "RegisteredDeviceId", Guid.NewGuid());
await Failure(() => binding.Service.MyScoreAsync(binding.From, binding.Context, default), 401, "invalid_authentication");
var deviceReplay = new DisputeFixture(); var deviceReplayInput = deviceReplay.Input();
await deviceReplay.Service.SubmitAsync(deviceReplayInput, "c04-device-replay", deviceReplay.Context, default);
deviceReplay.Device.Revoke("Synthetic replay revocation", deviceReplay.Clock.Now);
await Failure(() => deviceReplay.Service.SubmitAsync(deviceReplayInput, "c04-device-replay", deviceReplay.Context, default), 401, "invalid_authentication");
var securityVersion = new DisputeFixture(); DisputeFixture.Set(securityVersion.MobileSession, "SecurityVersion", 2);
await Failure(() => securityVersion.Service.MyScoreAsync(securityVersion.From, securityVersion.Context, default), 401, "invalid_authentication");
var expiredSession = new DisputeFixture(); DisputeFixture.Set(expiredSession.MobileSession, "ExpiresAt", expiredSession.Clock.Now);
await Failure(() => expiredSession.Service.MyScoreAsync(expiredSession.From, expiredSession.Context, default), 401, "invalid_authentication");
var ownerBinding = new DisputeFixture(); DisputeFixture.Set(ownerBinding.MobileSession, "UserAccountId", Guid.NewGuid());
await Failure(() => ownerBinding.Service.MyScoreAsync(ownerBinding.From, ownerBinding.Context, default), 401, "invalid_authentication");
var webSelf = new DisputeFixture(); webSelf.UseWeb(webSelf.Owner, webSelf.Employee);
await Failure(() => webSelf.Service.MyScoreAsync(webSelf.From, webSelf.Context, default), 403, "access_denied");
var expiry = new DisputeFixture(); expiry.Grants = expiry.Grants.Select(x => x with { ValidUntilUtc = expiry.Clock.Now }).ToList();
await Failure(() => expiry.Service.MyScoreAsync(expiry.From, expiry.Context, default), 403, "access_denied");
var unknownTiming = new DisputeFixture(); unknownTiming.Grants = unknownTiming.Grants.Select(x => x with { AuthorityTimingKnown = false }).ToList();
await Failure(() => unknownTiming.Service.MyScoreAsync(unknownTiming.From, unknownTiming.Context, default), 403, "access_denied");
var policy = new DisputeFixture(); policy.Source = policy.Source with { SourceManifest = null };
await Failure(() => policy.Service.SubmitAsync(policy.Input(), "c04-policy-0001", policy.Context, default), 409, "kpi_verified_calculation_required");
var storage = new DisputeFixture(); storage.FailWrites = true;
await Failure(() => storage.Service.SubmitAsync(storage.Input(), "c04-storage-0001", storage.Context, default), 503, "private_storage_unavailable");
Check(storage.Rows.Count == 0 && storage.Receipts.Count == 0, "Failed private draft storage produces no request/receipt.");
var race = new DisputeFixture(); race.OnWrite = purpose => {
    if (purpose.StartsWith("attendance:kpi:dispute:draft:")) race.Source = race.Source with { Fingerprint = new string('D', 64) };
};
await Failure(() => race.Service.SubmitAsync(race.Input(), "c04-race-0001", race.Context, default), 409, "kpi_dispute_source_changed");
Check(race.Rows.Count == 0 && race.Receipts.Count == 0, "Protected draft IO source race creates no accepted draft.");
var drift = new DisputeFixture(); var driftRequest = await drift.Service.SubmitAsync(drift.Input(), "c04-drift-0001", drift.Context, default);
drift.Source = drift.Source with { Fingerprint = new string('B', 64) }; drift.UseReviewer();
await Failure(() => drift.Service.RespondAsync(drift.Employee, driftRequest.Id, new(driftRequest.Version, KpiDisputeOutcome.ReviewedNoChange, "Changed source"), "c04-drift-0002", drift.Context, default), 409, "kpi_dispute_source_changed");
Check(drift.Decisions.Count == 0 && drift.Jobs.Count == 0, "Source drift never silently becomes no-change proof.");
var driftReturned = await drift.Service.RespondAsync(drift.Employee, driftRequest.Id,
    new(driftRequest.Version, KpiDisputeOutcome.Returned, "Source changed; refresh the immutable claim"), "c04-drift-0003", drift.Context, default);
Check(driftReturned.State == "Returned" && driftReturned.Response?.SourceChangedSinceSubmission == true,
    "A returned response retains actual changed source proof without reaching a score conclusion.");
drift.UseSelf();
var refreshed = await drift.Service.SubmitAsync(drift.Input() with { ResubmissionOf = driftRequest.Id,
    ExpectedReturnedVersion = driftReturned.Version }, "c04-drift-0004", drift.Context, default);
Check(refreshed.ResubmissionOf == driftRequest.Id && drift.Rows.Count == 2, "Source drift can be recovered with a new linked immutable draft.");
var reviewExpiry = new DisputeFixture(); var expiringRequest = await reviewExpiry.Service.SubmitAsync(reviewExpiry.Input(), "c04-review-exp-1", reviewExpiry.Context, default);
reviewExpiry.UseReviewer(); reviewExpiry.Grants = reviewExpiry.Grants.Select(x => x with { ValidUntilUtc = reviewExpiry.Clock.Now.AddSeconds(1) }).ToList();
reviewExpiry.OnWrite = purpose => { if (purpose.StartsWith("attendance:kpi:dispute:response:")) reviewExpiry.Clock.Now = reviewExpiry.Clock.Now.AddSeconds(2); };
await Failure(() => reviewExpiry.Service.RespondAsync(reviewExpiry.Employee, expiringRequest.Id,
    new(expiringRequest.Version, KpiDisputeOutcome.ReviewedNoChange, "Expiry during protected proof IO"), "c04-review-exp-2", reviewExpiry.Context, default), 403, "access_denied");
Check(reviewExpiry.Decisions.Count == 0 && reviewExpiry.Jobs.Count == 0 && reviewExpiry.Rows.Single().State == ApprovalState.Pending,
    "Expired reviewer authority after protected IO cannot commit a response or notification.");
var notificationRace = new DisputeFixture(); var notificationRaceRequest = await notificationRace.Service.SubmitAsync(notificationRace.Input(), "c04-notice-race1", notificationRace.Context, default);
notificationRace.UseReviewer(); notificationRace.OnWrite = purpose => {
    if (purpose.StartsWith("attendance:notification:")) notificationRace.Source = notificationRace.Source with { Fingerprint = new string('E', 64) };
};
await Failure(() => notificationRace.Service.RespondAsync(notificationRace.Employee, notificationRaceRequest.Id,
    new(notificationRaceRequest.Version, KpiDisputeOutcome.CorrectionRequired, "Source race during generic notification IO"), "c04-notice-race2", notificationRace.Context, default), 409, "kpi_dispute_source_changed");
Check(notificationRace.Decisions.Count == 0 && notificationRace.Jobs.Count == 0 && notificationRace.Receipts.Count == 1 &&
    notificationRace.Rows.Single().State == ApprovalState.Pending && notificationRace.ParentAudits.Count == 1,
    "Notification IO source race rolls back staged response/job and creates no score or review receipt.");
var notificationFailure = new DisputeFixture(); var notificationFailureRequest = await notificationFailure.Service.SubmitAsync(notificationFailure.Input(), "c04-notice-fail1", notificationFailure.Context, default);
notificationFailure.UseReviewer(); notificationFailure.OnWrite = purpose => {
    if (purpose.StartsWith("attendance:notification:")) throw new AuthenticationFailure("private_storage_unavailable", 503);
};
await Failure(() => notificationFailure.Service.RespondAsync(notificationFailure.Employee, notificationFailureRequest.Id,
    new(notificationFailureRequest.Version, KpiDisputeOutcome.Returned, "Private generic notification failure"), "c04-notice-fail2", notificationFailure.Context, default), 503, "private_storage_unavailable");
Check(notificationFailure.Decisions.Count == 0 && notificationFailure.Jobs.Count == 0 && notificationFailure.Rows.Single().State == ApprovalState.Pending,
    "Outbox private failure cannot leave a committed-looking response.");
var scoped = new DisputeFixture(); var scopedRequest = await scoped.Service.SubmitAsync(scoped.Input(), "c04-scoped-0001", scoped.Context, default);
scoped.UseReviewer(); var newSite = Guid.NewGuid();
scoped.CurrentScopes = [new([new(EmployeeId: scoped.Employee, SiteId: newSite)])];
scoped.Grants = [scoped.Grant(KpiDisputePermissionDefs.Decide, AccessScopeKind.Site, newSite)];
await Failure(() => scoped.Service.ReviewListAsync(scoped.Employee, new(scoped.From), scoped.Context, default), 403, "access_denied");
Check(scoped.PageReads == 0, "Original annual scope denial precedes count/page reads.");
scoped.Grants.Add(scoped.Grant(KpiDisputePermissionDefs.Decide, AccessScopeKind.Site, scoped.Site));
var scopedPage = await scoped.Service.ReviewListAsync(scoped.Employee, new(scoped.From), scoped.Context, default);
Check(scopedPage.TotalCount == 1, "Current plus original scope coverage admits the bounded queue.");
scoped.UseSelf();
await Failure(() => scoped.Service.MyListAsync(new(scoped.From, 1, 51), scoped.Context, default), 400, "invalid_kpi_dispute_page");
var supplementalScope = new DisputeFixture(); var relatedEmployee = Guid.NewGuid(); var relatedSite = Guid.NewGuid();
var completeScopes = supplementalScope.Source.Scopes.Concat(new[] { new AttendanceResourceScope([new(EmployeeId: relatedEmployee, SiteId: relatedSite)]) }).ToArray();
supplementalScope.Source = supplementalScope.Source with { Scopes = completeScopes, Fingerprint = KpiDisputeRules.Hash(completeScopes) };
supplementalScope.CurrentScopes = completeScopes;
var supplementalRequest = await supplementalScope.Service.SubmitAsync(supplementalScope.Input(), "c04-related-0001", supplementalScope.Context, default);
SafeJson(supplementalRequest);
Check(!JsonSerializer.Serialize(supplementalRequest).Contains(relatedEmployee.ToString()), "Other employee evidence scope is absent from self JSON.");
supplementalScope.UseReviewer(); supplementalScope.Grants = [supplementalScope.Grant(KpiDisputePermissionDefs.Decide, AccessScopeKind.Site, supplementalScope.Site)];
await Failure(() => supplementalScope.Service.ReviewDetailAsync(supplementalScope.Employee, supplementalRequest.Id, supplementalScope.Context, default), 403, "access_denied");
supplementalScope.Grants.Add(supplementalScope.Grant(KpiDisputePermissionDefs.Decide, AccessScopeKind.Site, relatedSite));
var supplementalProof = await supplementalScope.Service.ReviewDetailAsync(supplementalScope.Employee, supplementalRequest.Id, supplementalScope.Context, default);
Check(supplementalProof.SourceAtSubmission.Scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId == relatedEmployee),
    "Complete related source scopes are retained and independently authorized.");

var docs = new DisputeFixture(); var document = docs.AddDocument();
var documentRequest = await docs.Service.SubmitAsync(docs.Input() with { EvidenceDocumentIds = [document.Id] }, "c04-docs-0001", docs.Context, default);
Check(documentRequest.EvidenceDocumentIds.SequenceEqual([document.Id]), "Actual scoped verified private document accepted.");
document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, Guid.NewGuid().ToString(), "Synthetic withdrawn evidence", docs.Clock.Now);
docs.UseReviewer();
await Failure(() => docs.Service.RespondAsync(docs.Employee, documentRequest.Id, new(documentRequest.Version, KpiDisputeOutcome.CorrectionRequired, "Withdrawn evidence"), "c04-docs-0002", docs.Context, default), 409, "kpi_dispute_verified_documents_required");
var untrusted = new DisputeFixture();
await Failure(() => untrusted.Service.SubmitAsync(untrusted.Input() with { EvidenceDocumentIds = [Guid.NewGuid()] }, "c04-docs-bad-01", untrusted.Context, default), 409, "kpi_dispute_verified_documents_required");

var returned = new DisputeFixture(); var returnedInput = returned.Input();
var first = await returned.Service.SubmitAsync(returnedInput, "c04-return-0001", returned.Context, default);
returned.UseReviewer();
var back = await returned.Service.RespondAsync(returned.Employee, first.Id, new(first.Version, KpiDisputeOutcome.Returned, "Clarify the retained written claim"), "c04-return-0002", returned.Context, default);
returned.UseSelf();
await Failure(() => returned.Service.SubmitAsync(returnedInput, "c04-return-0003", returned.Context, default), 409, "kpi_dispute_returned_request_required");
var amendedStatement = await returned.Service.SubmitAsync(returnedInput with { ResubmissionOf = first.Id,
    ExpectedReturnedVersion = back.Version, Statement = "Clarified synthetic statement" }, "c04-return-0004", returned.Context, default);
Check(amendedStatement.ResubmissionOf == first.Id && returned.Rows.Count == 2 && returned.Decisions.Count == 1, "Returned response retains its old draft and links a new one.");

var correction = new DisputeFixture(); var correctionRequest = await correction.Service.SubmitAsync(correction.Input(), "c04-correct-0001", correction.Context, default);
correction.UseReviewer();
var correctionResponse = await correction.Service.RespondAsync(correction.Employee, correctionRequest.Id,
    new(correctionRequest.Version, KpiDisputeOutcome.CorrectionRequired, "Correct actual source through the existing KPI workflow"), "c04-correct-0002", correction.Context, default);
Check(correctionResponse.State == "CorrectionRequired" && correctionResponse.AmendedFinalizationId is null && correction.ParentAudits.Count == 1, "CorrectionRequired stays reviewable without a score mutation.");
await Failure(() => correction.Service.LinkAmendmentAsync(correction.Employee, correctionRequest.Id,
    new(correctionResponse.Version, correction.Final.Id, "Original final is not an amendment"), "c04-correct-0003", correction.Context, default), 409, "kpi_dispute_verified_successor_required");
var previous = correction.Final;
correction.Source = correction.MakeSource(.015m);
correction.SeedFinal(previous, correction.Clock.Now);
var linked = await correction.Service.LinkAmendmentAsync(correction.Employee, correctionRequest.Id,
    new(correctionResponse.Version, correction.Final.Id, "Verified parent KPI successor"), "c04-correct-0004", correction.Context, default);
Check(linked.State == "AmendmentReviewed" && linked.AmendedFinalizationId == correction.Final.Id && correction.ParentAudits.Count == 2,
    "Only parent's manifest-backed independently finalized direct successor is linked.");
var linkReplay = await correction.Service.LinkAmendmentAsync(correction.Employee, correctionRequest.Id,
    new(correctionResponse.Version, correction.Final.Id, "Verified parent KPI successor"), "c04-correct-0004", correction.Context, default);
Check(linkReplay.AmendedFinalizationId == linked.AmendedFinalizationId && correction.Audits.Count(x => x.EventType == KpiDisputeRules.AmendmentEvent) == 1, "Link retry appends one history event.");
correction.UseWeb(correction.Owner, correction.Employee);
await Failure(() => correction.Service.LinkAmendmentAsync(correction.Employee, correctionRequest.Id,
    new(correctionResponse.Version, correction.Final.Id, "Verified parent KPI successor"), "c04-correct-0004", correction.Context, default), 403, "access_denied");
Console.WriteLine($"C04 disputes: {checks} checks passed using actual AttendanceOperation, FinalizedKpiReader, dispute service/evidence and NotificationOutbox with synthetic adapters. Not SQL, HTTP, encrypted-storage or delivery acceptance.");

public class DisputeProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var result = Create<T, DisputeProxy>(); ((DisputeProxy)(object)result).Handler = handler; return result;
    }
}
public sealed class DisputeClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class DisputeFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Owner = Guid.NewGuid(), Site = Guid.NewGuid(), PolicyId = Guid.NewGuid(), AssignmentId = Guid.NewGuid();
    public readonly DateOnly From = new(2025, 7, 1);
    public readonly DisputeClock Clock = new();
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-c04-dispute");
    public AuthenticatedAccount Identity;
    public RegisteredDevice Device;
    public UserSession MobileSession;
    public bool SessionCurrent = true, FailWrites;
    public int Depth, PageReads;
    public Action<string>? OnWrite;
    public KpiSource Source;
    public FinalizedKpiScore Final = null!;
    public IReadOnlyList<AttendanceResourceScope> CurrentScopes;
    public List<PermissionGrant> Grants = [];
    public List<ApprovalRequest> Rows = [], ParentRows = [];
    public List<ApprovalDecision> Decisions = [], ParentDecisions = [];
    public List<AuditEvent> Audits = [], ParentAudits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<OutboxMessage> Jobs = [];
    public List<EmployeeDocument> Documents = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Stored = [];
    private Dictionary<Guid, ApprovalState> saved = [];
    public IPrivateFileStore PrivateFiles;
    public KpiDisputeService Service;
    public DisputeFixture() {
        Device = new(Owner, new string('A', 64), "android", "Synthetic bound device", Clock.Now.AddDays(-2));
        Device.MarkVerified(Clock.Now.AddDays(-1));
        MobileSession = (UserSession)RuntimeHelpers.GetUninitializedObject(typeof(UserSession));
        Set(MobileSession, "Id", Guid.NewGuid()); Set(MobileSession, "UserAccountId", Owner); Set(MobileSession, "Client", SessionClient.Mobile);
        Set(MobileSession, "RegisteredDeviceId", Device.Id); Set(MobileSession, "SecurityVersion", 1);
        Set(MobileSession, "CreatedAt", Clock.Now.AddHours(-1)); Set(MobileSession, "ExpiresAt", Clock.Now.AddDays(1));
        Identity = new(Owner, Employee, MobileSession.Id, 1, SessionClient.Mobile, false);
        Source = MakeSource(.005m); CurrentScopes = Source.Scopes; AllowAll(); SeedFinal(null, Clock.Now.AddDays(-1));
        PrivateFiles = DisputeProxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") {
                if (FailWrites) throw new AuthenticationFailure("private_storage_unavailable", 503);
                OnWrite?.Invoke((string)args[1]!);
                return Task.FromResult(Put((byte[])args[0]!, (string)args[1]!));
            }
            var reference = (PrivateFileReference)args[0]!; var stored = Stored[reference.StorageKey];
            if (stored.Purpose != (string)args[1]! || stored.Bytes.LongLength != reference.SizeBytes ||
                Convert.ToHexString(SHA256.HashData(stored.Bytes)) != reference.Sha256) throw new AuthenticationFailure("private_evidence_unavailable", 409);
            return Task.FromResult(stored.Bytes.ToArray());
        });
        var authorization = DisputeProxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticScopedKpiRole"), _ => throw new Exception(method.Name)
        });
        var journal = DisputeProxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => Add(Audits, (AuditEvent)args[0]!),
            "SaveChangesAsync" => Save(), _ => throw new Exception(method.Name)
        });
        var current = DisputeProxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = DisputeProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authentication = DisputeProxy.Make<IAuthenticationRepository>((method, args) => method.Name switch {
            "GetSessionAsync" => Task.FromResult<UserSession?>((Guid)args[0]! == MobileSession.Id ? MobileSession : null),
            "GetDeviceAsync" => Task.FromResult<RegisteredDevice?>((Guid)args[0]! == Owner && (Guid)args[1]! == Device.Id ? Device : null),
            _ => throw new Exception(method.Name)
        });
        var operation = new AttendanceOperation(current, access, authorization, journal, authentication, Clock, PrivateFiles);
        var parent = DisputeProxy.Make<IKpiRepository>((method, args) => method.Name switch {
            "FinalizationsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(ParentAudits.Where(x => x.EmployeeScopeId == (Guid)args[0]! &&
                x.TargetId == AttendanceKpiService.Target((Guid)args[0]!, (DateOnly)args[1]!)).ToArray()),
            "FindAsync" => Task.FromResult(ParentRows.SingleOrDefault(x => x.Id == (Guid)args[1]! && x.SubjectId == (Guid)args[0]!) is { } row ?
                new KpiRequestRow(row, ParentDecisions.Where(x => x.ApprovalRequestId == row.Id).ToArray()) : null), _ => throw new Exception(method.Name)
        });
        var repository = DisputeProxy.Make<IKpiDisputeRepository>((method, args) => method.Name switch {
            "RequireTransaction" => Require(),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Id == (Guid)args[1]! && x.SubjectId == (Guid)args[0]!) is { } row ?
                new KpiDisputeRow(row, Decisions.Where(x => x.ApprovalRequestId == row.Id).ToArray()) : null),
            "YearAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Year((Guid)args[0]!, (DateOnly)args[1]!, (Guid?)args[2], (Guid?)args[3]).ToArray()),
            "PageAsync" => Page((Guid)args[0]!, (KpiDisputeListRequest)args[1]!, (Guid?)args[2], (Guid?)args[3]),
            "EvidenceAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.EmployeeScopeId == (Guid)args[0]! &&
                x.TargetType == KpiDisputeRules.TargetType && x.TargetId == ((Guid)args[1]!).ToString() && x.EventType == (string)args[2]!).Take(2).ToArray()),
            "DocumentsAsync" => Task.FromResult<IReadOnlyList<EmployeeDocument>>(Documents.Where(x => x.EmployeeId == (Guid)args[0]! && ((IReadOnlyList<Guid>)args[1]!).Contains(x.Id)).ToArray()),
            "Add" => args[0] switch { ApprovalRequest row => Add(Rows, row), ApprovalDecision decision => Add(Decisions, decision), AuditEvent audit => Add(Audits, audit), _ => throw new Exception("Unexpected write") },
            "ExpectVersion" => null, "SaveChangesAsync" => Save(), _ => throw new Exception(method.Name)
        });
        var sources = DisputeProxy.Make<IKpiSourceReader>((method, _) => method.Name switch {
            "ReadAsync" => Task.FromResult(Source), "ScopesAsync" => Task.FromResult(CurrentScopes), _ => throw new Exception(method.Name)
        });
        var summaries = DisputeProxy.Make<IAttendanceSummaryRepository>((method, _) => method.Name == "ResourceScopesAsync" ? Task.FromResult(CurrentScopes) : throw new Exception(method.Name));
        var jobs = DisputeProxy.Make<IDurableJobRepository>((method, args) => method.Name switch {
            "RequireProducerTransaction" => Require(),
            "FindByIdentityAsync" => Task.FromResult(Jobs.SingleOrDefault(x => x.MessageType == (string)args[0]! && x.DeduplicationKey == (string)args[1]!)),
            "RecipientExistsAsync" => Task.FromResult((Guid)args[0]! == Owner),
            "Stage" => Add(Jobs, (OutboxMessage)args[0]!), _ => throw new Exception(method.Name)
        });
        Service = new(operation, repository, new FinalizedKpiReader(parent, operation), sources, summaries,
            new(repository, operation, PrivateFiles), new(current, access, authentication, journal, Clock), PrivateFiles, new NotificationOutbox(jobs, PrivateFiles, Clock), Clock);
    }
    public void UseSelf() => Identity = new(Owner, Employee, MobileSession.Id, 1, SessionClient.Mobile, false);
    public void UseWeb(Guid account, Guid employee) => Identity = new(account, employee, Guid.NewGuid(), 1, SessionClient.Web, true);
    public void UseReviewer() => UseWeb(Guid.NewGuid(), Guid.NewGuid());
    public PermissionGrant Grant(string permission, AccessScopeKind scope, Guid? target) => new(permission, permission == KpiDisputePermissionDefs.Decide, Guid.NewGuid(), scope, target) {
        AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddDays(-2), ValidUntilUtc = Clock.Now.AddDays(1)
    };
    public void AllowAll() => Grants = KpiDisputePermissionDefs.Definitions.Select(x => Grant(x.Code, AccessScopeKind.All, null)).ToList();
    public CreateKpiDisputeRequest Input() => new(From, Final.Id, Final.Revision, Final.SourceFingerprint,
        KpiDisputeReason.AttendanceEvidence, "Synthetic written statement", "Synthetic reason");
    public KpiSource MakeSource(decimal waiting) {
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SITE"],
            new("SYNTHETIC-C04-DISPUTE", "attendance.kpi.decide", true, [AdvancedEvidenceKind.AcceptedDuty]),
            Kpi: new(100, 7, 2, AdvancedRounding.ToEven, 0, 100, KpiLateBasis.PercentOfBaseScore,
                .8m, .5m, KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var days = Enumerable.Range(0, From.AddYears(1).DayNumber - From.DayNumber).Select(i =>
            new KpiDayInput(From.AddDays(i), Guid.NewGuid(), new string('A', 64), false, null, null, null, i == 0 ? waiting : null)).ToArray();
        var input = new KpiInput(rules, From, From.AddYears(1), days, []);
        var manifest = new KpiSourceManifest(AttendanceKpiCalculation.Version, input, days.Select(x =>
            new KpiSavedDay(x.Date, x.SummaryId, 1, "synthetic-saved-calculation", x.SourceFingerprint,
                PolicyId, Guid.NewGuid(), PolicyId, [AssignmentId], new string('C', 64))).ToArray(), null);
        return new(Employee, From, From.AddYears(1), PolicyId, AttendanceKpiCalculation.Calculate(input),
            KpiDisputeRules.Hash(manifest), [new([new(EmployeeId: Employee, SiteId: Site)])], [AssignmentId]) {
            PolicyFingerprint = new string('C', 64), SourceManifest = manifest
        };
    }
    public void SeedFinal(FinalizedKpiScore? previous, DateTimeOffset at) {
        var requester = Guid.NewGuid(); var actor = Guid.NewGuid();
        var snapshot = new KpiRequestSnapshot(1, Employee, requester, From.AddYears(1).AddDays(-1), Source, previous?.Id);
        var reference = AttendanceOperation.Reference(Put(JsonSerializer.SerializeToUtf8Bytes(snapshot), AttendanceKpiService.RequestPurpose(Employee)));
        var row = new ApprovalRequest(AttendanceKpiService.RequestType, Employee, requester.ToString(), reference, 1, at.AddMinutes(-1));
        var decision = row.Decide(ApprovalState.Approved, actor.ToString(), "SyntheticIndependentKpiReviewer", "Synthetic approved exact source", at, reference);
        ParentRows.Add(row); ParentDecisions.Add(decision);
        Final = new(1, Guid.NewGuid(), Employee, From, From.AddYears(1), (previous?.Revision ?? 0) + 1, previous?.Id,
            row.Id, decision.Id, PolicyId, Source.Calculation.ExactScore!.Value, Source.Calculation.DisplayScore!.Value,
            Source.Calculation.Fingerprint, Source.Fingerprint, actor, at, Source.Scopes);
        var finalReference = AttendanceOperation.Reference(Put(JsonSerializer.SerializeToUtf8Bytes(Final), AttendanceKpiService.FinalPurpose(Employee, From)));
        ParentAudits.Add(new(AttendanceKpiService.FinalizedEvent, actor.ToString(), decision.ActorRoleCode,
            AttendanceKpiService.TargetType, AttendanceKpiService.Target(Employee, From), at, Context.CorrelationId, Employee, finalReference));
    }
    public EmployeeDocument AddDocument() {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Synthetic scoped documentary evidence");
        var reference = AttendanceOperation.Reference(Put(bytes, $"employee/{Employee:N}/document"));
        var document = new EmployeeDocument(Employee, EmployeeDocumentType.Other, reference, "synthetic.txt", "text/plain",
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), Owner.ToString(), Clock.Now.AddDays(-2));
        document.RecordScan(DocumentScanStatus.Clean, true, "synthetic-scanner", Clock.Now.AddDays(-1));
        document.RecordVerification(DocumentVerificationStatus.Verified, Guid.NewGuid().ToString(), "Synthetic verified private evidence", Clock.Now.AddDays(-1));
        Set(document, "RowVersion", BitConverter.GetBytes(1L)); Documents.Add(document); return document;
    }
    private PrivateFileReference Put(byte[] bytes, string purpose) {
        var id = Guid.NewGuid().ToString("N"); Stored[id] = (purpose, bytes.ToArray());
        return new(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length);
    }
    private IEnumerable<ApprovalRequest> Year(Guid employee, DateOnly from, Guid? requester, Guid? excluded) => Rows.Where(x =>
        x.SubjectId == employee && x.RequestType.StartsWith(KpiDisputeRules.YearPrefix(from), StringComparison.Ordinal) &&
        (!requester.HasValue || x.RequesterUserId == requester.ToString()) && (!excluded.HasValue || x.RequesterUserId != excluded.ToString()));
    private Task<PagedResult<ApprovalRequest>> Page(Guid employee, KpiDisputeListRequest request, Guid? requester, Guid? excluded) {
        PageReads++; var rows = Year(employee, request.YearFrom, requester, excluded).OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).ToArray();
        return Task.FromResult(new PagedResult<ApprovalRequest>(rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(), rows.Length, request.Page, request.PageSize));
    }
    private Task Save() {
        foreach (var row in Rows) {
            if (!saved.TryGetValue(row.Id, out var state) || state != row.State) {
                var revision = row.RowVersion.Length == 8 ? BitConverter.ToInt64(row.RowVersion) + 1 : 1;
                Set(row, "RowVersion", BitConverter.GetBytes(revision)); saved[row.Id] = row.State;
            }
        }
        return Task.CompletedTask;
    }
    private object? Require() { if (Depth == 0) throw new Exception("No authorized transaction"); return null; }
    private static object? Add<T>(List<T> target, T value) { target.Add(value); return null; }
    public static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) {
        if (Depth != 0) throw new Exception("Nested authorization transaction");
        var counts = new[] { Rows.Count, Decisions.Count, Audits.Count, Receipts.Count, Jobs.Count };
        var states = Rows.Select(x => (Row: x, x.State, x.ClosedAt, Version: x.RowVersion.ToArray())).ToArray();
        var savedBefore = new Dictionary<Guid, ApprovalState>(saved); Depth++;
        try { return await action(ct); }
        catch {
            Rows.RemoveRange(counts[0], Rows.Count - counts[0]); Decisions.RemoveRange(counts[1], Decisions.Count - counts[1]);
            Audits.RemoveRange(counts[2], Audits.Count - counts[2]); Receipts.RemoveRange(counts[3], Receipts.Count - counts[3]); Jobs.RemoveRange(counts[4], Jobs.Count - counts[4]);
            foreach (var state in states) { Set(state.Row, "State", state.State); Set(state.Row, "ClosedAt", state.ClosedAt); Set(state.Row, "RowVersion", state.Version); }
            saved = savedBefore; throw;
        }
        finally { Depth--; }
    }
}
