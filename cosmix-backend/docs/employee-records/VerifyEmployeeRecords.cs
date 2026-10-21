// Standalone synthetic executable. Compile in TEMP with project references; no test project or live SQL/provider is used.
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Authentication.Providers;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

var f = new Fixture();
var s = f.Service();
int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks++; }
async Task Fails(string code, Func<Task> action) { try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; } throw new Exception("Expected " + code); }
void Invalid(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception("Expected rejected domain transition"); }
var v = Fixture.Version;
var assetIssue = new RequestEmployeeRecord(v, EmployeeRecordAction.Issue, null, f.Now, "Recorded delivery",
    Asset: new("DEVICE-001", "Good", f.AssetEvidence.Id));
await Fails("employee_record_policy_not_configured", () => f.Service(new()).RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue, "absent-policy-001", f.Context, default));
Check(f.Records.Requests.Count == 0, "missing operational authority creates no request");
f.Identity = f.Identity with { MfaSatisfied = false };
await Fails("access_denied", () => s.AssetsAsync(f.Employee.Id, false, null, new(), f.Context, default));
f.Identity = f.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails("access_denied", () => s.AssetsAsync(f.Employee.Id, false, null, new(), f.Context, default));
f.Identity = f.Identity with { Client = SessionClient.Web };
f.ExcludedPermission = EmployeeRecordPermissions.AssetsRequest;
await Fails("access_denied", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue, "no-permission-001", f.Context, default));
f.ExcludedPermission = null;
var request = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue, "asset-issue-001", f.Context, default);
var requestAgain = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue, "asset-issue-001", f.Context, default);
Check(request.RecordRequestId == requestAgain.RecordRequestId && f.Records.Requests.Count == 1, "identical request replay creates one request");
await Fails("employee_record_request_pending", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue, "asset-pending-002", f.Context, default));
await Fails("employee_version_conflict", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue with { ExpectedEmployeeVersion = Convert.ToBase64String(new byte[] { 1,0,0,0,0,0,0,0 }) }, "asset-version-002", f.Context, default));
await Fails("idempotency_key_reused", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetIssue with { Reason = "Changed" }, "asset-issue-001", f.Context, default));
var decide = new DecideEmployeeRecord(v, v, ApprovalState.Approved, "Delivery evidence checked");
await Fails("access_denied", () => s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, request.RecordRequestId!.Value, decide, "self-review-001", f.Context, default));
f.Reviewer(); f.FixedAdmin = false;
await Fails("access_denied", () => s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, request.RecordRequestId!.Value, decide, "nonadmin-review-001", f.Context, default));
f.FixedAdmin = true;
var issued = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, request.RecordRequestId!.Value, decide, "asset-decision-001", f.Context, default);
var issuedAgain = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, request.RecordRequestId!.Value, decide, "asset-decision-001", f.Context, default);
Check(issued.EmployeeRecordId == issuedAgain.EmployeeRecordId && f.Records.Assets.Count == 1 && f.Records.AssetEvents.Count == 1, "issue retry retains one custody/event");
f.CurrentAuthorized = false;
await Fails("access_denied", () => s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, request.RecordRequestId!.Value, decide, "asset-decision-001", f.Context, default));
f.CurrentAuthorized = true; f.Requester();
await Fails("asset_already_in_custody", () => s.RequestAsync(f.Target.Id, EmployeeRecordKind.Asset, assetIssue with { Asset = assetIssue.Asset! with { EvidenceDocumentId = f.TargetEvidence.Id } }, "double-issue-001", f.Context, default));
var transfer = new RequestEmployeeRecord(v, EmployeeRecordAction.Transfer, issued.EmployeeRecordId, f.Now, "Custodian transfer",
    Asset: new(null, "Returned good", f.AssetEvidence.Id, f.Receiver.Id, f.Target.Id, v, f.TargetEvidence.Id, "Received good"));
var tr = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, transfer, "asset-transfer-001", f.Context, default);
f.Reviewer(); var transferred = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Asset, tr.RecordRequestId!.Value, decide, "asset-transfer-decision-001", f.Context, default);
Check(f.Records.Assets.Count == 2 && f.Records.Assets.Single(a => a.EmployeeId == f.Employee.Id).ReturnedAt == f.Now &&
    f.Records.AssetEvents.Count == 3 && f.Records.AssetEvents.Last().TransferredFromCustodyId == issued.EmployeeRecordId,
    "transfer retains issue, one return and receiving evidence");
