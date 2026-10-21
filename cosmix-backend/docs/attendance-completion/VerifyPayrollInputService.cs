#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Fails(Func<Task> action, string code, int status = 409)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure ex) { Check(ex.Code == code && ex.StatusCode == status, $"Expected {code}/{status}, got {ex.Code}/{ex.StatusCode}"); }
}
var f = new Fixture(); var s = f.Service; var ct = CancellationToken.None;
var preview = await s.PreviewAsync(f.Employee, f.Month, f.Context, ct);
Check(preview.CanSave && preview.DeductionTotal == 125.25m && !preview.PayrollReady && !preview.PaymentInstruction, "Prepared financial input is not payroll/payment authority");
Check(preview.ExpectedRevision == 0 && preview.PreviewFingerprint.Length == 64 && preview.LegalStatus == "legal_policy_review_required", "Explicit source revision/fingerprint and legal gate");
Check(f.RequestedJurisdictionDays.Count == 1 && f.RequestedJurisdictionDays[0] == new AttendancePayrollJurisdictionDay(f.Month, f.Site) &&
    preview.Jurisdictions.Single().ConfigurationVersionId == f.Jurisdictions[0].ConfigurationVersionId,
    "A retained manifest snapshot drives exact dated-site jurisdiction lookup");
Check(preview.Lines.Single().MonthlySalary == 31000, "Only restricted financial response contains salary input");
var mappedLine = preview.Lines.Single(); var sourceLine = f.Source.Lines.Single();
Check(mappedLine.SourceIds.SequenceEqual(sourceLine.SourceIds) && mappedLine.SourceFingerprints.SequenceEqual(sourceLine.SourceFingerprints) &&
    mappedLine.CompensationFingerprint == sourceLine.CompensationFingerprint, "Restricted line maps exact source and salary provenance");
Check(mappedLine.ExactAmountNumerator == "501" && mappedLine.ExactAmountDenominator == "4" &&
    mappedLine.OrdinaryComparisonAmount == sourceLine.OrdinaryComparisonAmount, "Restricted line maps exact rational amount and overlap comparison");
var request = new SaveAttendancePayrollInputsRequest(preview.PreviewFingerprint, 0, "Synthetic confirmed payroll input review");
var first = await s.SaveAsync(f.Employee, f.Month, request, "payroll-input-001", f.Context, ct);
Check(first.Revision.Revision == 1 && f.Rows.Count == 1 && f.Receipts.Count == 1, "Exactly one immutable revision and receipt");
Check(f.Audits.Any(x => x.EventType == "attendance.payroll-inputs.save") && f.TransactionDepth == 0, "Audited shared authorization transaction");
Check(first.Inputs.State == "prepared_review_required" && !first.Inputs.CanSave && !first.Inputs.PayrollReady, "Saved inputs remain reviewable with no automatic activation");
var retry = await s.SaveAsync(f.Employee, f.Month, request, "payroll-input-001", f.Context, ct);
Check(retry.Revision.Id == first.Revision.Id && f.Rows.Count == 1, "Identical retry does not duplicate deduction");
await Fails(() => s.SaveAsync(f.Employee, f.Month, request with { Reason = "Changed" }, "payroll-input-001", f.Context, ct), "idempotency_key_reused");
await Fails(() => s.SaveAsync(f.Employee, f.Month, request, "payroll-stale-001", f.Context, ct), "payroll_input_revision_conflict");
var same = await s.SaveAsync(f.Employee, f.Month, request with { ExpectedRevision = 1 }, "payroll-input-same", f.Context, ct);
Check(same.Revision.Id == first.Revision.Id && f.Rows.Count == 1, "New key with identical complete source reuses existing revision");
f.Source = f.Source with { InputFingerprint = new string('B', 64) };
await Fails(() => s.SaveAsync(f.Employee, f.Month, request with { ExpectedRevision = 1 }, "payroll-stale-source", f.Context, ct), "payroll_input_preview_stale");
var nextPreview = await s.PreviewAsync(f.Employee, f.Month, f.Context, ct);
var second = await s.SaveAsync(f.Employee, f.Month, new(nextPreview.PreviewFingerprint, 1, "Changed verified sources"), "payroll-input-002", f.Context, ct);
Check(second.Revision.Revision == 2 && second.Revision.SupersedesRevisionId == first.Revision.Id && f.Rows.Count == 2, "New full replacement links immutable predecessor");
Check(first.Inputs.PreviewFingerprint != second.Inputs.PreviewFingerprint, "Changed source changes retained fingerprint");
var history = await s.HistoryAsync(f.Employee, f.Month, 1, 1, f.Context, ct);
Check(history.Items.Count == 1 && history.Items[0].Id == second.Revision.Id && history.TotalCount == 2, "Bounded ordered history");
await Fails(() => s.HistoryAsync(f.Employee, f.Month, 1, 101, f.Context, ct), "invalid_attendance_request", 400);
f.Locked = true;
Check(!(await s.PreviewAsync(f.Employee, f.Month, f.Context, ct)).CanSave, "Locked period cannot be saved");
await Fails(() => s.SaveAsync(f.Employee, f.Month, new(second.Inputs.PreviewFingerprint, 2, "Locked write"), "payroll-locked-01", f.Context, ct), "attendance_period_locked");
var retained = await s.DetailAsync(f.Employee, f.Month, first.Revision.Id, f.Context, ct);
Check(retained.Inputs.IsLocked && retained.Inputs.State == "retained_input_revision", "Locked read retains saved evidence without recalculation");
var count = f.Calculations;
await s.DetailAsync(f.Employee, f.Month, second.Revision.Id, f.Context, ct);
Check(f.Calculations == count, "History never silently recalculates finalized sources");
f.Locked = false;
f.Source = f.Source with { State = "unavailable", Issues = [new("source_pending")] };
var blocked = await s.PreviewAsync(f.Employee, f.Month, f.Context, ct);
Check(blocked.DeductionTotal is null && !blocked.CanSave && blocked.Lines.Count == 1, "Incomplete evidence exposes candidates with no fabricated ready total");
await Fails(() => s.SaveAsync(f.Employee, f.Month, new(blocked.PreviewFingerprint, 2, "Unavailable input"), "payroll-unavailable", f.Context, ct), "payroll_input_sources_unavailable");
f.Source = f.Source with { State = "ready", Issues = [] };
f.Identity = f.Identity with { MfaSatisfied = false };
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "access_denied", 403);
f.Identity = f.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "access_denied", 403);
f.Identity = f.Identity with { Client = SessionClient.Web };
f.SessionCurrent = false;
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "invalid_authentication", 401); f.SessionCurrent = true;
f.Grants.RemoveAll(x => x.Permission == AttendancePayrollInputPermissions.Calculate);
var fileReads = f.FileReads;
await Fails(() => s.SaveAsync(f.Employee, f.Month, request, "payroll-input-001", f.Context, ct), "access_denied", 403);
Check(f.FileReads == fileReads, "Revocation is checked before protected replay read");
f.Grant(AttendancePayrollInputPermissions.Calculate, f.Site);
f.CurrentScopes = [new([new(f.Employee, SiteId: Guid.NewGuid())])];
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "access_denied", 403);
f.CurrentScopes = f.Source.SourceManifest.OriginalScopes;
var old = f.Source;
f.Source = f.Source with { SourceManifest = f.Source.SourceManifest with { OriginalScopes = [new([new(f.Employee, SiteId: Guid.NewGuid())])] } };
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "access_denied", 403);
f.Source = old;
var savedRow = f.Rows[0]; var privateRef = JsonSerializer.Deserialize<PrivateFileReference>(savedRow.ProtectedEvidenceReference)!;
var original = f.Files[privateRef.StorageKey]; var corrupt = JsonNode.Parse(original.Bytes)!;
corrupt["Calculation"]!["Lines"]![0]!["Amount"] = 999m;
f.Files[privateRef.StorageKey] = (original.Purpose, JsonSerializer.SerializeToUtf8Bytes(corrupt));
await Fails(() => s.DetailAsync(f.Employee, f.Month, savedRow.Id, f.Context, ct), "payroll_input_evidence_invalid");
f.Files[privateRef.StorageKey] = original;
await Fails(() => s.DetailAsync(f.Employee, f.Month.AddMonths(1), savedRow.Id, f.Context, ct), "attendance_resource_not_found", 404);
f.Source = old with { Lines = [old.Lines[0], old.Lines[0]] };
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "payroll_input_contract_invalid");
f.Source = old with { Lines = [old.Lines[0] with { Amount = -1 }] };
await Fails(() => s.PreviewAsync(f.Employee, f.Month, f.Context, ct), "payroll_input_contract_invalid");
Check(f.Rows.Count == 2, "Denied, stale, unavailable and corrupt operations never add a revision");

