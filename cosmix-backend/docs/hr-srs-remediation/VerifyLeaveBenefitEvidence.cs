#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;

var checks = 0;
var failures = new List<string>();
void Check(bool condition, string name)
{ checks++; if (!condition) { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
async Task Scenario(string name, Func<Task> action)
{ try { await action(); } catch (Exception e) { Check(false, name + ": " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message); } }
async Task Reject(string name, Func<Task> action)
{
    try { await action(); Check(false, name + " accepted malformed evidence"); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == 409, name + " must report an explicit conflict, got " + e.Code); }
}

foreach (var cash in new[] { false, true })
    await Scenario("valid award", async () => {
        var f = new EvidenceFixture(cash);
        var credits = await f.Read();
        Check(credits.Count == 1 && credits[0].AwardId == f.Entry.Id && credits[0].Minutes == 140 &&
            credits[0].StandardDayMinutes == 420 && credits[0].Encashable == cash,
            "Verified award retains source-day denominator and explicit cash eligibility " + cash);
        Check((await f.Read(new DateOnly(2026, 7, 1))).Count == 0, "Award is unavailable before Karachi award date");
        Check((await f.Read(new DateOnly(2026, 7, 2))).Count == 1, "Award is available exactly on Karachi award date");
    });

foreach (var cultureName in new[] { "ar-SA", "th-TH" })
    await Scenario("invariant source key " + cultureName, async () => {
        var f = new EvidenceFixture();
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            Check(LeaveBenefitEvidence.SourceKey(new DateOnly(2025, 7, 1)) == "leave-benefit:2025-07-01",
                cultureName + " source identity uses the literal Gregorian invariant date");
            Check((await f.Read()).Single().AwardId == f.Entry.Id,
                cultureName + " validator accepts the saved invariant ledger key");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    });

var mutations = new (string Name, Action<JsonObject> Change)[] {
    ("unsupported proof schema", p => p["SchemaVersion"] = 2),
    ("wrong award identity", p => p["Id"] = Guid.NewGuid().ToString()),
    ("foreign employee", p => p["EmployeeId"] = Guid.NewGuid().ToString()),
    ("foreign leave type", p => p["LeaveTypeId"] = Guid.NewGuid().ToString()),
    ("changed minutes", p => p["Minutes"] = 141),
    ("changed target year", p => p["YearTo"] = "2028-07-01"),
    ("backdated applicability", p => p["AvailableFrom"] = "2026-07-01"),
    ("self award", p => p["ActorEmployeeId"] = p["EmployeeId"]!.DeepClone()),
    ("missing actor employee", p => p["ActorEmployeeId"] = Guid.Empty.ToString()),
    ("non Admin award", p => p["ActorRole"] = "SuperAdmin"),
    ("changed actor account", p => p["ActorAccountId"] = Guid.NewGuid().ToString()),
    ("changed reason", p => p["Reason"] = "Unbound reason"),
    ("missing policy", p => p["Policy"] = null),
    ("missing KPI", p => p["Kpi"] = null),
    ("missing final score", p => p["Kpi"]!["Score"] = null),
    ("different policy version", p => p["Policy"]!["VersionId"] = Guid.NewGuid().ToString()),
    ("empty policy identity", p => p["Policy"]!["PolicyId"] = Guid.Empty.ToString()),
    ("unassigned policy category", p => p["Policy"]!["StaffCategoryId"] = Guid.NewGuid().ToString()),
    ("missing assignment evidence", p => p["Policy"]!["AssignmentIds"] = new JsonArray()),
    ("policy starts after award", p => p["Policy"]!["EffectiveFrom"] = "2026-07-03"),
    ("policy ends at award", p => p["Policy"]!["EffectiveTo"] = "2026-07-02"),
    ("policy published after award", p => p["Policy"]!["PublishedAt"] = "2026-07-03T00:00:00+00:00"),
    ("service eligibility missing", p => p["Policy"]!["JoinedOn"] = "2026-07-02"),
    ("KPI belongs to another employee", p => p["Kpi"]!["Score"]!["EmployeeId"] = Guid.NewGuid().ToString()),
    ("missing finalization identity", p => p["Kpi"]!["Score"]!["Id"] = Guid.Empty.ToString()),
    ("unsupported final score schema", p => p["Kpi"]!["Score"]!["SchemaVersion"] = 2),
    ("missing KPI request identity", p => p["Kpi"]!["Score"]!["RequestId"] = Guid.Empty.ToString()),
    ("missing KPI decision identity", p => p["Kpi"]!["Score"]!["DecisionId"] = Guid.Empty.ToString()),
    ("invalid KPI revision", p => p["Kpi"]!["Score"]!["Revision"] = 0),
    ("below exact KPI threshold", p => p["Kpi"]!["Score"]!["ExactScore"] = 79.999999m),
    ("KPI exceeds percentage", p => p["Kpi"]!["Score"]!["ExactScore"] = 100.000001m),
    ("KPI finalized after award", p => p["Kpi"]!["Score"]!["FinalizedAtUtc"] = "2026-07-03T00:00:00+00:00"),
    ("incomplete source year", p => p["Kpi"]!["Score"]!["ToExclusive"] = "2026-06-30"),
    ("out of range source year", p => p["Kpi"]!["Score"]!["From"] = "9999-01-01"),
    ("unverified fingerprint", p => p["Kpi"]!["VerifiedSourceFingerprint"] = new string('C', 64)),
    ("empty matching fingerprints", p => { p["Kpi"]!["VerifiedSourceFingerprint"] = ""; p["Kpi"]!["Score"]!["SourceFingerprint"] = ""; })
};
foreach (var mutation in mutations)
    await Scenario(mutation.Name, async () => {
        var f = new EvidenceFixture(); f.ChangeProof(mutation.Change);
        await Reject(mutation.Name, () => f.Reader.AwardAsync(f.Rows[0], default));
    });

await Scenario("missing typed audit", async () => {
    var f = new EvidenceFixture(); f.Rows[0] = f.Rows[0] with { Evidence = null };
    await Reject("Ledger credit without audit", () => f.Read());
});
await Scenario("audit target binding", async () => {
    var f = new EvidenceFixture();
    f.Rows[0] = f.Rows[0] with { Evidence = new AuditEvent(LeaveBenefitEvidence.AwardEvent, f.Actor.ToString(), "Admin",
        LeaveBenefitEvidence.TargetType, Guid.NewGuid().ToString(), f.Now, "synthetic", employeeScopeId: f.Employee,
        protectedChangesReference: f.Rows[0].Evidence!.ProtectedChangesReference) };
    await Reject("Audit for another ledger entry", () => f.Read());
});
await Scenario("raw malformed JSON", async () => {
    var f = new EvidenceFixture(); f.RawProof = "{";
    await Reject("Malformed protected JSON", () => f.Read());
});
await Scenario("duplicate ledger identity", async () => {
    var f = new EvidenceFixture(); f.Rows.Add(f.Rows[0]);
    await Reject("Repeated ledger row", () => f.Read());
});
await Scenario("duplicate source across policy versions", async () => {
    var f = new EvidenceFixture(); f.AddSecondAward();
    await Reject("Same source year under another policy", () => f.Read());
});
await Scenario("foreign year response", async () => {
    var f = new EvidenceFixture();
    await Reject("Mismatched selected year", () => f.Reader.ReadAsync(f.Employee, new(2027, 7, 1), new(2028, 7, 1), new(2027, 7, 2), default));
});
await Scenario("revocation", async () => {
    var f = new EvidenceFixture(); var original = f.Proof; f.Reverse();
    Check((await f.Read()).Count == 0, "Verified exact reversal removes credit");
    Check(f.Entry.MinutesDelta == 140 && f.Proof == original, "Revocation does not mutate the original award");
});
foreach (var mode in new[] { "orphan", "duplicate", "partial", "wrong-fingerprint", "missing-audit", "before-award", "self", "missing-actor" })
    await Scenario("reversal " + mode, async () => {
        var f = new EvidenceFixture(); f.Reverse(mode);
        await Reject("Reversal " + mode, () => f.Read());
    });

// Hand-calculated fractions catch denominator substitution and cross-kind annual leakage.
await Scenario("quota", () => {
    var credit = new LeaveBenefitCredit(Guid.NewGuid(), 140, 420, LeaveKind.Sick, false);
    var empty = new LeaveBalanceUsage(0, 0, []) { Credits = [credit] };
    Check(!LeaveTreatmentRules.ExceedsDays(empty, LeaveKind.Sick, 160, 480, 0, 0), "140/420 source days allow exactly 160/480 current days");
    Check(LeaveTreatmentRules.ExceedsDays(empty, LeaveKind.Sick, 161, 480, 0, 0), "Fractional credits never round upward");
    Check(LeaveTreatmentRules.ExceedsDays(empty, LeaveKind.Casual, 1, 480, 0, 10), "Unused foreign-kind credit cannot raise annual capacity");
    var consumed = new LeaveBalanceUsage(70, 0, [new(70, 420, LeaveKind.Sick)]) { Credits = [credit] };
    Check(!LeaveTreatmentRules.ExceedsDays(consumed, LeaveKind.Casual, 480, 480, 1, 1), "Foreign-kind credit offsets that kind's recorded use");
    Check(LeaveTreatmentRules.ExceedsDays(consumed, LeaveKind.Casual, 481, 480, 1, 2), "Unused portion of foreign-kind credit remains ring-fenced");
    var released = new LeaveBalanceUsage(0, 0, [new(70, 420, LeaveKind.Sick), new(-70, 420, LeaveKind.Sick)]) { Credits = [credit] };
    Check(LeaveTreatmentRules.ExceedsDays(released, LeaveKind.Casual, 481, 480, 1, 2), "Released usage cannot keep foreign-kind credit available");
    var balance = LeaveTreatmentRules.Balances(empty, LeaveKind.Sick, 0, 0);
    Check(balance.Annual.UsedDays == new LeaveExactDays("0", "1") && balance.Type.RemainingDays == new LeaveExactDays("1", "3"),
        "Credits increase entitlement without making factual usage negative");
    return Task.CompletedTask;
});
await Scenario("invalid quota credits", async () => {
    var credit = new LeaveBenefitCredit(Guid.NewGuid(), 1, 480, LeaveKind.Other, true);
    foreach (var credits in new IReadOnlyList<LeaveBenefitCredit>[] { [credit, credit], [credit with { Minutes = -1 }], [credit with { StandardDayMinutes = 0 }] })
        await Reject("Malformed credit units", () => { LeaveTreatmentRules.Balances(new(0, 0, []) { Credits = credits }, LeaveKind.Other, 1, 1); return Task.CompletedTask; });
});
await Scenario("legacy proof digest", () => {
    const string json = """
    {"RequestId":"11111111-1111-1111-1111-111111111111","Decision":1,"Treatment":1,"PolicyVersionId":"22222222-2222-2222-2222-222222222222","SubmissionDigest":"retained","PolicyRulesJson":null,"StaffCategoryId":null,"JoinedOn":null,"Holidays":[],"Usage":{"AnnualMinutes":480,"TypeMinutes":480,"Units":[{"UsedMinutes":480,"StandardDayMinutes":480,"Kind":0}]}}
    """;
    var oldShape = JsonSerializer.Deserialize<JsonElement>(json);
    var rehydrated = JsonSerializer.Deserialize<LeaveDecisionProof>(json)!;
    Check(LeaveProvenance.Digest(oldShape) == LeaveProvenance.Digest(rehydrated), "Legacy decision proof digest survives missing Credits field");
    Check(KpiMissingEvidenceRules.Hash(new { originalDecisionProofs = new[] { oldShape } }) ==
        KpiMissingEvidenceRules.Hash(new { originalDecisionProofs = new[] { rehydrated } }), "Legacy KPI source hash retains embedded decision-proof shape");
    var withCredit = rehydrated with { Usage = rehydrated.Usage! with { Credits = [new(Guid.NewGuid(), 140, 420, LeaveKind.Other, true)] } };
    Check(LeaveProvenance.Digest(withCredit) != LeaveProvenance.Digest(rehydrated), "New nonempty credits remain cryptographically bound");
    return Task.CompletedTask;
});

Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks - failures.Count}/{checks} leave benefit evidence/quota checks. Real validators; synthetic private storage; no SQL or host.");
return failures.Count == 0 ? 0 : 1;

