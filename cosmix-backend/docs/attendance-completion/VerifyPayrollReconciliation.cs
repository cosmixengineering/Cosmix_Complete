#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Policies;

// Standalone rules regression, with synthetic inputs; never opens SQL or a network connection.
var month = new DateOnly(2026, 10, 1);
var checks = 0;
AttendancePayrollInputDay Day(DateOnly date, int payable) => RulesFixture.Day(date, payable);
var prior = Day(month.AddDays(-1), 270);
var current1 = Day(month, 540);
var current2 = Day(month.AddDays(1), 540);
var unavailable = AttendancePayrollInputRules.Calculate(month, [prior, current1, current2]);
Check(unavailable.State == "unavailable" && unavailable.Issues.Any(x => x.Code == "prior_deduction_reconciliation_required"),
    "Missing settlement remains explicitly unavailable");
Check(unavailable.Lines.All(x => x.Kind != "late_threshold"),
    "Missing settlement must not expose a provisional late amount as a charge");
var noPriorOrdinary = AttendancePayrollInputRules.Calculate(month,
    [prior with { Time = prior.Time! with { DayStatus = AttendanceDayStatus.Present, PayableTicks = 540 * TimeSpan.TicksPerMinute } },
        current1, current2]);
Check(noPriorOrdinary.State == "unavailable" && noPriorOrdinary.Lines.All(x => x.Kind != "late_threshold"),
    "Prior no-charge candidate is not evidence of a settled no-charge source");
var sameMonth = AttendancePayrollInputRules.Calculate(month,
    [Day(month, 270), Day(month.AddDays(1), 540), Day(month.AddDays(2), 540)]);
Check(sameMonth.State == "ready" && sameMonth.Lines.Sum(x => x.Amount) == 1000m,
    "Same-month larger-only composition requires no prior settlement");
var noThreshold = AttendancePayrollInputRules.Calculate(month, [prior, current1]);
Check(noThreshold.State == "ready" && noThreshold.Lines.Count == 0,
    "An incomplete late threshold does not request unrelated financial evidence");
var priorOrdinary = AttendancePayrollInputRules.Calculate(new(2026, 9, 1), [prior]).Lines.Single();
Check(priorOrdinary.Amount == 516.67m, "Prior ordinary source uses its original September divisor");
var settled = Source(prior, priorOrdinary);
var reconciled = Reconcile([prior, current1, current2], settled);
Check(reconciled.State == "ready" && reconciled.Lines.Single().Amount == 483.33m &&
    reconciled.Lines.Single().OrdinaryComparisonAmount == 516.67m,
    "October charge is only the 483.33 excess over actual retained September ordinary");
var changedSalary = current2 with { Compensation = current2.Compensation! with
    { Id = Guid.NewGuid(), BaseAmount = 62000, GrossAmount = 62000, Fingerprint = new('D', 64) } };
Check(Reconcile([prior, current1, changedSalary], settled).Lines.Single().Amount == 1483.33m,
    "Completion-date salary changes the threshold without repricing settled prior ordinary");
var higherPrior = prior with { Compensation = prior.Compensation! with
    { Id = Guid.NewGuid(), BaseAmount = 62000, GrossAmount = 62000, Fingerprint = new('D', 64) } };
var higherOrdinary = AttendancePayrollInputRules.Calculate(new(2026, 9, 1), [higherPrior]).Lines.Single();
var suppressed = Reconcile([higherPrior, current1, current2], Source(higherPrior, higherOrdinary));
Check(higherOrdinary.Amount == 1033.33m && suppressed.State == "ready" && suppressed.Lines.Single().Amount == 0 &&
    suppressed.Lines.Single().ApplicationCode == "suppressed_by_ordinary",
    "A larger prior actual ordinary charge suppresses late without generating a credit");
var combined = Reconcile([prior, Day(month, 270), current2], settled);
Check(combined.State == "ready" && combined.Lines.Sum(x => x.Amount) == 500m &&
    combined.Lines.Single(x => x.Kind == "late_threshold").OrdinaryComparisonAmount == 1016.67m,
    "Prior actual and current ordinary compose larger-only, without duplicate ordinary");
var noChargeDay = prior with { Time = prior.Time! with
    { DayStatus = AttendanceDayStatus.Present, PayableTicks = 540 * TimeSpan.TicksPerMinute } };