var jurisdiction = new Fixture();
var configured = await jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct);
var exactJurisdiction = jurisdiction.Jurisdictions.Single();
jurisdiction.Jurisdictions = [];
await Fails(() => jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct), "payroll_jurisdiction_contract_invalid");
jurisdiction.Jurisdictions = [exactJurisdiction with { SiteId = Guid.NewGuid() }];
await Fails(() => jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct), "payroll_jurisdiction_contract_invalid");
jurisdiction.Jurisdictions = [exactJurisdiction with { BusinessDate = jurisdiction.Month.AddDays(1) }];
await Fails(() => jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct), "payroll_jurisdiction_contract_invalid");
jurisdiction.Jurisdictions = [exactJurisdiction, exactJurisdiction];
await Fails(() => jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct), "payroll_jurisdiction_contract_invalid");
Check(jurisdiction.Rows.Count == 0 && jurisdiction.Receipts.Count == 0, "Missing/misbound/duplicate jurisdiction results create no financial revision");
jurisdiction.Jurisdictions = [new(jurisdiction.Month, jurisdiction.Site, null, null, "unavailable")];
var missingConfiguration = await jurisdiction.Service.PreviewAsync(jurisdiction.Employee, jurisdiction.Month, jurisdiction.Context, ct);
Check(missingConfiguration.LegalStatus == "jurisdiction_configuration_required" && missingConfiguration.CanSave &&
    !missingConfiguration.PayrollReady && !missingConfiguration.PaymentInstruction,
    "Unavailable jurisdiction permits a review artifact, never payroll activation or payment instruction");
Check(missingConfiguration.PreviewFingerprint != configured.PreviewFingerprint, "Jurisdiction evidence changes the saved-preview identity");
await Fails(() => jurisdiction.Service.SaveAsync(jurisdiction.Employee, jurisdiction.Month,
    new(configured.PreviewFingerprint, 0, "Old jurisdiction proposal"), "payroll-jurisdiction-stale", jurisdiction.Context, ct), "payroll_input_preview_stale");
var jurisdictionSaved = await jurisdiction.Service.SaveAsync(jurisdiction.Employee, jurisdiction.Month,
    new(missingConfiguration.PreviewFingerprint, 0, "Retain missing jurisdiction for review"), "payroll-jurisdiction-save", jurisdiction.Context, ct);
Check(jurisdictionSaved.Inputs.LegalStatus == "jurisdiction_configuration_required" && !jurisdictionSaved.Inputs.PayrollReady &&
    jurisdictionSaved.Inputs.State == "prepared_review_required", "Saving missing jurisdiction cannot assert legal authority");
jurisdiction.Jurisdictions = [exactJurisdiction];
var locationReads = jurisdiction.JurisdictionReads;
var retainedJurisdiction = await jurisdiction.Service.DetailAsync(jurisdiction.Employee, jurisdiction.Month,
    jurisdictionSaved.Revision.Id, jurisdiction.Context, ct);
Check(retainedJurisdiction.Inputs.Jurisdictions.Single().State == "unavailable" &&
    retainedJurisdiction.Inputs.LegalStatus == "jurisdiction_configuration_required" && jurisdiction.JurisdictionReads == locationReads,
    "Saved legal/location evidence is not silently upgraded by a newer configuration");

var historical = new Fixture();
var historicalPreview = await historical.Service.PreviewAsync(historical.Employee, historical.Month, historical.Context, ct);
var historicalRequest = new SaveAttendancePayrollInputsRequest(historicalPreview.PreviewFingerprint, 0, "Original scoped evidence");
var historicalSaved = await historical.Service.SaveAsync(historical.Employee, historical.Month, historicalRequest,
    "payroll-original-scope", historical.Context, ct);