Check((await s.AssetsAsync(f.Employee.Id, false, null, new(), f.Context, default)).TotalCount == 0 &&
    (await s.AssetsAsync(f.Target.Id, false, null, new(), f.Context, default)).TotalCount == 1, "source and receiver current custody are factual");
f.Requester();
var assetReturn = new RequestEmployeeRecord(v, EmployeeRecordAction.Return, issued.EmployeeRecordId, f.Now, "Second return", Asset: new(null, "Good", f.AssetEvidence.Id, f.Receiver.Id));
await Fails("asset_already_returned_or_date_conflict", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset, assetReturn, "double-return-001", f.Context, default));
var medical = f.Document(f.Employee.Id, EmployeeDocumentType.Other, null, null, EmployeeDocumentPrivacy.Medical);
await Fails("employee_record_evidence_unavailable", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset,
    assetIssue with { Asset = new("DEVICE-002", "Good", medical.Id) }, "medical-evidence-001", f.Context, default));
await Fails("employee_record_evidence_unavailable", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset,
    assetIssue with { Asset = new("DEVICE-002", "Good", f.TargetEvidence.Id) }, "foreign-evidence-001", f.Context, default));
f.Store.Corrupt = true;
await Fails("employee_record_evidence_unavailable", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset,
    assetIssue with { Asset = new("DEVICE-002", "Good", f.AssetEvidence.Id) }, "corrupt-evidence-001", f.Context, default));
f.Store.Corrupt = false;

var simIssue = new RequestEmployeeRecord(v, EmployeeRecordAction.Issue, null, f.Now, "Company SIM delivery",
    Sim: new(f.AssetEvidence.Id, "SIM-001", "+92 300 1234567", "tier-a"));
await Fails("invalid_employee_sim_credit_tier", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim,
    simIssue with { Sim = simIssue.Sim! with { CreditTierCode = "UNAPPROVED" } }, "sim-tier-denied-001", f.Context, default));
await Fails("employee_sim_credit_policy_not_configured", () => f.Service(fields: new()).RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim, simIssue, "sim-policy-absent-001", f.Context, default));
var sr = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim, simIssue, "sim-issue-001", f.Context, default);
f.Reviewer(); var si = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Sim, sr.RecordRequestId!.Value, decide, "sim-decision-001", f.Context, default);
Check(f.Records.Sims.Single().PhoneNumber == "+923001234567" && f.Records.Sims.Single().AllowedCreditTierCode == "TIER-A", "SIM normalizes phone and approved tier spelling");
f.Requester();
await Fails("sim_or_phone_already_in_custody", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim,
    simIssue with { Sim = simIssue.Sim! with { SimReference = "SIM-002" } }, "sim-duplicate-phone-001", f.Context, default));
var simTransfer = new RequestEmployeeRecord(v, EmployeeRecordAction.Transfer, si.EmployeeRecordId, f.Now, "SIM transfer",
    Sim: new(f.AssetEvidence.Id, CreditTierCode: "TIER-A", ReceiverAccountId: f.Receiver.Id, TargetEmployeeId: f.Target.Id,
        ExpectedTargetVersion: v, TargetEvidenceDocumentId: f.TargetEvidence.Id));
var str = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim, simTransfer, "sim-transfer-001", f.Context, default);
f.Reviewer(); await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Sim, str.RecordRequestId!.Value, decide, "sim-transfer-decision-001", f.Context, default);
Check(f.Records.Sims.Count == 3 && f.Records.Sims[0].Status == CompanySimStatus.Active && f.Records.Sims[1].Status == CompanySimStatus.Returned &&
    f.Records.Sims[1].SupersedesEmployeeSimAssignmentId == si.EmployeeRecordId && f.Records.Sims[2].TransferredFromAssignmentId == f.Records.Sims[1].Id,
    "SIM transfer preserves original, successor return and receiving assignment");
Check((await s.SimsAsync(f.Employee.Id, false, null, new(), f.Context, default)).TotalCount == 0 && (await s.SimsAsync(f.Target.Id, false, null, new(), f.Context, default)).TotalCount == 1,
    "SIM current tips exclude superseded source");
f.Requester();
await Fails("sim_already_returned_or_revised", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Sim, simTransfer, "sim-stale-source-001", f.Context, default));

f.Requester(); var cert = new RequestEmployeeRecord(v, EmployeeRecordAction.Issue, null, f.Now, "Initial licence",
    Certification: new(f.CertificateEvidence.Id, "Electrical", "CERT-001", "Synthetic Authority", Fixture.Today.AddDays(-2), Fixture.Today.AddDays(30)));