Check(Reconcile([noChargeDay, current1, current2], Source(noChargeDay, null)).Lines.Single().Amount == 1000m,
    "Explicit verified retained no-ordinary day in a settled nonempty source can support the threshold");
void Unavailable(AttendancePayrollInputResult value, string name) => Check(value.State == "unavailable" &&
    value.Lines.All(x => x.Kind != "late_threshold"), name);
var confirmedPosting = (JsonObject)settled.DeepClone(); confirmedPosting["Status"] = "posted";
Check(Reconcile([prior, current1, current2], confirmedPosting).Lines.Single().Amount == 483.33m,
    "Explicit confirmed posting is authoritative without imposing a settlement stage");
foreach (var status in new[] { "handoff", "unknown" })
{
    var unconfirmed = (JsonObject)settled.DeepClone(); unconfirmed["Status"] = status;
    Unavailable(Reconcile([prior, current1, current2], unconfirmed), status + " is not confirmed actual posting");
}
foreach (var field in new[] { "SourceRevisionId", "ConfirmedAttestationId" })
{
    var missing = (JsonObject)settled.DeepClone(); missing[field] = Guid.Empty.ToString();
    Unavailable(Reconcile([prior, current1, current2], missing), "Missing " + field + " cannot prove deduction");
}
foreach (var field in new[] { "SourceFingerprint", "CompensationFingerprint", "SourceRevisionFingerprint", "ConfirmedEvidenceFingerprint" })
{
    var changed = (JsonObject)settled.DeepClone(); changed[field] = field is "SourceFingerprint" or "CompensationFingerprint" ? new string('F', 64) : "invalid";
    Unavailable(Reconcile([prior, current1, current2], changed), "Changed/invalid " + field + " is unavailable");
}
var wrongPolicy = (JsonObject)settled.DeepClone(); wrongPolicy["PolicyVersionId"] = Guid.NewGuid().ToString();
Unavailable(Reconcile([prior, current1, current2], wrongPolicy), "Different retained policy cannot substitute for the source day");
var wrongSalary = (JsonObject)settled.DeepClone(); wrongSalary["CompensationRecordId"] = Guid.NewGuid().ToString();
Unavailable(Reconcile([prior, current1, current2], wrongSalary), "Different retained salary cannot substitute for the source day");
var wrongAmount = (JsonObject)settled.DeepClone(); wrongAmount["OrdinaryLine"]!["Amount"] = 516.68m;
Unavailable(Reconcile([prior, current1, current2], wrongAmount), "One-cent source mismatch cannot alter actual ordinary comparison");
var missingLine = (JsonObject)settled.DeepClone(); missingLine["OrdinaryLine"] = null;
Unavailable(Reconcile([prior, current1, current2], missingLine), "Missing actual ordinary line is not zero");
Unavailable(Reconcile([prior, current1, current2], settled, settled), "Duplicate settled date cannot subtract twice");
var august = Day(new(2026, 8, 31), 270);
var augustOrdinary = AttendancePayrollInputRules.Calculate(new(2026, 8, 1), [august]).Lines.Single();
Check(Reconcile([august, prior, current1], Source(august, augustOrdinary), settled).Lines.Single().Amount == 0,
    "Two prior months contribute only their exact retained ordinary lines");
Unavailable(Reconcile([august, prior, current1], settled), "A missing prior month cannot be silently omitted");
Check(JsonSerializer.Serialize(Reconcile([prior, current1, current2], settled).Lines) ==
    JsonSerializer.Serialize(Reconcile([current2, prior, current1], settled).Lines), "Input ordering does not change deterministic reconciliation");
var manifest = new AttendancePayrollInputManifest(1, Guid.NewGuid(), month, month.AddMonths(1), month, month,
    AttendancePayrollInputRules.Version, new('A', 64), [new([new(EmployeeId: Guid.NewGuid())])], []);
var legacyManifest = new { manifest.SchemaVersion, manifest.EmployeeId, manifest.MonthStart, manifest.MonthEndExclusive,
    manifest.SourceFrom, manifest.SourceTo, manifest.CalculationVersion, manifest.CoverageFingerprint,
    manifest.OriginalScopes, manifest.Days };