var currentSite = Guid.NewGuid();
historical.CurrentScopes = [new([new(historical.Employee, SiteId: currentSite)])];
historical.Grants.Clear();
historical.Grant(AttendancePayrollInputPermissions.Read, currentSite);
historical.Grant(AttendancePayrollInputPermissions.Calculate, currentSite);
Check(AttendanceScopeEvaluation.Allows(historical.Identity, historical.Grants, AttendancePayrollInputPermissions.Read, historical.CurrentScopes) &&
    AttendanceScopeEvaluation.Allows(historical.Identity, historical.Grants, AttendancePayrollInputPermissions.Calculate, historical.CurrentScopes),
    "Current scopes remain authorized while original saved scopes are independently revoked");
var readsBeforeOriginalDenial = historical.FileReads;
var calculationsBeforeOriginalDenial = historical.Calculations;
await Fails(() => historical.Service.DetailAsync(historical.Employee, historical.Month, historicalSaved.Revision.Id, historical.Context, ct), "access_denied", 403);
await Fails(() => historical.Service.HistoryAsync(historical.Employee, historical.Month, 1, 10, historical.Context, ct), "access_denied", 403);
await Fails(() => historical.Service.SaveAsync(historical.Employee, historical.Month, historicalRequest,
    "payroll-original-scope", historical.Context, ct), "access_denied", 403);
Check(historical.FileReads == readsBeforeOriginalDenial + 3 && historical.Calculations == calculationsBeforeOriginalDenial &&
    historical.Rows.Count == 1 && historical.Receipts.Count == 1,
    "Original-scope denials reach protected evidence/replay authorization without recalculation or a new revision/receipt");
historical.Grant(AttendancePayrollInputPermissions.Read, historical.Site);
historical.Grant(AttendancePayrollInputPermissions.Calculate, historical.Site);
Check((await historical.Service.DetailAsync(historical.Employee, historical.Month, historicalSaved.Revision.Id, historical.Context, ct)).Revision.Id == historicalSaved.Revision.Id,
    "Detail succeeds when both current and original historical scopes are authorized");
Check((await historical.Service.HistoryAsync(historical.Employee, historical.Month, 1, 10, historical.Context, ct)).Items.Single().Id == historicalSaved.Revision.Id,
    "History succeeds when both current and original historical scopes are authorized");
Check((await historical.Service.SaveAsync(historical.Employee, historical.Month, historicalRequest,
    "payroll-original-scope", historical.Context, ct)).Revision.Id == historicalSaved.Revision.Id && historical.Rows.Count == 1,
    "Exact receipt replay preserves one revision after original-scope permission is restored");
var financialReview = new Fixture();
financialReview.Source = financialReview.Source with { Lines = [financialReview.Source.Lines[0] with
    { Kind = "late_threshold", CandidateAmount = 142.75m, OrdinaryComparisonAmount = 17.50m,
      ExactAmountNumerator = "571", ExactAmountDenominator = "4", ApplicationCode = "late_excess_over_ordinary" }] };
var financialPreview = await financialReview.Service.PreviewAsync(financialReview.Employee, financialReview.Month, financialReview.Context, ct);
var financialSaved = await financialReview.Service.SaveAsync(financialReview.Employee, financialReview.Month,
    new(financialPreview.PreviewFingerprint, 0, "Review exact financial comparison"), "payroll-financial-fields", financialReview.Context, ct);
var financialDetail = await financialReview.Service.DetailAsync(financialReview.Employee, financialReview.Month,
    financialSaved.Revision.Id, financialReview.Context, ct);
var financialLine = financialDetail.Inputs.Lines.Single();
Check(financialLine.CandidateAmount == 142.75m && financialLine.OrdinaryComparisonAmount == 17.50m && financialLine.Amount == 125.25m &&
    financialLine.ApplicationCode == "late_excess_over_ordinary", "Saved restricted detail preserves nonzero overlap comparison without double charging");
Check(financialLine.ExactAmountNumerator == "571" && financialLine.ExactAmountDenominator == "4" &&
    financialLine.SourceIds.SequenceEqual(financialReview.Source.Lines[0].SourceIds) &&
    financialLine.SourceFingerprints.SequenceEqual(financialReview.Source.Lines[0].SourceFingerprints) &&
    financialLine.CompensationFingerprint == financialReview.Source.Lines[0].CompensationFingerprint,
    "Saved financial explanation retains rational candidate amount and every provenance identity/digest");
var publicFinancialJson = JsonSerializer.Serialize(financialDetail);
Check(!publicFinancialJson.Contains("ProtectedEvidenceReference", StringComparison.Ordinal) &&
    !publicFinancialJson.Contains("StorageKey", StringComparison.Ordinal) && !publicFinancialJson.Contains("SourceManifest", StringComparison.Ordinal),
    "Restricted review DTO exposes provenance IDs/hashes without private storage references or internal source manifest");
var monthFinance = new Fixture();
var missingFinance = await monthFinance.InspectFinancial();
Check(missingFinance.Issues.Any(x => x.Code == "payroll_input_revision_missing") && missingFinance.PayrollInputs.Count == 0,
    "actual financial dependency inspection reports a missing saved revision, never a zero payable amount");
monthFinance.CalculationFailure="employee_assignment_unavailable";
var missingAssignmentFinance=await monthFinance.InspectFinancial();
Check(missingAssignmentFinance.Issues.Any(x=>x.Code=="employee_assignment_unavailable") && missingAssignmentFinance.PayrollInputs.Count==0,
    "financial source dependency keeps a missing historical assignment as an actionable month-preview issue");
monthFinance.CalculationFailure="access_denied";
await Fails(()=>monthFinance.InspectFinancial(),"access_denied",403);
monthFinance.CalculationFailure=null;
var monthPreview = await monthFinance.Service.PreviewAsync(monthFinance.Employee, monthFinance.Month, monthFinance.Context, ct);
var monthSaved = await monthFinance.Service.SaveAsync(monthFinance.Employee, monthFinance.Month,
    new(monthPreview.PreviewFingerprint, 0, "Prepare exact month-lock financial source"), "month-source-save", monthFinance.Context, ct);