var cr = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, cert, "cert-issue-001", f.Context, default);
f.Reviewer(); var ci = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, cr.RecordRequestId!.Value, decide, "cert-decision-001", f.Context, default);
f.Requester(); var renewalDoc = f.Document(f.Employee.Id, EmployeeDocumentType.License, Fixture.Today, Fixture.Today.AddDays(60));
var renewal = cert with { Action = EmployeeRecordAction.Renew, ExpectedCurrentRecordId = ci.EmployeeRecordId,
    Certification = cert.Certification! with { EvidenceDocumentId = renewalDoc.Id, IssuedOn = Fixture.Today, ExpiresOn = Fixture.Today.AddDays(60) } };
var rr = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, renewal, "cert-renew-001", f.Context, default);
f.Reviewer(); var ri = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, rr.RecordRequestId!.Value, decide, "cert-renew-decision-001", f.Context, default);
Check(f.Records.Certificates.Count == 2 && f.Records.Certificates[0].EmployeeDocumentId == f.CertificateEvidence.Id &&
    f.Records.Certificates[0].ExpiresOn == Fixture.Today.AddDays(30) && f.Records.Certificates[1].EmployeeDocumentId == renewalDoc.Id,
    "renewal retains old certificate and evidence");
Check((await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().Id == ri.EmployeeRecordId, "renewal current view has exactly successor");
f.Requester();
await Fails("certification_already_revoked_or_revised", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, renewal, "cert-stale-source-001", f.Context, default));
f.Requester(); var replacementDoc = f.Document(f.Employee.Id, EmployeeDocumentType.License, Fixture.Today, Fixture.Today.AddDays(60));
var replacement = renewal with { Action = EmployeeRecordAction.Replace, ExpectedCurrentRecordId = ri.EmployeeRecordId,
    Certification = renewal.Certification! with { EvidenceDocumentId = replacementDoc.Id } };
var replacementRequest = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, replacement, "cert-replace-001", f.Context, default);
f.Reviewer(); var replaced = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, replacementRequest.RecordRequestId!.Value, decide, "cert-replace-decision-001", f.Context, default);
Check(f.Records.Certificates.Count == 3 && f.Records.Certificates[1].EmployeeDocumentId == renewalDoc.Id && f.Records.Certificates[2].EmployeeDocumentId == replacementDoc.Id &&
    f.Records.Certificates[2].ExpiresOn == f.Records.Certificates[1].ExpiresOn, "replacement changes evidence while retaining validity and prior attachment");
f.Now = f.Now.AddDays(60);
Check(!(await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().IsExpired, "certificate valid on inclusive expiry date");
f.Now = f.Now.AddDays(1);
Check((await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().IsExpired, "expiry detected next business date");
f.Now = f.Now.AddDays(-61);
f.Requester(); var suspend = new RequestEmployeeRecord(v, EmployeeRecordAction.Suspend, replaced.EmployeeRecordId, f.Now, "Licence suspension evidence", Certification: new(f.AssetEvidence.Id));
var susp = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, suspend, "cert-suspend-001", f.Context, default);
f.Reviewer(); var suspended = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, susp.RecordRequestId!.Value, decide, "cert-suspend-decision-001", f.Context, default);
Check((await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().Status == EmployeeCertificationStatus.Suspended, "suspended tip stays visible");
f.Requester();
await Fails("certification_renewal_requires_later_validity", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification,
    renewal with { ExpectedCurrentRecordId = suspended.EmployeeRecordId, Certification = renewal.Certification! with { EvidenceDocumentId = f.Document(f.Employee.Id, EmployeeDocumentType.License, Fixture.Today, Fixture.Today.AddDays(90)).Id, ExpiresOn = Fixture.Today.AddDays(90) } },
    "suspended-renew-001", f.Context, default));