Check(JsonSerializer.Serialize(manifest) == JsonSerializer.Serialize(legacyManifest),
    "Null reconciliation provenance preserves historical schema-1 manifest bytes");
Check(JsonSerializer.Serialize(manifest with { PriorConfirmedOrdinary = [settled.Deserialize<AttendancePayrollConfirmedOrdinaryDay>()!] }) !=
    JsonSerializer.Serialize(manifest), "Retained settlement changes target source fingerprint bytes");

var harness = new ReaderHarness();
var saved = await harness.Save("reconcile-source-save");
Check((await harness.Read()).Issues.Any(x => x.Code == "prior_deduction_posting_unavailable"),
    "A real saved protected source is not a confirmed deduction posting");
var handoff = await harness.Attest(saved, "handoff", null, "reconcile-handoff");
Check((await harness.Read()).Days.Count == 0, "Real handoff source reader cannot supply a monetary amount");
var posted = await harness.Attest(saved, "posted", handoff.Id, "reconcile-posted");
var actualPosted = await harness.Read();
Check(actualPosted.Days.Single().ConfirmedAttestationId == posted.Id && actualPosted.Days.Single().OrdinaryLine!.Amount == 516.67m,
    "Real posted-only source reader accepts explicit confirmed actual deduction");
harness.Calculation = harness.Calculation with { InputFingerprint = new('9', 64) };
var postedReplacement = await harness.Save("posted-source-replacement");
var pinnedPosting = await harness.Read();
Check(postedReplacement.Revision.Id != saved.Revision.Id && pinnedPosting.Days.Single().SourceRevisionId == saved.Revision.Id &&
    pinnedPosting.Days.Single().ConfirmedAttestationId == posted.Id,
    "A later saved replacement cannot change an already confirmed posted-only source");
var receipt = await harness.Attest(saved, "settled", posted.Id, "reconcile-settled");
var retained = await harness.Read();
Check(retained.Issues.Count == 0 && retained.Days.Single().OrdinaryLine!.Amount == 516.67m &&
    retained.Days.Single().SourceRevisionId == saved.Revision.Id && retained.Days.Single().ConfirmedAttestationId == receipt.Id,
    "Actual reader validates the exact saved source and complete real-service settlement chain");
Check(harness.MaximumDepth == 1 && harness.Depth == 0,
    "Source reader and root static validators never nest AttendanceOperation transactions");
var fromReader = AttendancePayrollInputRules.Calculate(month, [prior, current1, current2], retained.Days);
Check(fromReader.State == "ready" && fromReader.Lines.Single().Amount == 483.33m,
    "Domain consumes real-reader evidence for the exact larger-only amount");
harness.Calculation = harness.Calculation with { InputFingerprint = new('9', 64) };
var replacement = await harness.Save("reconcile-replacement");
var pinned = await harness.Read();
Check(replacement.Revision.Id != saved.Revision.Id && pinned.Days.Single().SourceRevisionId == saved.Revision.Id,
    "Latest saved replacement cannot replace a chain-pinned financial source");