var inspectedFinance = await monthFinance.InspectFinancial();
Check(inspectedFinance.PayrollInputs.Single().SourceRevisionId == monthSaved.Revision.Id &&
    inspectedFinance.PayrollInputs.Single().InputFingerprint == monthSaved.Revision.InputFingerprint,
    "actual financial reader binds the exact saved protected revision");
Check(inspectedFinance.Issues.Any(x => x.Code == "payroll_handoff_required") &&
    inspectedFinance.Issues.Any(x => x.Code == "legal_payroll_activation_required"),
    "saved calculation is neither payroll handoff nor legal activation");
var beforeMoneyChange = inspectedFinance.Fingerprint;
monthFinance.Source = monthFinance.Source with { InputFingerprint = new string('9', 64) };
var changedFinance = await monthFinance.InspectFinancial();
Check(changedFinance.Issues.Any(x => x.Code == "payroll_input_revision_stale") && changedFinance.Fingerprint != beforeMoneyChange,
    "changed calculation remains explicitly stale and changes finalization fingerprint");
monthFinance.Grants.RemoveAll(x => x.Permission == AttendancePayrollInputPermissions.Read);
await Fails(() => monthFinance.InspectFinancial(), "access_denied", 403);
var officePayroll = new Fixture(); officePayroll.UseOffice();
var officePreview = await officePayroll.Service.PreviewAsync(officePayroll.Employee, officePayroll.Month, officePayroll.Context, ct);
Check(JsonSerializer.Serialize(officePayroll.RequestedJurisdictionDays).Contains("\"BranchId\":\"" + officePayroll.Branch + "\"", StringComparison.Ordinal),
    "B01 RED: payroll jurisdiction requests bind the actual Office branch rather than a null Site or Head Office default");
Check(officePreview.PayrollReady == false && officePreview.PaymentInstruction == false, "Office preview grants no payroll payment or legal authority");
var exactOfficeLocation = officePayroll.Jurisdictions.Single();
foreach (var wrong in new[] { exactOfficeLocation with { BranchId = Guid.NewGuid() },
    exactOfficeLocation with { ConfigurationVersionId = Guid.NewGuid() }, exactOfficeLocation with { JurisdictionCode = "PK-PB" } })
{
    officePayroll.Jurisdictions = [wrong];
    await Fails(() => officePayroll.Service.PreviewAsync(officePayroll.Employee, officePayroll.Month, officePayroll.Context, ct),
        "payroll_jurisdiction_contract_invalid");
}
officePayroll.Jurisdictions = [exactOfficeLocation];
var officeSaved = await officePayroll.Service.SaveAsync(officePayroll.Employee, officePayroll.Month,
    new(officePreview.PreviewFingerprint, 0, "Retain exact historical Office"), "office-payroll-save", officePayroll.Context, ct);
officePayroll.Jurisdictions = [exactOfficeLocation with { State = "unavailable", ConfigurationVersionId = null, JurisdictionCode = null }];
var officeChanged = await officePayroll.Service.PreviewAsync(officePayroll.Employee, officePayroll.Month, officePayroll.Context, ct);
Check(officeChanged.PreviewFingerprint != officePreview.PreviewFingerprint && officeChanged.LegalStatus == "jurisdiction_configuration_required",
    "Office location changes alter preview fingerprint without legal certification");
var officeReads = officePayroll.JurisdictionReads;
var officeDetail = await officePayroll.Service.DetailAsync(officePayroll.Employee, officePayroll.Month, officeSaved.Revision.Id, officePayroll.Context, ct);
Check(officeDetail.Inputs.Jurisdictions.Single() == exactOfficeLocation && officePayroll.JurisdictionReads == officeReads,
    "retained Office jurisdiction remains bound to saved history rather than a later live default");
var missingOffice = new Fixture(); missingOffice.UseOffice();
var missingDay = missingOffice.Source.SourceManifest.Days.Single();
missingOffice.Source = missingOffice.Source with { State = "unavailable", Lines = [], Issues = [new("office_boundary_not_configured", missingOffice.Month)],
    SourceManifest = missingOffice.Source.SourceManifest with { Days = [missingDay with { Snapshot = null, Time = null, BranchId = missingOffice.Branch }] } };
await Fails(() => missingOffice.Service.PreviewAsync(missingOffice.Employee, missingOffice.Month, missingOffice.Context, ct),
    "payroll_jurisdiction_contract_invalid");
missingOffice.Jurisdictions = [new(missingOffice.Month, null, null, null, "unavailable", missingOffice.Branch)];
var missingOfficePreview = await missingOffice.Service.PreviewAsync(missingOffice.Employee, missingOffice.Month, missingOffice.Context, ct);
Check(missingOfficePreview.State == "unavailable" && missingOfficePreview.DeductionTotal is null && !missingOfficePreview.CanSave &&
    missingOfficePreview.LegalStatus == "jurisdiction_configuration_required", "missing Office proof is pending, never a zero-money configured Office");
var broaderOfficePayroll = new Fixture(); broaderOfficePayroll.UseOffice();
var broaderSitePayroll = new Fixture(); var siteDay = broaderSitePayroll.Source.SourceManifest.Days.Single();
var siteSnapshot = siteDay.Snapshot! with { Employment = siteDay.Snapshot!.Employment with { StaffCategory = "FIELD_ENGINEER" } };
broaderSitePayroll.Source = broaderSitePayroll.Source with { SourceManifest = broaderSitePayroll.Source.SourceManifest with {
    Days = [siteDay with { Snapshot = siteSnapshot }] } };
await Fails(() => broaderSitePayroll.Service.PreviewAsync(broaderSitePayroll.Employee, broaderSitePayroll.Month, broaderSitePayroll.Context, ct),
    "payroll_taxonomy_source_conflict");
siteSnapshot = siteSnapshot with { Roster = siteSnapshot.Roster with { Taxonomy = new("FIELD_ENGINEER", Guid.NewGuid(), new('D', 64),
    StaffWorkLocation.Site, StaffScheduleBasis.Site, true) } };