var revoke = suspend with { Action = EmployeeRecordAction.Revoke, ExpectedCurrentRecordId = suspended.EmployeeRecordId };
var rev = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification, revoke, "cert-revoke-001", f.Context, default);
f.Reviewer(); var revoked = await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, rev.RecordRequestId!.Value, decide, "cert-revoke-decision-001", f.Context, default);
Check((await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().Status == EmployeeCertificationStatus.Revoked && f.Records.Certificates.Count == 5, "revocation preserves full history");
Check(typeof(IAppendOnlyEntity).IsAssignableFrom(typeof(EmployeeCertification)) && typeof(IAppendOnlyEntity).IsAssignableFrom(typeof(EmployeeSimAssignment)) &&
    typeof(IAppendOnlyEntity).IsAssignableFrom(typeof(EmployeeAssetHistory)), "SIM, certification and asset events append only");
Invalid(() => f.Records.Assets[0].RecordReturn(f.Now, "Good", f.Receiver.Id.ToString(), null));
Invalid(() => new EmployeeCertification(f.Employee.Id, "Electrical", "n", "i", Fixture.Today, Fixture.Today.AddDays(-1), null, "actor", f.Now));
Invalid(() => new EmployeeSimAssignment(f.Employee.Id, "+923001234567", "SIM", "TIER-A", CompanySimStatus.Active, Fixture.Today, null, "actor", f.Now, 2));
f.ExcludedPermission = EmployeeRecordPermissions.CertificationsRead;
await Fails("access_denied", () => s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default));
f.ExcludedPermission = null;
f.SetApproved(false);
await Fails("employee_not_approved", () => s.AssetsAsync(f.Employee.Id, false, null, new(), f.Context, default));
f.SetApproved(true);
Fixture.Set(f.Employee, nameof(Employee.IsArchived), true);
await Fails("employee_record_requires_active_unscheduled_employment", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Asset,
    assetIssue with { Asset = new("DEVICE-003", "Good", f.AssetEvidence.Id) }, "archive-issue-denied-001", f.Context, default));
Fixture.Set(f.Employee, nameof(Employee.IsArchived), false);
var dbOptions = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=never-used;Database=offline;Integrated Security=true;TrustServerCertificate=true").Options;
using (var db = new CosmixDbContext(dbOptions))
{
    var model = db.Model;
    Check(model.FindEntityType(typeof(EmployeeAssetCustody))!.GetIndexes().Any(i => i.IsUnique && i.GetFilter() == "[ReturnedAt] IS NULL"), "SQL active asset uniqueness preserved");
    Check(model.FindEntityType(typeof(EmployeeAssetHistory))!.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeAssetCustodyId", "Revision" })), "asset event revision uniqueness");
    Check(model.FindEntityType(typeof(EmployeeCertification))!.GetIndexes().Any(i => i.IsUnique && i.Properties.Single().Name == "SupersedesEmployeeCertificationId"), "one certificate successor enforced");
    var sql = db.Set<EmployeeSimAssignment>().Where(x => x.EmployeeId == f.Employee.Id && x.Status != CompanySimStatus.Returned &&
        !db.Set<EmployeeSimAssignment>().Any(n => n.SupersedesEmployeeSimAssignmentId == x.Id)).ToQueryString();
    Check(sql.Contains("NOT EXISTS"), "SQL latest SIM excludes prior revisions");
}
void GuardRejects(Action<CosmixDbContext> stage)
{
    using var db = new CosmixDbContext(dbOptions); stage(db); db.ChangeTracker.DetectChanges();
    var method = typeof(CosmixDbContext).Assembly.GetType("Cosmix.Infrastructure.Database.EmployeeAssetWriteGuard", true)!.GetMethod("Validate")!;
    try { method.Invoke(null, new object[] { db }); }
    catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; return; }
    throw new Exception("Asset mutation guard accepted unsafe transition");
}
GuardRejects(db => { var row = new EmployeeAssetCustody(f.Employee.Id, "GUARD-1", f.Now, "Good", null, "actor"); db.Attach(row); Fixture.Set(row, nameof(EmployeeAssetCustody.AssetReference), "CHANGED"); });
GuardRejects(db => { var row = new EmployeeAssetCustody(f.Employee.Id, "GUARD-2", f.Now, "Good", null, "actor"); db.Attach(row); row.RecordReturn(f.Now, "Good", f.Receiver.Id.ToString(), null); });
GuardRejects(db => { var row = new EmployeeAssetCustody(f.Employee.Id, "GUARD-3", f.Now, "Good", null, "actor"); db.Attach(row); db.Remove(row); });
Check(f.Audits > 0 && f.Receipts.Count >= 12 && f.Receipts.Values.All(x => x.ProtectedResultReference!.Length <= 500), "audits and compact idempotent receipts saved");
try { System.Text.Json.JsonSerializer.Deserialize<AssetRecordInput>("{\"AssetReference\":\"A\",\"Condition\":\"Good\",\"EvidenceDocumentId\":\"" + f.AssetEvidence.Id + "\",\"PrivateReference\":\"client-storage-key\"}"); throw new Exception("Unknown client storage reference accepted"); }
catch (System.Text.Json.JsonException) { checks++; }
var baselineChecks = checks;
Console.WriteLine($"PASS: {baselineChecks} existing standalone behavior/model checks.");