async Task Denied(Func<Task> action, string code)
{
    try { await action(); }
    catch (AuthenticationFailure error) when (error.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
harness.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Read);
await Denied(async () => await harness.Read(), "access_denied");
harness.Grant(AttendancePayrollAttestationPermissions.Read, harness.Site);
harness.CurrentScopes = [new([new(harness.Employee, SiteId: Guid.NewGuid())])];
await Denied(async () => await harness.Read(), "access_denied");
harness.Grant(AttendancePayrollInputPermissions.Read, harness.CurrentScopes[0].Alternatives[0].SiteId!.Value);
harness.Grant(AttendancePayrollAttestationPermissions.Read, harness.CurrentScopes[0].Alternatives[0].SiteId!.Value);
Check((await harness.Read()).OriginalScopes.Count == 2, "Original and changed current source scopes are retained separately");
harness.Grants.RemoveAll(x => x.Permission == AttendancePayrollAttestationPermissions.Read && x.TargetId == harness.Site);
await Denied(async () => await harness.Read(), "access_denied");
harness.Grant(AttendancePayrollAttestationPermissions.Read, harness.Site);
var originalReadGrants = harness.Grants.Where(x => x.Permission == AttendancePayrollInputPermissions.Read && x.TargetId == harness.Site).ToArray();
harness.Grants.RemoveAll(x => originalReadGrants.Contains(x));
await Denied(async () => await harness.Read(), "access_denied");
harness.Grants.AddRange(originalReadGrants);
var reference = JsonSerializer.Deserialize<PrivateFileReference>(harness.Rows[0].ProtectedEvidenceReference)!;
var originalBytes = harness.Files[reference.StorageKey];
var tampered = JsonNode.Parse(originalBytes.Bytes)!.AsObject();
tampered["Calculation"]!["Lines"]![0]!["Amount"] = 516.68m;
harness.Files[reference.StorageKey] = (originalBytes.Purpose, JsonSerializer.SerializeToUtf8Bytes(tampered));
await Denied(async () => await harness.Read(), "payroll_input_evidence_invalid");
harness.Files[reference.StorageKey] = originalBytes;
var settlementReference = JsonSerializer.Deserialize<PrivateFileReference>(harness.AttestationRows.Last().ProtectedEvidenceReference)!;
var settlementBytes = harness.Files[settlementReference.StorageKey];
var corruptReceipt = JsonNode.Parse(settlementBytes.Bytes)!.AsObject(); corruptReceipt["Statement"] = "changed assertion";
harness.Files[settlementReference.StorageKey] = (settlementBytes.Purpose, JsonSerializer.SerializeToUtf8Bytes(corruptReceipt));
await Denied(async () => await harness.Read(), "payroll_attestation_evidence_invalid");
harness.Files[settlementReference.StorageKey] = settlementBytes;
harness.Identity = harness.Identity with { MfaSatisfied = false };
await Denied(async () => await harness.Read(), "access_denied");
harness.Identity = harness.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Denied(async () => await harness.Read(), "access_denied");
harness.Identity = harness.Identity with { Client = SessionClient.Web }; harness.SessionCurrent = false;
await Denied(async () => await harness.Read(), "invalid_authentication");
Console.WriteLine($"PASS: {checks} reconciliation rules/actual reader/source/receipt assertions; synthetic IO, no SQL/network writes.");

void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    checks++;
}

// Reflection resolves only the proposed public API, allowing the first red run against unchanged production.
AttendancePayrollInputResult Reconcile(AttendancePayrollInputDay[] days, params JsonObject[] sources)
{
    var method = typeof(AttendancePayrollInputRules).GetMethods().SingleOrDefault(x =>
        x.Name == "Calculate" && x.GetParameters().Length == 3)
        ?? throw new Exception("Missing settled-source reconciliation overload");
    var evidenceType = method.GetParameters()[2].ParameterType.GetGenericArguments().Single();
    var values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(evidenceType))!;
    foreach (var source in sources) values.Add(source.Deserialize(evidenceType)!);
    return (AttendancePayrollInputResult)method.Invoke(null, [month, days, values])!;
}

static JsonObject Source(AttendancePayrollInputDay day, AttendancePayrollInputLine? line) =>
    (JsonObject)JsonSerializer.SerializeToNode(new
    {
        day.BusinessDate, SourceMonthStart = new DateOnly(day.BusinessDate.Year, day.BusinessDate.Month, 1),
        SourceRevisionId = Guid.Parse($"20000000-0000-0000-0000-{day.BusinessDate.Year:D4}{day.BusinessDate.Month:D2}000001"),
        SourceRevisionFingerprint = new string('E', 64),
        ConfirmedAttestationId = Guid.Parse($"20000000-0000-0000-0000-{day.BusinessDate.Year:D4}{day.BusinessDate.Month:D2}000002"),
        ConfirmedEvidenceFingerprint = new string('F', 64), Status = "settled",
        SourceFingerprint = day.SourceFingerprint, day.PolicyVersionId,
        CompensationRecordId = day.Compensation!.Id, CompensationFingerprint = day.Compensation.Fingerprint,
        OrdinaryLine = line
    })!;