broaderSitePayroll.Source = broaderSitePayroll.Source with { SourceManifest = broaderSitePayroll.Source.SourceManifest with {
    Days = [siteDay with { Snapshot = siteSnapshot }] } };
Check((await broaderSitePayroll.Service.PreviewAsync(broaderSitePayroll.Employee, broaderSitePayroll.Month, broaderSitePayroll.Context, ct)).CanSave,
    "Broader Site payroll source requires and retains its matching typed taxonomy pin");
var broaderDay = broaderOfficePayroll.Source.SourceManifest.Days.Single(); var broaderSnapshot = broaderDay.Snapshot!;
var publishedTaxonomy = new RosterStaffTaxonomySource("SUPPORT", Guid.NewGuid(), new('D', 64), StaffWorkLocation.Office, StaffScheduleBasis.Office, false);
broaderSnapshot = broaderSnapshot with { Roster = broaderSnapshot.Roster with { Taxonomy = publishedTaxonomy },
    Employment = broaderSnapshot.Employment with { StaffCategory = "SUPPORT" } };
broaderOfficePayroll.Source = broaderOfficePayroll.Source with { SourceManifest = broaderOfficePayroll.Source.SourceManifest with {
    Days = [broaderDay with { Snapshot = broaderSnapshot }] } };
Check((await broaderOfficePayroll.Service.PreviewAsync(broaderOfficePayroll.Employee, broaderOfficePayroll.Month, broaderOfficePayroll.Context, ct)).CanSave,
    "Actual payroll service accepts retained broader Office taxonomy without inventing jurisdiction/payment authority");
foreach (var badPin in new RosterStaffTaxonomySource?[] { null, publishedTaxonomy with { StaffCategoryCode = "OTHER" },
    publishedTaxonomy with { WorkLocation = StaffWorkLocation.Site }, publishedTaxonomy with { PolicyVersionId = Guid.Empty } })
{
    broaderOfficePayroll.Source = broaderOfficePayroll.Source with { SourceManifest = broaderOfficePayroll.Source.SourceManifest with {
        Days = [broaderDay with { Snapshot = broaderSnapshot with { Roster = broaderSnapshot.Roster with { Taxonomy = badPin } } }] } };
    await Fails(() => broaderOfficePayroll.Service.PreviewAsync(broaderOfficePayroll.Employee, broaderOfficePayroll.Month, broaderOfficePayroll.Context, ct),
        "payroll_office_source_conflict");
}
// Actual Finance service with a deliberately synthetic legal/lock collaborator, not legal activation.
var handoffInputs=new Fixture();
async Task SaveHandoffInput(string key) {
    var p=await handoffInputs.Service.PreviewAsync(handoffInputs.Employee,handoffInputs.Month,handoffInputs.Context,ct);
    await handoffInputs.Service.SaveAsync(handoffInputs.Employee,handoffInputs.Month,
        new(p.PreviewFingerprint,p.ExpectedRevision,"Synthetic retained Finance source"),key,handoffInputs.Context,ct);
}
await SaveHandoffInput("handoff-source-one");
var originalRevision=handoffInputs.Rows.Single();
handoffInputs.Source=handoffInputs.Source with {InputFingerprint=new('B',64)};
await SaveHandoffInput("handoff-source-two");
var handoff=new HandoffFixture(handoffInputs,originalRevision);
var handoffPreview=await handoff.Preview();
Check(handoffPreview.Lines.Single().SourceId==originalRevision.Id && handoffPreview.Lines.Single().SourceVersion==1 &&
    handoffPreview.Lines.Single().Amount==-125.25m && handoffPreview.Skipped.Count==0,
    "Finance consumes exact retained revision 1 even when revision 2 is latest");
handoff.AddLoan();
var withLoan=await handoff.Preview();
Check(withLoan.Lines.Count==2 && withLoan.Lines.Single(x=>x.Component=="LoanRepayment").Amount==-100,
    "unpaid interest-free installment projects exact principal");
handoff.LoanPayments.Add(handoff.LoanPayment(100));
var manuallyPaid=await handoff.Preview();
Check(manuallyPaid.Lines.Count==1 && manuallyPaid.Skipped.Single().Reason=="loan_installment_already_repaid",
    "fully manually repaid installment is explicitly excluded before preparation");
await Fails(()=>handoff.Prepare(withLoan,"paid-loan-stale-preview"),"payroll_handoff_preview_changed");
Check(handoff.Batches.Count==0,"stale unpaid preview cannot produce a duplicate deduction batch");
handoff.LoanPayments.Clear(); handoff.LoanPayments.Add(handoff.LoanPayment(20));
await Fails(()=>handoff.Preview(),"payroll_loan_balance_changed");
handoff.LoanPayments.Clear();
handoffInputs.AfterPrivateWrite=purpose=> { if(purpose.EndsWith(":line",StringComparison.Ordinal) && handoff.LoanPayments.Count==0)
    handoff.LoanPayments.Add(handoff.LoanPayment(100)); };