// A revoked credential reserves its identity; Issue cannot bypass terminal status with the same proof.
f.Requester();
var requestsBeforeRevokedReissue = f.Records.Requests.Count;
var revokedReissue = replacement with { Action = EmployeeRecordAction.Issue, ExpectedCurrentRecordId = null };
await Fails("certification_already_recorded", () => s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification,
    revokedReissue, "cert-revoked-reissue-denied-001", f.Context, default));
Check(f.Records.Requests.Count == requestsBeforeRevokedReissue && f.Records.Certificates.Count == 5 &&
    (await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items.Single().Status == EmployeeCertificationStatus.Revoked,
    "denied same-credential issue preserves terminal tip and creates no proposal or history");
var distinctCredential = revokedReissue with { Certification = revokedReissue.Certification! with {
    Number = "CERT-002", EvidenceDocumentId = f.Document(f.Employee.Id, EmployeeDocumentType.License, Fixture.Today, Fixture.Today.AddDays(60)).Id } };
var distinctCredentialRequest = await s.RequestAsync(f.Employee.Id, EmployeeRecordKind.Certification,
    distinctCredential, "cert-distinct-issue-001", f.Context, default);
f.Reviewer();
await s.DecideAsync(f.Employee.Id, EmployeeRecordKind.Certification, distinctCredentialRequest.RecordRequestId!.Value,
    decide, "cert-distinct-decision-001", f.Context, default);
var distinctCurrent = (await s.CertificationsAsync(f.Employee.Id, false, null, new(), f.Context, default)).Items;
Check(distinctCurrent.Count == 2 && distinctCurrent.Any(c => c.Status == EmployeeCertificationStatus.Revoked && c.Number == "CERT-001") &&
    distinctCurrent.Any(c => c.Status == EmployeeCertificationStatus.Active && c.Number == "CERT-002"),
    "separate credential identity remains issuable while revoked identity stays terminal");

// Hiring validates all proposed custody intervals before any row is materialized.
AssetProposalInput AssetProposal(string? reference, DateOnly? issued, DateOnly? returned) => new() {
    AssetReference = reference, IssuedOn = issued, ReturnedOn = returned, IssueCondition = "Good",
    ReturnCondition = returned.HasValue ? "Good" : null, ReturnedToAccountId = returned.HasValue ? f.Receiver.Id : null };
EmployeeProfileInput AssetProfile(params AssetProposalInput[] assets) => new() { Assets = assets };
var earlierIssue = Fixture.Today.AddDays(-4); var laterIssue = Fixture.Today.AddDays(-3); var returnDate = Fixture.Today.AddDays(-1);
await Fails("employee_asset_proposal_overlap", () => {
    EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, returnDate),
        AssetProposal("HISTORY-001", laterIssue, returnDate)), Fixture.Today); return Task.CompletedTask; });
await Fails("employee_asset_proposal_overlap", () => {
    EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal(" history-001 ", earlierIssue, returnDate),
        AssetProposal("HISTORY-001", laterIssue, returnDate)), Fixture.Today); return Task.CompletedTask; });
await Fails("employee_asset_proposal_overlap", () => {
    EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, null),
        AssetProposal("HISTORY-001", laterIssue, returnDate)), Fixture.Today); return Task.CompletedTask; });
EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, returnDate),
    AssetProposal("HISTORY-001", returnDate, null)), Fixture.Today);
Check(true, "adjacent returned and issued custody remains valid at the half-open boundary");
EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, laterIssue),
    AssetProposal("HISTORY-001", returnDate, Fixture.Today)), Fixture.Today);
Check(true, "disjoint intervals for one asset remain valid");
EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, returnDate),
    AssetProposal("HISTORY-002", laterIssue, returnDate)), Fixture.Today);
Check(true, "overlapping intervals for distinct assets remain valid");
EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, earlierIssue),
    AssetProposal("HISTORY-001", earlierIssue, returnDate)), Fixture.Today);
Check(true, "zero-length returned interval consumes no custody");
EmployeeFormValidation.ValidateProfile(AssetProfile(AssetProposal("HISTORY-001", earlierIssue, returnDate),
    AssetProposal("HISTORY-001", null, null), AssetProposal(null, laterIssue, null)), Fixture.Today);
Check(true, "partial draft asset fields remain editable");
Console.WriteLine($"PASS: {checks} standalone behavior/model checks; synthetic interfaces, no live SQL/provider or account mutation.");