public class EvidenceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, EvidenceProxy>(); ((EvidenceProxy)(object)value).Handler = handler; return value; }
}
public sealed class EvidenceFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Actor = Guid.NewGuid(), ActorEmployee = Guid.NewGuid();
    public readonly DateTimeOffset Now = new(2026, 7, 1, 19, 0, 0, TimeSpan.Zero);
    public readonly List<LeaveBenefitRow> Rows = [];
    public readonly Dictionary<string, (string Purpose, string Json)> Files = [];
    public LeaveLedgerEntry Entry;
    public LeaveBenefitAwardProof Proof;
    public LeaveBenefitEvidence Reader;
    public string? RawProof;
    private string proofStorageKey = "";
    public EvidenceFixture(bool cash = false)
    {
        var category = Guid.NewGuid(); var policy = Guid.NewGuid(); var type = Guid.NewGuid();
        var settings = new LeavePolicySettings(18, 6, 12, 4, 420, [category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Sick]) {
            SettingsVersion = 4, YearStartMonth = 7, YearStartDay = 1,
            Benefits = new(LeaveKind.Sick, 4, 80m, 140, 1, 30, cash),
            Encashment = cash ? new([LeaveKind.Sick], 4, 80m, 4800, 30, 1m, 2, MidpointRounding.ToEven,
                "PKR", EncashmentValuationDate.LastDayOfLeaveYear, 366) : null
        };
        Entry = new(Employee, type, new(2026, 7, 1), new(2027, 7, 1), LeaveLedgerEntryType.Entitlement, 140, null, null,
            policy, "Synthetic award", "leave-benefit:2025-07-01", Actor.ToString(), Now);
        var source = new EncashmentPolicySource(Guid.NewGuid(), policy, "leave-settings-v4", JsonSerializer.Serialize(settings),
            new(2026, 7, 1), new(2027, 7, 1), Now.AddDays(-7), Actor.ToString(), category, new(2020, 1, 1), [Guid.NewGuid()]);
        var score = new FinalizedKpiScore(1, Guid.NewGuid(), Employee, new(2025, 7, 1), new(2026, 7, 1), 1, null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 80m, 80m, new string('A', 64), new string('B', 64),
            Guid.NewGuid(), Now.AddHours(-1), [new([new AccessResource(EmployeeId: Employee)])]);
        Proof = new(1, Entry.Id, Employee, type, LeaveKind.Sick, Entry.LeaveYearStart, Entry.LeaveYearEnd, new(2026, 7, 2),
            140, source, new(score, score.SourceFingerprint, []), Actor, ActorEmployee, "Admin", Now, Entry.Reason);
        Rows.Add(Attach(Entry, Proof, LeaveBenefitEvidence.AwardEvent));
        proofStorageKey = JsonSerializer.Deserialize<PrivateFileReference>(Rows[0].Evidence!.ProtectedChangesReference!)!.StorageKey;
        var files = EvidenceProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name != "ReadAsync") throw new InvalidOperationException("Read-only evidence attempted " + m.Name);
            var reference = (PrivateFileReference)a[0]!; var purpose = (string)a[1]!;
            if (!Files.TryGetValue(reference.StorageKey, out var saved) || saved.Purpose != purpose)
                throw AttendanceOperation.Conflict("private_evidence_unavailable");
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(reference.StorageKey == proofStorageKey && RawProof is not null ? RawProof : saved.Json));
        });
        var repo = EvidenceProxy.Make<ILeaveBenefitRepository>((m, a) => m.Name == "YearAsync"
            ? Task.FromResult<IReadOnlyList<LeaveBenefitRow>>(Rows.ToArray()) : throw new InvalidOperationException("Unexpected evidence repository call " + m.Name));
        Reader = new(new AttendanceOperation(null!, null!, null!, null!, null!, TimeProvider.System, files), repo);
    }
    public Task<IReadOnlyList<LeaveBenefitCredit>> Read(DateOnly? useFrom = null) =>
        Reader.ReadAsync(Employee, new(2026, 7, 1), new(2027, 7, 1), useFrom ?? new DateOnly(2026, 7, 2), default);
    public void ChangeProof(Action<JsonObject> change)
    {
        var json = JsonSerializer.SerializeToNode(Proof)!.AsObject(); change(json);
        var saved = Files[proofStorageKey]; Files[proofStorageKey] = (saved.Purpose, json.ToJsonString());
    }
    public void AddSecondAward()
    {
        var other = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart, Entry.LeaveYearEnd,
            LeaveLedgerEntryType.Entitlement, 140, null, null, Guid.NewGuid(), Entry.Reason, Entry.IdempotencyKey, Actor.ToString(), Now);
        var proof = Proof with { Id = other.Id, Policy = Proof.Policy with { VersionId = other.PolicyVersionId } };
        Rows.Add(Attach(other, proof, LeaveBenefitEvidence.AwardEvent));
    }
    public void Reverse(string mode = "valid")
    {
        var when = mode == "before-award" ? Now.AddTicks(-1) : Now.AddHours(1);
        var entry = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart, Entry.LeaveYearEnd,
            LeaveLedgerEntryType.Adjustment, mode == "partial" ? -139 : -140, null,
            mode == "orphan" ? Guid.NewGuid() : Entry.Id, Entry.PolicyVersionId, "Synthetic revocation",
            "leave-benefit-revoke:" + Entry.Id.ToString("N"), Actor.ToString(), when);
        var proof = new LeaveBenefitRevocationProof(1, entry.Id, Entry.Id, Employee,
            mode == "wrong-fingerprint" ? new string('0', 64) : LeaveBenefitEvidence.Fingerprint(Proof), Actor,
            mode == "self" ? Employee : mode == "missing-actor" ? Guid.Empty : ActorEmployee, "Admin", when, entry.Reason);
        var row = Attach(entry, proof, LeaveBenefitEvidence.RevokeEvent);
        Rows.Add(mode == "missing-audit" ? row with { Evidence = null } : row);
        if (mode == "duplicate") Rows.Add(row with { Entry = new LeaveLedgerEntry(Employee, Entry.LeaveTypeId, Entry.LeaveYearStart,
            Entry.LeaveYearEnd, LeaveLedgerEntryType.Adjustment, -140, null, Entry.Id, Entry.PolicyVersionId,
            entry.Reason, entry.IdempotencyKey, Actor.ToString(), when) });
    }
    private LeaveBenefitRow Attach<T>(LeaveLedgerEntry entry, T proof, string eventType)
    {
        var json = JsonSerializer.Serialize(proof); var bytes = System.Text.Encoding.UTF8.GetBytes(json); var key = Guid.NewGuid().ToString("N");
        Files.Add(key, (LeaveBenefitEvidence.Purpose(Employee, entry.Id), json));
        var reference = AttendanceOperation.Reference(new(key, "synthetic", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
        return new(entry, new(eventType, Actor.ToString(), "Admin", LeaveBenefitEvidence.TargetType, entry.Id.ToString(), entry.RecordedAt,
            "synthetic-evidence", employeeScopeId: Employee, protectedChangesReference: reference));
    }
}