await Fails(()=>handoff.Prepare(withLoan,"paid-loan-during-prepare"),"payroll_handoff_preview_changed");
Check(handoff.Batches.Count==0,"repayment during private evidence IO cannot freeze an obsolete loan deduction");
handoffInputs.AfterPrivateWrite=null; handoff.LoanRows.Clear(); handoff.LoanPayments.Clear();
var member=handoff.Snapshot.PayrollInputs![0];
handoff.Snapshot=handoff.Snapshot with {PayrollInputs=[member with {SourceEvidenceSha256=new('F',64)}]};
await Fails(()=>handoff.Preview(),"month_lock_payroll_revision_conflict");
handoff.Snapshot=handoff.Snapshot with {PayrollInputs=[member with {SourceRevisionId=Guid.NewGuid()}]};
await Fails(()=>handoff.Preview(),"month_lock_payroll_revision_missing");
handoff.Snapshot=handoff.Snapshot with {PayrollInputs=[member],SchemaVersion=2,Overtime=null};
await Fails(()=>handoff.Preview(),"payroll_handoff_retained_membership_unavailable");
handoff.Snapshot=handoff.Snapshot with {SchemaVersion=3,Overtime=[]};
var sourceGrants=handoffInputs.Grants.Where(x=>x.Permission==AttendancePayrollInputPermissions.Read).ToArray();
handoffInputs.Grants.RemoveAll(x=>x.Permission==AttendancePayrollInputPermissions.Read);
await Fails(()=>handoff.Preview(),"access_denied",403);
Check(handoff.Batches.Count==0,"denied retained deduction is not silently skipped into a partial batch");
handoffInputs.Grants.AddRange(sourceGrants);
handoffInputs.Identity=handoffInputs.Identity with {MfaSatisfied=false};
await Fails(()=>handoff.Preview(),"access_denied",403);
handoffInputs.Identity=handoffInputs.Identity with {MfaSatisfied=true};
handoffInputs.AfterPrivateWrite=purpose=> {
    if(purpose.EndsWith(":line",StringComparison.Ordinal))
        handoff.Snapshot=handoff.Snapshot with {PreviewFingerprint=new('C',64)};
};
await Fails(()=>handoff.Prepare(handoffPreview,"handoff-race-001"),"payroll_handoff_preview_changed");
Check(handoff.Batches.Count==0,"changed lock evidence during private line IO stages no Finance batch");
handoffInputs.AfterPrivateWrite=null;
handoffPreview=await handoff.Preview();
var batch=await handoff.Prepare(handoffPreview,"handoff-prepare-001");
Check(batch.Batch.State=="prepared" && batch.Lines.Single().SourceId==originalRevision.Id && batch.Processing.Count==0,
    "prepared Finance record is reviewable, not posted/settled/paid");
Check((await handoff.Prepare(handoffPreview,"handoff-prepare-001")).Batch.Id==batch.Batch.Id && handoff.Batches.Count==1,
    "same-key Finance retry does not duplicate batch or deduction");
await Fails(()=>handoff.Prepare(handoffPreview with {PreviewFingerprint=new('D',64)},"handoff-prepare-001"),"idempotency_key_reused");
await Fails(()=>handoff.Prepare(handoffPreview,"handoff-prepare-002"),"payroll_handoff_period_already_prepared");
handoff.ReadFailure="month_lock_legal_activation_unavailable";
await Fails(()=>handoff.Prepare(handoffPreview,"handoff-prepare-001"),"month_lock_legal_activation_unavailable");
Check(handoff.Batches.Count==1,"receipt replay cannot bypass revoked/unavailable retained lock verification");
Console.WriteLine($"PASS: {checks} payroll input/Finance service/authorization/retry/evidence checks; synthetic IO and legal collaborator, no SQL/network writes.");

public sealed class HandoffFixture
{
    public MonthLockSnapshot Snapshot;
    public List<PayrollHandoffBatch> Batches=[];
    public List<PayrollHandoffLoanScheduleSource> LoanRows=[];
    public List<RepaymentLedgerEntry> LoanPayments=[];
    public AdvanceLoanAgreement? Loan;
    public string? ReadFailure;
    private readonly Fixture input;
    private readonly PayrollHandoffService service;
    public HandoffFixture(Fixture input,AttendancePayrollInputRevision row)
    {
        this.input=input;
        foreach(var definition in PayrollHandoffPermissions.Definitions)
            input.Grants.Add(new(definition.Code,definition.Sensitive,Guid.NewGuid(),AccessScopeKind.All,null));
        var summary=new DailyAttendanceSummary(input.Employee,input.Month,1,AttendanceDayStatus.RestDay,Guid.NewGuid(),null,
            "synthetic-lock-v1",new('A',64),540,0,0,0,0,0,new Clock().GetUtcNow());
        var period=new AttendancePeriodLock(input.Month,input.Month.AddMonths(1));
        var item=period.RecordDirectLock(Guid.NewGuid(),input.Identity.AccountId.ToString(),"Synthetic lock collaborator",
            new Clock().GetUtcNow(),"synthetic-lock-reference");
        var existing=new MonthLockExisting(period,item,[summary],false);
        Snapshot=new(3,item.Id,period.Id,item.Revision,input.Month,input.Month.AddMonths(1),item.Actor,item.Reason,item.AtUtc,new('A',64),
            [new(input.Employee,input.Month,summary.Id,summary.InputFingerprint,summary.PolicyVersionId!.Value,summary.CalculationVersion)],[],
            [new(input.Employee,input.Month,row.Id,row.Revision,row.InputFingerprint,row.CalculationVersion,
                JsonSerializer.Deserialize<PrivateFileReference>(row.ProtectedEvidenceReference)!.Sha256,
                Guid.NewGuid(),new('B',64),new('C',64))],[]);
        var months=Proxy.Make<IMonthLockRepository>((m,_)=>m.Name=="FindAsync"?Task.FromResult<MonthLockExisting?>(existing):throw new Exception(m.Name));
        var reader=Proxy.Make<IMonthLockSnapshotReader>((_,_)=>ReadFailure is null?Task.FromResult(Snapshot):
            Task.FromException<MonthLockSnapshot>(new AuthenticationFailure(ReadFailure,409)));
        var repo=Proxy.Make<IPayrollHandoffRepository>((m,a)=>m.Name switch {
            "AllowancesAsync"=>Task.FromResult<IReadOnlyList<PayrollHandoffAllowanceSource>>([]),
            "LoanSchedulesAsync"=>Task.FromResult<IReadOnlyList<PayrollHandoffLoanScheduleSource>>(LoanRows.ToArray()),
            "FindAgreementIdForScheduleAsync"=>Task.FromResult<Guid?>(Loan?.Schedule.Any(x=>x.Id==(Guid)a[0]!)==true?Loan.Id:null),
            "FuelClaimsAsync"=>Task.FromResult<IReadOnlyList<PayrollHandoffFuelClaimSource>>([]),
            "PeriodPreparedAsync"=>Task.FromResult(Batches.Count!=0),
            "Add"=>Add((PayrollHandoffBatch)a[0]!),"SaveChangesAsync"=>Task.CompletedTask,
            _=>throw new Exception("Unexpected Finance query: "+m.Name)});
        service=new(input.Operation,repo,months,input.Ledger,
            Proxy.Make<IAllowanceRepository>((m,_)=>throw new Exception(m.Name)),
            Proxy.Make<IAllowanceSettlementStore>((m,_)=>throw new Exception(m.Name)),input.Access,input.FileStore,reader,
            Proxy.Make<IAdvanceLoanRepository>((m,a)=>m.Name switch {
                "FindAgreementWithDetailsAsync"=>Task.FromResult(Loan?.Id==(Guid)a[0]!?Loan:null),
                "ListLedgerEntriesAsync"=>Task.FromResult<IReadOnlyList<RepaymentLedgerEntry>>(LoanPayments.ToArray()),
                _=>throw new Exception(m.Name)}));
    }
    public void AddLoan() {
        var now=new Clock().GetUtcNow();
        var request=new AdvanceLoanRequest(input.Employee,AdvanceLoanKind.Loan,1000,"PKR","Synthetic principal-only loan",input.Month,Guid.NewGuid().ToString(),now);
        var approval=new ApprovalRequest(AdvanceLoanService.RequestType,request.Id,request.RequestedByUserId,"synthetic-loan-source",1,now);
        request.Submit(approval); approval.Decide(ApprovalState.Approved,Guid.NewGuid().ToString(),"Synthetic Finance","Principal only",now);
        typeof(AdvanceLoanRequest).GetProperty(nameof(AdvanceLoanRequest.ApprovalRequest))!.SetValue(request,approval);
        Loan=new(request.Id,1000,"PKR",input.Month,"synthetic-interest-free-terms",approval.Id,Guid.NewGuid().ToString(),now);
        typeof(AdvanceLoanAgreement).GetProperty(nameof(AdvanceLoanAgreement.AdvanceLoanRequest))!.SetValue(Loan,request);
        var schedule=new RepaymentScheduleEntry(Loan.Id,1,input.Month,100,0,"PKR"); Loan.Schedule.Add(schedule);
        LoanRows.Add(new(schedule.Id,input.Employee,Loan.Id,1,input.Month,100,"PKR",approval.Id,Loan.ApprovedTermsPrivateReference));
    }
    public RepaymentLedgerEntry LoanPayment(decimal amount)=>new(Loan!.Id,Loan.Schedule.Single().Id,RepaymentEntryKind.ReceivedPayment,
        amount,"PKR",input.Month,"SYNTHETIC-MANUAL-PAYMENT",Guid.NewGuid().ToString(),null,input.Identity.AccountId.ToString(),new Clock().GetUtcNow());
    private object? Add(PayrollHandoffBatch batch){Batches.Add(batch);return null;}
    public Task<PayrollHandoffPreview> Preview()=>service.PreviewAsync(input.Month.Year,input.Month.Month,input.Context,default);
    public Task<PayrollHandoffBatchDetail> Prepare(PayrollHandoffPreview preview,string key)=>service.PrepareAsync(
        new(input.Month.Year,input.Month.Month,preview.PreviewFingerprint,"Synthetic Finance handoff"),key,input.Context,default);
}