static class RulesFixture
{
public static AttendancePayrollInputDay Day(DateOnly date, int payable)
{
    var rules = new AttendancePolicyRules(3, new(900, 3600, 9000),
        new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
            AttendanceEarlyExitTreatment.HalfDayCredit, 16200, AttendanceNonScheduledDayTreatment.NoCredit,
            AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
        new(3, AttendanceLateResetPeriod.CalendarYear, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
            AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,
            AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate, true,
            AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
        new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, .5m,
            AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
    var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
    return new(date, "available", Guid.Parse("10000000-0000-0000-0000-000000000001"), rules,
        new(rules, start, start.AddHours(9), 540, 0, false, false, [], []),
        new("calculated", payable == 0 ? AttendanceDayStatus.Absent : payable == 270 ? AttendanceDayStatus.HalfDay : AttendanceDayStatus.Present,
            payable * TimeSpan.TicksPerMinute, TimeSpan.TicksPerMinute, true, []),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), 31000, 31000, "PKR", WageBasis.Monthly,
            new(2020, 1, 1), null, new('C', 64), []), new('A', 64), []);
}
}

public class ReaderProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var value = Create<T, ReaderProxy>(); ((ReaderProxy)(object)value).Handler = handler; return value;
    }
}

sealed class ReaderClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
}