public class Hook : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args!);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = DispatchProxy.Create<T, Hook>(); ((Hook)(object)proxy).Handler = handler; return proxy; }
}
public sealed class FixedClock(Func<DateTimeOffset> now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now(); }
public sealed class MemoryStore : IPrivateFileStore
{
    private readonly Dictionary<string, byte[]> blobs = [];
    public bool Corrupt;
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    { var key = Guid.NewGuid().ToString("N"); blobs.Add(key, bytes.ToArray()); return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "offline-key", bytes.Length)); }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => Task.FromResult(Corrupt ? new byte[] { 9 } : blobs[reference.StorageKey].ToArray());
}
public sealed class Fixture
{
    public static readonly DateOnly Today = new(2026, 10, 13);
    public static readonly string Version = Convert.ToBase64String(new byte[8]);
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public AuthRequestContext Context = new("synthetic", "a05-offline");
    public readonly Guid RequesterId = Guid.NewGuid(), ReviewerId = Guid.NewGuid();
    public AuthenticatedAccount Identity;
    public bool FixedAdmin = true, CurrentAuthorized = true, Approved = true;
    public string? ExcludedPermission;
    public Employee Employee, Target;
    public UserAccount Receiver;
    public EmployeeDocument AssetEvidence, TargetEvidence, CertificateEvidence;
    public MemoryStore Store = new();
    public MemoryRecords Records = new();
    public Dictionary<Guid, EmployeeDocument> Documents = [];
    public Dictionary<string, IdempotencyReceipt> Receipts = [];
    public int Audits;
    private IEmployeeHiringRepository hiring;
    private IEmployeeLifecycleRepository lifecycle;
    private IAccessManagementRepository access;
    private EmployeeOperation operation;
    public Fixture()
    {
        Employee = NewEmployee(); Target = NewEmployee();
        Identity = new(RequesterId, Employee.Id, Guid.NewGuid(), 1, SessionClient.Web, true);
        Receiver = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
        Set(Receiver, nameof(UserAccount.Id), Guid.NewGuid()); Set(Receiver, nameof(UserAccount.EmployeeId), Target.Id); Set(Receiver, nameof(UserAccount.Status), AccountStatus.Active);
        AssetEvidence = Document(Employee.Id, EmployeeDocumentType.Other, null, null);
        TargetEvidence = Document(Target.Id, EmployeeDocumentType.Other, null, null);
        CertificateEvidence = Document(Employee.Id, EmployeeDocumentType.License, Today.AddDays(-2), Today.AddDays(30));
        hiring = Hook.Create<IEmployeeHiringRepository>((m, a) => m.Name switch
        {
            "FindAsync" => Task.FromResult<Employee?>((Guid)a[0]! == Employee.Id ? Employee : (Guid)a[0]! == Target.Id ? Target : null),
            "FindDraftAsync" => Task.FromResult<EmployeeHiringDraft?>(null),
            "FindDocumentAsync" => Task.FromResult(Documents.TryGetValue((Guid)a[1]!, out var d) && d.EmployeeId == (Guid)a[0]! ? d : null),
            "ExpectVersion" => Match((Employee)a[0]!, (byte[])a[1]!),
            "FindReceiptAsync" => Task.FromResult(Receipts.GetValueOrDefault(a[0] + "/" + a[1] + "/" + a[2])),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!),
            "AddAudit" => AddAudit((AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception("Unexpected hiring method " + m.Name)
        });
        lifecycle = Hook.Create<IEmployeeLifecycleRepository>((m, a) => m.Name switch
        {
            "HasApprovedEmploymentAsync" => Task.FromResult(Approved),
            "CurrentChangesAsync" => Task.FromResult<IReadOnlyList<EmployeeStatusChange>>([]),
            "HasFutureChangeAsync" => Task.FromResult(false),
            _ => throw new Exception("Unexpected lifecycle method " + m.Name)
        });
        access = Hook.Create<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "IsCurrentAdministratorAsync" => Task.FromResult(CurrentAuthorized),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(Identity.AccountId == RequesterId ? "HR" : "Admin"),
            "FindAssignmentAsync" => Task.FromResult<UserRoleAssignment?>(Assignment()),
            "FindAccountAsync" => Task.FromResult<UserAccount?>((Guid)a[0]! == Receiver.Id ? Receiver : null),
            _ => throw new Exception("Unexpected access method " + m.Name)
        });
        var grants = Hook.Create<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(new[] { EmployeePermissions.Read,
            EmployeeRecordPermissions.AssetsRead, EmployeeRecordPermissions.AssetsRequest, EmployeeRecordPermissions.AssetsReview,
            EmployeeRecordPermissions.SimsRead, EmployeeRecordPermissions.SimsRequest, EmployeeRecordPermissions.SimsReview,
            EmployeeRecordPermissions.CertificationsRead, EmployeeRecordPermissions.CertificationsRequest, EmployeeRecordPermissions.CertificationsReview }
            .Where(p => p != ExcludedPermission).Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)).ToArray()));
        operation = new(Hook.Create<ICurrentAuthentication>((m,a) => Identity), grants, access, hiring, new FixedClock(() => Now), Store);
    }
    public EmployeeRecordService Service(EmployeeRecordPolicyOptions? policy = null, EmployeeFieldPolicyOptions? fields = null) =>
        new(operation, hiring, lifecycle, Records, access, Store, new LibPhoneNumberNormalizer(), new MobileAuthenticationOptions { BusinessTimeZoneId = "Asia/Karachi" },
            fields ?? new() { ApprovedVersion = "synthetic-fields", EffectiveFrom = Today.AddDays(-1), SimCreditTierValues = ["TIER-A"] },
            policy ?? new() { ApprovedVersion = "synthetic-authority", ApprovedReviewMode = "FixedAdminReview", EffectiveFrom = Today.AddDays(-1) });
    public EmployeeDocument Document(Guid employeeId, EmployeeDocumentType type, DateOnly? issued, DateOnly? expiry, EmployeeDocumentPrivacy privacy = EmployeeDocumentPrivacy.Standard)
    {
        byte[] bytes = [1, 2, 3, 4]; var reference = Store.WriteAsync(bytes, "unused", default).Result;
        var doc = new EmployeeDocument(employeeId, type, System.Text.Json.JsonSerializer.Serialize(reference), "synthetic.pdf", "application/pdf", bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)), RequesterId.ToString(), Now.AddMinutes(-3), issued, expiry, privacy);
        doc.RecordScan(DocumentScanStatus.Clean, true, "synthetic", Now.AddMinutes(-2));
        doc.RecordVerification(DocumentVerificationStatus.Verified, ReviewerId.ToString(), "Checked", Now.AddMinutes(-1)); Documents[doc.Id] = doc; return doc;
    }
    public void Requester() => Identity = Identity with { AccountId = RequesterId };
    public void Reviewer() => Identity = Identity with { AccountId = ReviewerId };
    public void SetApproved(bool value) => Approved = value;
    private Employee NewEmployee()
    { var e = new Employee(Now.AddDays(-10), "synthetic"); Set(e, nameof(Employee.Status), EmployeeStatus.Active); Set(e, nameof(Employee.OnboardingStatus), OnboardingStatus.Approved); Set(e, nameof(Employee.JoinedOn), Today.AddDays(-5)); Set(e, nameof(Employee.RowVersion), new byte[8]); return e; }
    public static void Set(object instance, string property, object value) => instance.GetType().GetProperty(property)!.SetValue(instance, value);
    private object? Match(Employee employee, byte[] version) { if (!employee.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_version_conflict"); return null; }
    private object? AddReceipt(IdempotencyReceipt receipt) { Receipts.Add(receipt.ActorUserId + "/" + receipt.Operation + "/" + receipt.Key, receipt); return null; }
    private object? AddAudit(AuditEvent audit) { Audits++; return null; }
    private UserRoleAssignment Assignment() { var x = (UserRoleAssignment)Activator.CreateInstance(typeof(UserRoleAssignment), true)!; Set(x, nameof(UserRoleAssignment.RoleId), Guid.NewGuid()); return x; }
}
public sealed class MemoryRecords : IEmployeeRecordRepository
{
    public List<EmployeeAssetCustody> Assets = [];
    public List<EmployeeAssetHistory> AssetEvents = [];
    public List<EmployeeSimAssignment> Sims = [];
    public List<EmployeeCertification> Certificates = [];
    public List<ApprovalRequest> Requests = [];
    public List<ApprovalDecision> Decisions = [];
    public Task<EmployeeAssetCustody?> AssetAsync(Guid id, CancellationToken ct) => Task.FromResult(Assets.SingleOrDefault(x => x.Id == id));
    public Task<EmployeeAssetCustody?> LatestAssetAsync(string reference, CancellationToken ct) => Task.FromResult(Assets.Where(x => x.AssetReference == reference).OrderBy(x => x.ReturnedAt.HasValue).ThenByDescending(x => x.ReturnedAt).FirstOrDefault());
    public Task<IReadOnlyList<EmployeeAssetHistory>> AssetHistoryAsync(Guid employeeId, Guid custodyId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmployeeAssetHistory>>(AssetEvents.Where(x => x.EmployeeId == employeeId && x.EmployeeAssetCustodyId == custodyId).ToArray());
    public Task<PagedResult<EmployeeAssetCustody>> AssetsAsync(Guid employeeId, bool history, DateTimeOffset asOf, EmployeeRecordQuery query, CancellationToken ct) => Task.FromResult(Page(Assets.Where(x => x.EmployeeId == employeeId && (history || x.IssuedAt <= asOf && (x.ReturnedAt == null || x.ReturnedAt > asOf))), query));
    public Task<EmployeeSimAssignment?> SimAsync(Guid id, CancellationToken ct) => Task.FromResult(Sims.SingleOrDefault(x => x.Id == id));
    public Task<bool> SimSuccessorAsync(Guid id, CancellationToken ct) => Task.FromResult(Sims.Any(x => x.SupersedesEmployeeSimAssignmentId == id));
    public Task<IReadOnlyList<EmployeeSimAssignment>> LatestSimsAsync(string reference, string phone, DateOnly asOf, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmployeeSimAssignment>>(Sims.Where(x => (x.SimReference == reference || x.PhoneNumber == phone) && (x.Status != CompanySimStatus.Returned || x.ReturnedOn > asOf) && !Sims.Any(n => n.SupersedesEmployeeSimAssignmentId == x.Id)).Take(2).ToArray());
    public Task<PagedResult<EmployeeSimAssignment>> SimsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct) => Task.FromResult(Page(Sims.Where(x => x.EmployeeId == employeeId && (history || x.EffectiveOn <= asOf && x.Status != CompanySimStatus.Returned && !Sims.Any(n => n.SupersedesEmployeeSimAssignmentId == x.Id && n.EffectiveOn <= asOf))), query));
    public Task<EmployeeCertification?> CertificationAsync(Guid id, CancellationToken ct) => Task.FromResult(Certificates.SingleOrDefault(x => x.Id == id));
    public Task<bool> CertificationSuccessorAsync(Guid id, CancellationToken ct) => Task.FromResult(Certificates.Any(x => x.SupersedesEmployeeCertificationId == id));
    public Task<bool> ActiveCertificationAsync(Guid employeeId, string type, string number, string issuer, CancellationToken ct) => Task.FromResult(Certificates.Any(x => x.EmployeeId == employeeId && x.CertificateType == type && x.CertificateNumber == number && x.Issuer == issuer && !Certificates.Any(n => n.SupersedesEmployeeCertificationId == x.Id)));
    public Task<PagedResult<EmployeeCertification>> CertificationsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct) => Task.FromResult(Page(Certificates.Where(x => x.EmployeeId == employeeId && (history || x.EffectiveOn <= asOf && !Certificates.Any(n => n.SupersedesEmployeeCertificationId == x.Id && n.EffectiveOn <= asOf))), query));
    public Task<bool> HasPendingAsync(Guid employeeId, string type, CancellationToken ct) => Task.FromResult(Requests.Any(x => x.SubjectId == employeeId && x.RequestType == type && x.State == ApprovalState.Pending));
    public Task<ApprovalRequest?> RequestAsync(Guid employeeId, string type, Guid id, CancellationToken ct) => Task.FromResult(Requests.SingleOrDefault(x => x.SubjectId == employeeId && x.RequestType == type && x.Id == id));
    public Task<PagedResult<EmployeeRecordRequestSummary>> RequestsAsync(Guid? employeeId, string type, EmployeeRecordQuery query, CancellationToken ct) => Task.FromResult(Page(Requests.Where(x => (!employeeId.HasValue || x.SubjectId == employeeId) && x.RequestType == type).Select(x => new EmployeeRecordRequestSummary(x.Id, x.SubjectId, x.State, x.RequesterUserId, x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))), query));
    public Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<ApprovalDecision>>(Decisions.Where(x => x.ApprovalRequestId == id).ToArray());
    public void ExpectVersion(ApprovalRequest request, byte[] version) { if (!request.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_record_request_version_conflict"); }
    public void Add(EmployeeAssetCustody row) { Fixture.Set(row, nameof(EmployeeAssetCustody.RowVersion), new byte[8]); Assets.Add(row); }
    public void Add(EmployeeAssetHistory row) => AssetEvents.Add(row);
    public void Add(EmployeeSimAssignment row) => Sims.Add(row);
    public void Add(EmployeeCertification row) => Certificates.Add(row);
    public void Add(ApprovalRequest row) { Fixture.Set(row, nameof(ApprovalRequest.RowVersion), new byte[8]); Requests.Add(row); }
    public void Add(ApprovalDecision row) => Decisions.Add(row);
    private static PagedResult<T> Page<T>(IEnumerable<T> rows, EmployeeRecordQuery q) { var all = rows.ToArray(); return new(all.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), all.Length, q.Page, q.PageSize); }
}