public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
}
public sealed class Clock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero); }
public sealed class Fixture
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid();
    public Guid? Branch;
    public DateOnly Month = new(2026, 9, 1);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-payroll-input-service");
    public bool Locked, SessionCurrent = true;
    public int Calculations, FileReads, TransactionDepth, JurisdictionReads;
    public string? CalculationFailure;
    public List<PermissionGrant> Grants = [];
    public List<AttendancePayrollInputRevision> Rows = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AttendancePayrollInputCalculation Source;
    public IReadOnlyList<AttendanceResourceScope> CurrentScopes;
    public IReadOnlyList<AttendancePayrollJurisdictionContext> Jurisdictions;
    public IReadOnlyList<AttendancePayrollJurisdictionDay> RequestedJurisdictionDays = [];
    public AttendancePayrollInputService Service;
    public AttendanceOperation Operation;
    public IAttendancePayrollInputLedger Ledger;
    public IAccountAccessReader Access;
    public IPrivateFileStore FileStore;
    public Action<string>? AfterPrivateWrite;
    public AttendanceMonthLockFinancialReader Financial;
    public Task<MonthLockFinancialInspection> InspectFinancial() => Operation.ReadAsync(MonthLockPermissions.Read,
        "attendance.month-lock.financial-check", null, Context,
        (actor, token) => Financial.InspectAsync(Month, Month.AddMonths(1), [Employee], actor, token), default);
    public void Grant(string permission, Guid site) => Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.Site, site));
    public Fixture()
    {
        Grant(AttendancePayrollInputPermissions.Read, Site); Grant(AttendancePayrollInputPermissions.Calculate, Site);
        Grant(AttendancePayrollAttestationPermissions.Read, Site);
        Grants.Add(new(MonthLockPermissions.Read, true, Guid.NewGuid(), AccessScopeKind.All, null));
        CurrentScopes = [new([new(Employee, SiteId: Site)])];
        var line = new AttendancePayrollInputLine("synthetic-day", "ordinary_shortfall", Month, [Month], [Guid.NewGuid()], [Guid.NewGuid()], [new string('C', 64)],
            Guid.NewGuid(), new string('D', 64), AttendanceDeductionSalaryBasis.BaseSalary, 31000, 30, 0.12525m, 100, 1000,
            "501", "4", 125.25m, "PKR", AttendanceDeductionRounding.TwoDecimalPlacesHalfUp, CandidateAmount: 125.25m);
        var rules = new AttendancePolicyRules(3, new(900, 3600, 9000),
            new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
                AttendanceEarlyExitTreatment.ActualWorkedTime, 16200, AttendanceNonScheduledDayTreatment.NoCredit,
                AttendanceNonScheduledDayTreatment.ScheduledDayCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
            new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
                AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
                AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange, AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
                true, AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
            new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, 0.5m,
                AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
                AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
        var starts = new DateTimeOffset(Month.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        var timeInput = new AttendanceTimeInput(rules, starts, starts.AddHours(9), 540, 0, false, false, [], []);
        var time = AttendanceDailyTimeCalculation.Calculate(timeInput);
        var snapshot = new AttendanceCalculationSnapshot(1, Employee, Month, AttendanceDailyTimeCalculation.Version, new('A', 64),
            new(Employee, Month, "scheduled", RosterSchedule.TimeZoneId, Site, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                starts, starts.AddHours(9), 540, 0, 0),
            new(Guid.NewGuid(), Guid.NewGuid(), "SITE", Site, Month, null),
            new("effective", line.PolicyVersionIds[0], [Guid.NewGuid()], rules, AttendancePolicyRulesCodec.Canonicalize(rules), new('B', 64)),
            new(LeaveContextStatus.Complete, [], new('C', 64), []), [], [], [], [], timeInput, 0);
        var salary = new AttendancePayrollCompensation(line.CompensationRecordId, 31000, 31000, "PKR", WageBasis.Monthly,
            Month, null, line.CompensationFingerprint, [line.CompensationRecordId]);
        Source = new(Employee, Month, Month.AddMonths(1), Month, Month.AddMonths(1).AddDays(-1), "ready", new string('A', 64), [line], [],
            new(1, Employee, Month, Month.AddMonths(1), Month, Month.AddMonths(1).AddDays(-1), AttendancePayrollInputRules.Version,
                new string('E', 64), CurrentScopes, [new(Month, "available", new('C', 64), snapshot, time, salary)]));
        Jurisdictions = [new(Month, Site, Guid.NewGuid(), "PK-PB", "configured")];
        var current = Proxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = Proxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => Transaction(method, args), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ExplicitPayrollManager"), _ => throw new Exception(method.Name) });
        var journal = Proxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => Add(Audits, (AuditEvent)args[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(method.Name) });
        var files = Proxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") { var bytes = (byte[])args[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)args[1]!, bytes);
                AfterPrivateWrite?.Invoke((string)args[1]!);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            FileReads++; var stored = Files[((PrivateFileReference)args[0]!).StorageKey];
            if (stored.Purpose != (string)args[1]!) throw new Exception("Purpose mismatch"); return Task.FromResult(stored.Bytes);
        });
        var operation = new AttendanceOperation(current, access, authorization, journal,
            Proxy.Make<IAuthenticationRepository>((method, _) => throw new Exception(method.Name)), new Clock(), files);
        var producer = Proxy.Make<IAttendancePayrollInputProducer>((method, _) => method.Name switch {
            "ResourceScopesAsync" => Task.FromResult(CurrentScopes), "CalculateAsync" => Calculate(), _ => throw new Exception(method.Name) });
        var ledger = Proxy.Make<IAttendancePayrollInputLedger>((method, args) => method.Name switch {
            "IsLockedAsync" => Task.FromResult(Locked), "LatestAsync" => Task.FromResult(Rows.OrderByDescending(x => x.Revision).FirstOrDefault()),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.EmployeeId == (Guid)args[0]! && x.Id == (Guid)args[1]!)),
            "HistoryAsync" => Task.FromResult(new PagedResult<AttendancePayrollInputRevision>(Rows.OrderByDescending(x => x.Revision)
                .Skip(((int)args[2]! - 1) * (int)args[3]!).Take((int)args[3]!).ToArray(), Rows.Count, (int)args[2]!, (int)args[3]!)),
            "Add" => Add(Rows, (AttendancePayrollInputRevision)args[0]!), _ => throw new Exception(method.Name) });
        var jurisdictions = Proxy.Make<IAttendancePayrollJurisdictionReader>((_, args) =>
        {
            JurisdictionReads++;
            RequestedJurisdictionDays = ((IReadOnlyList<AttendancePayrollJurisdictionDay>)args[0]!).ToArray();
            return Task.FromResult(Jurisdictions);
        });
        Service = new(operation, producer, ledger, jurisdictions, files, access);
        Ledger=ledger;Access=access;FileStore=files;
        Operation = operation;
        var attestationRepository = Proxy.Make<IAttendancePayrollAttestationRepository>((method, _) => method.Name switch {
            "HistoryAsync" => Task.FromResult<IReadOnlyList<AttendancePayrollAttestation>>([]), _ => throw new Exception(method.Name) });
        var attestations = new AttendancePayrollAttestationService(operation, Service, ledger, producer, attestationRepository, files, access);
        Financial = new(Service, attestations, ledger, attestationRepository);
    }
    public void UseOffice()
    {
        Branch = Guid.NewGuid();
        var day = Source.SourceManifest.Days[0]; var original = day.Snapshot!;
        var officeRoster = original.Roster with { SiteId = null, BranchId = Branch,
            Explanation = new("office", null, original.Employment.AssignmentId, null, null, "synthetic")
                { OfficeRosterAssignmentId = original.Roster.AssignmentId },
            Holiday = new("configured", Month, Guid.NewGuid(), "synthetic", 1, []) };
        var boundary = new OfficeBoundaryVersionResponse(Guid.NewGuid(), Branch.Value, 1, Month.AddDays(-1),
            24m, 67m, 100m, 20m, "PK-SD", "synthetic-boundary", "synthetic", new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var office = OfficeAttendanceSource.Capture(Employee, Month, officeRoster, boundary)!;
        var snapshot = original with { Roster = officeRoster, OfficeSource = office,
            Employment = original.Employment with { StaffCategory = "OFFICE", SiteId = null, BranchId = Branch } };
        CurrentScopes = [new([new(EmployeeId: Employee)])];
        Grants.Clear();
        foreach (var permission in new[] { AttendancePayrollInputPermissions.Read, AttendancePayrollInputPermissions.Calculate })
            Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.Employee, Employee));
        Source = Source with { SourceManifest = Source.SourceManifest with { OriginalScopes = CurrentScopes,
            Days = [day with { Snapshot = snapshot }] } };
        Jurisdictions = [new(Month, null, office.OfficeGeofenceVersionId, office.JurisdictionCode, "configured", Branch)];
    }
    private Task<AttendancePayrollInputCalculation> Calculate() { Calculations++; return CalculationFailure is null?Task.FromResult(Source):
        Task.FromException<AttendancePayrollInputCalculation>(new AuthenticationFailure(CalculationFailure,CalculationFailure=="access_denied"?403:409)); }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private object Transaction(MethodInfo method, object?[] args) => GetType().GetMethod(nameof(TransactionCore), BindingFlags.Instance | BindingFlags.NonPublic)!
        .MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args)!;
    private async Task<T> TransactionCore<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    { TransactionDepth++; try { return await action(ct); } finally { TransactionDepth--; } }
}