sealed class ReaderHarness
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid();
    public DateOnly Month = new(2026, 9, 1), Date = new(2026, 9, 30);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-reconciliation");
    public bool SessionCurrent = true;
    public int Depth, MaximumDepth;
    public List<PermissionGrant> Grants = [];
    public List<AttendancePayrollInputRevision> Rows = [];
    public List<AttendancePayrollAttestation> AttestationRows = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AttendancePayrollInputCalculation Calculation;
    public IReadOnlyList<AttendanceResourceScope> CurrentScopes;
    public AttendancePayrollInputService Inputs;
    public AttendancePayrollAttestationService Attestations;
    public AttendanceOperation Operation;
    public AttendancePayrollReconciliationSourceReader Reader;
    public void Grant(string permission, Guid site) => Grants.Add(new(permission, true, Guid.NewGuid(), AccessScopeKind.Site, site));
    public ReaderHarness()
    {
        foreach (var permission in new[] { AttendancePayrollInputPermissions.Read, AttendancePayrollInputPermissions.Calculate,
            AttendancePayrollAttestationPermissions.Read, AttendancePayrollAttestationPermissions.Attest }) Grant(permission, Site);
        CurrentScopes = [new([new(Employee, SiteId: Site)])];
        var day = RulesFixture.Day(Date, 270);
        var timeInput = day.TimeInput!;
        var policy = new AttendancePolicyResolution("effective", day.PolicyVersionId, [], day.Rules,
            AttendancePolicyRulesCodec.Canonicalize(day.Rules!), new('B', 64));
        var snapshot = new AttendanceCalculationSnapshot(1, Employee, Date, AttendanceDailyTimeCalculation.Version,
            new('A', 64), new(Employee, Date, "scheduled", RosterSchedule.TimeZoneId, Site, Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), 1, timeInput.ShiftStartsAtUtc, timeInput.ShiftEndsAtUtc, 540, 0, 0),
            new(Guid.NewGuid(), Guid.NewGuid(), "SITE", Site, Month, null), policy,
            new(LeaveContextStatus.Complete, [], new('C', 64), []), [], [], [], [], timeInput, 0);
        var result = AttendancePayrollInputRules.Calculate(Month, [day]);
        Calculation = new(Employee, Month, Month.AddMonths(1), Date, Date, result.State, new('A', 64), result.Lines, result.Issues,
            new(1, Employee, Month, Month.AddMonths(1), Date, Date, AttendancePayrollInputRules.Version, new('E', 64),
                CurrentScopes, [new(Date, "available", day.SourceFingerprint, snapshot, day.Time, day.Compensation, policy)]));
        var current = ReaderProxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = ReaderProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization = ReaderProxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => Transaction(method, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ExplicitPayrollReader"), _ => throw new Exception(method.Name) });
        var journal = ReaderProxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! &&
                x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => Add(Audits, (AuditEvent)args[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(method.Name) });
        var files = ReaderProxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync")
            {
                var bytes = (byte[])args[0]!; var key = Guid.NewGuid().ToString("N"); Files[key] = ((string)args[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var stored = Files[((PrivateFileReference)args[0]!).StorageKey];
            if (stored.Purpose != (string)args[1]!) throw new Exception("Purpose mismatch");
            return Task.FromResult(stored.Bytes);
        });
        var clock = new ReaderClock();
        Operation = new(current, access, authorization, journal,
            ReaderProxy.Make<IAuthenticationRepository>((method, _) => throw new Exception(method.Name)), clock, files);
        var producer = ReaderProxy.Make<IAttendancePayrollInputProducer>((method, _) => method.Name switch {
            "ResourceScopesAsync" => Task.FromResult(CurrentScopes), "CalculateAsync" => Task.FromResult(Calculation),
            _ => throw new Exception(method.Name) });
        var ledger = ReaderProxy.Make<IAttendancePayrollInputLedger>((method, args) => method.Name switch {
            "IsLockedAsync" => Task.FromResult(false),
            "LatestAsync" => Task.FromResult(Rows.OrderByDescending(x => x.Revision).FirstOrDefault()),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.EmployeeId == (Guid)args[0]! && x.Id == (Guid)args[1]!)),
            "Add" => Add(Rows, (AttendancePayrollInputRevision)args[0]!), _ => throw new Exception(method.Name) });
        var receipts = ReaderProxy.Make<IAttendancePayrollAttestationRepository>((method, args) => method.Name switch {
            "HistoryAsync" => Task.FromResult<IReadOnlyList<AttendancePayrollAttestation>>(AttestationRows.OrderBy(x => x.Step).ToArray()),
            "ReceiptExistsAsync" => Task.FromResult(AttestationRows.Any(x => x.ExternalReceiptKey == (string)args[0]! && x.Step == (int)args[1]!)),
            "Add" => Add(AttestationRows, (AttendancePayrollAttestation)args[0]!), _ => throw new Exception(method.Name) });
        var summary = ReaderProxy.Make<IAttendanceSummaryRepository>((method, _) => method.Name == "ResourceScopesAsync"
            ? Task.FromResult(CurrentScopes) : throw new Exception(method.Name));
        // Stable jurisdiction revision keeps preview/save fingerprints deterministic.
        var jurisdictionId = Guid.NewGuid();
        var locations = ReaderProxy.Make<IAttendancePayrollJurisdictionReader>((_, _) =>
            Task.FromResult<IReadOnlyList<AttendancePayrollJurisdictionContext>>([new(Date, Site, jurisdictionId, "PK-PB", "configured")]));
        Inputs = new(Operation, producer, ledger, locations, files, access);
        Attestations = new(Operation, Inputs, ledger, producer, receipts, files, access);
        Reader = new(ledger, receipts, summary, Operation, current, access, journal, clock);
    }
    public async Task<AttendancePayrollInputDetail> Save(string key)
    {
        var preview = await Inputs.PreviewAsync(Employee, Month, Context, default);
        return await Inputs.SaveAsync(Employee, Month, new(preview.PreviewFingerprint, preview.ExpectedRevision,
            "Prepare exact prior ordinary source"), key, Context, default);
    }
    public Task<AttendancePayrollAttestationResponse> Attest(AttendancePayrollInputDetail source, string status, Guid? previous, string key)
    {
        var now = new ReaderClock().GetUtcNow();
        return Attestations.CreateAsync(Employee, Month, source.Revision.Id,
            new(source.Revision.Revision, source.Revision.InputFingerprint, previous, status, now,
                status == "handoff" ? null : now,
                source.Inputs.Lines.Select(x => new AttendancePayrollAttestationLine(x.Key, x.Amount, x.Currency)).ToArray(),
                "SYNTHETIC", "EMPLOYEE/SEP", "synthetic evidence", new('A', 64), "Explicit synthetic assertion",
                "Verify prior ordinary reconciliation"), key, Context, default);
    }
    public Task<AttendancePayrollReconciliationSources> Read() => Operation.ReadAsync(AttendancePayrollInputPermissions.Read,
        "attendance.payroll-reconciliation.verify", Employee, Context,
        (_, token) => Reader.ReadAsync(Employee, Month.AddMonths(1), [Date], token), default,
        resourceScopes: (_, _) => Task.FromResult(CurrentScopes));
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private object Transaction(MethodInfo method, object?[] args) => GetType().GetMethod(nameof(TransactionCore),
        BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args)!;
    private async Task<T> TransactionCore<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Depth != 0) throw new Exception("Nested authorization transaction");
        Depth++; MaximumDepth = Math.Max(MaximumDepth, Depth);
        try { return await action(ct); } finally { Depth--; }
    }
}
