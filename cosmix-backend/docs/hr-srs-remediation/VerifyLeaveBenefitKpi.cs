// Standalone: compile this file and LeaveBenefitKpiReader.cs against frozen Application/Domain DLLs.
// No project build, host, SQL, provider, configuration or persisted business writes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Policies;

internal static class VerifyLeaveBenefitKpi
{
    private static int checks, failures;
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) { failures++; Console.WriteLine("FAIL: " + name); }
    }
    private static async Task Scenario(string name, Func<Task> run)
    {
        try { await run(); }
        catch (Exception e) { Check(false, name + ": " + e.GetBaseException().Message); }
    }
    private static async Task Reject(string name, Func<Task> run, string code)
    {
        try { await run(); Check(false, name + " accepted invalid evidence"); }
        catch (AuthenticationFailure e) { Check(e.StatusCode == 409 && e.Code == code, name + ": expected " + code + ", got " + e.Code); }
    }
    public static async Task<int> Main()
    {
        await Scenario("exact evidence", async () => {
            var f = new BenefitKpiFixture(); var result = await f.Read();
            Check(result.Score == f.Final && result.Score.ExactScore == 83.456789m && result.Score.DisplayScore == 83.46m,
                "Retains the exact finalized record and all score precision");
            Check(result.VerifiedSourceFingerprint == new string('B', 64) && result.ResolvedDisputeIds.Count == 0,
                "Returns verified fingerprint and empty resolved-dispute history");
        });
        await Scenario("no benefit threshold", async () => {
            var f = new BenefitKpiFixture(score: 1m);
            Check((await f.Read()).Score.ExactScore == 1m, "Low verified scores are evidence, without inventing an award threshold");
        });
        foreach (var dates in new[] {
            (new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)),
            (new DateOnly(2025, 4, 1), new DateOnly(2026, 4, 2)),
            (new DateOnly(2025, 4, 1), new DateOnly(2025, 4, 1)),
            (new DateOnly(2025, 4, 1), new DateOnly(2024, 4, 1)),
            (default(DateOnly), new DateOnly(2, 1, 1)),
            (new DateOnly(9999, 1, 1), new DateOnly(9999, 12, 31)) })
            await Scenario("annual interval", async () => {
                var f = new BenefitKpiFixture();
                await Reject("Incomplete or invalid annual interval", () => f.Read(dates.Item1, dates.Item2), "leave_benefit_year_invalid");
            });
        await Scenario("completed local year boundary", async () => {
            var f = new BenefitKpiFixture();
            var boundary = new DateTimeOffset(2026, 3, 31, 19, 0, 0, TimeSpan.Zero);
            f.Final = f.Final! with { FinalizedAtUtc = boundary };
            await Reject("One tick before Karachi year end", () => f.Read(now: boundary.AddTicks(-1)), "leave_benefit_completed_year_required");
            Check((await f.Read(now: boundary)).Score.FinalizedAtUtc == boundary, "Completed year and finalization exactly at now are accepted");
        });
        await Scenario("leap-year coverage", async () => {
            var f = new BenefitKpiFixture(new DateOnly(2023, 4, 1));
            Check((await f.Read()).Score.ToExclusive == new DateOnly(2024, 4, 1), "Complete 366-day KPI year is accepted");
        });
        await Scenario("missing final", async () => {
            var f = new BenefitKpiFixture(); f.Final = null;
            await Reject("Missing finalized KPI", () => f.Read(), "leave_benefit_finalized_kpi_required");
        });
        await Scenario("future final", async () => {
            var f = new BenefitKpiFixture(); f.Final = f.Final! with { FinalizedAtUtc = f.Now.AddTicks(1) };
            await Reject("Future finalization", () => f.Read(), "leave_benefit_kpi_ineligible");
        });
        var badFinals = new (string Name, Func<FinalizedKpiScore, FinalizedKpiScore> Change)[] {
            ("Employee", x => x with { EmployeeId = Guid.NewGuid() }),
            ("Year", x => x with { From = x.From.AddYears(-1), ToExclusive = x.ToExclusive.AddYears(-1) }),
            ("End", x => x with { ToExclusive = x.ToExclusive.AddDays(-1) }),
            ("Schema", x => x with { SchemaVersion = 2 }),
            ("Final identity", x => x with { Id = Guid.Empty }),
            ("Revision", x => x with { Revision = 0 }),
            ("Decision", x => x with { DecisionId = Guid.Empty }),
            ("Policy", x => x with { PolicyVersionId = Guid.Empty }),
            ("Exact score", x => x with { ExactScore = 101m }),
            ("Display score", x => x with { DisplayScore = -1m }),
            ("Fingerprint", x => x with { SourceFingerprint = "unverified" }),
            ("UTC", x => x with { FinalizedAtUtc = x.FinalizedAtUtc.ToOffset(TimeSpan.FromHours(5)) }),
            ("Scope", x => x with { OriginalScopes = [] })
        };
        foreach (var entry in badFinals) await Scenario(entry.Name, async () => {
            var f = new BenefitKpiFixture(); f.Final = entry.Change(f.Final!);
            await Reject("Final " + entry.Name, () => f.Read(), "kpi_dispute_final_evidence_conflict");
        });
        foreach (var offset in new[] { 0, -1, 11 }) await Scenario("overlapping recomputation", async () => {
            var f = new BenefitKpiFixture(); var start = f.From.AddMonths(offset); f.Pending(start, start.AddYears(1));
            await Reject("Overlapping pending recomputation", () => f.Read(), "leave_benefit_kpi_review_pending");
        });
        await Scenario("adjacent pending windows", async () => {
            var f = new BenefitKpiFixture(); f.Pending(f.From.AddYears(-1), f.From); f.Pending(f.To, f.To.AddYears(1));
            Check((await f.Read()).Score.Id == f.Final!.Id, "Half-open neighboring recomputations do not block this year");
        });
        foreach (var malformed in new[] { "employee", "source", "json" }) await Scenario("pending " + malformed, async () => {
            var f = new BenefitKpiFixture(); f.Pending(f.To, f.To.AddYears(1), malformed);
            await Reject("Malformed pending evidence", () => f.Read(), malformed == "json" ? "private_evidence_unavailable" : "leave_benefit_kpi_pending_evidence_conflict");
        });
        var malformedPending = new (string Name, Func<KpiRequestSnapshot, KpiRequestSnapshot> Change)[] {
            ("foreign source employee", x => x with { Source = x.Source with { EmployeeId = Guid.NewGuid() } }),
            ("empty source employee", x => x with { Source = x.Source with { EmployeeId = Guid.Empty } }),
            ("default source start", x => x with { AsOf = new DateOnly(1, 12, 31), Source = x.Source with { From = default, ToExclusive = new DateOnly(2, 1, 1) } }),
            ("overflow source start", x => x with { AsOf = DateOnly.MaxValue.AddDays(-1), Source = x.Source with { From = new DateOnly(9999, 1, 1), ToExclusive = DateOnly.MaxValue } }),
            ("empty source interval", x => x with { AsOf = x.Source.From.AddDays(-1), Source = x.Source with { ToExclusive = x.Source.From } }),
            ("reversed source interval", x => x with { AsOf = x.Source.From.AddDays(-2), Source = x.Source with { ToExclusive = x.Source.From.AddDays(-1) } }),
            ("short source year", x => x with { AsOf = x.AsOf.AddDays(-1), Source = x.Source with { ToExclusive = x.Source.ToExclusive.AddDays(-1) } }),
            ("long source year", x => x with { AsOf = x.AsOf.AddDays(1), Source = x.Source with { ToExclusive = x.Source.ToExclusive.AddDays(1) } }),
            ("non-month source start", x => x with { AsOf = x.AsOf.AddDays(1), Source = x.Source with { From = x.Source.From.AddDays(1), ToExclusive = x.Source.ToExclusive.AddDays(1) } }),
            ("snapshot schema", x => x with { SchemaVersion = 2 }),
            ("empty requester", x => x with { RequesterAccountId = Guid.Empty }),
            ("foreign requester", x => x with { RequesterAccountId = Guid.NewGuid() }),
            ("request as-of", x => x with { AsOf = x.AsOf.AddDays(-1) }),
            ("overflow request as-of", x => x with { AsOf = DateOnly.MaxValue })
        };
        foreach (var entry in malformedPending) await Scenario("unrelated pending " + entry.Name, async () => {
            var f = new BenefitKpiFixture(); f.Pending(f.To, f.To.AddYears(1));
            f.Tamper(f.PendingRows[0].SnapshotPrivateReference, entry.Change);
            await Reject("Unrelated pending " + entry.Name, () => f.Read(), "leave_benefit_kpi_pending_evidence_conflict");
        });
        foreach (var damage in new[] { "request type", "request subject", "requester binding", "closed request" })
            await Scenario("pending authoritative " + damage, async () => {
                var f = new BenefitKpiFixture(); f.Pending(f.To, f.To.AddYears(1)); var original = f.PendingRows[0];
                var row = new ApprovalRequest(damage == "request type" ? "OtherApproval" : original.RequestType,
                    damage == "request subject" ? Guid.NewGuid() : original.SubjectId,
                    damage == "requester binding" ? Guid.NewGuid().ToString() : original.RequesterUserId,
                    original.SnapshotPrivateReference, 1, original.RequestedAt);
                if (damage == "closed request") row.Cancel(row.RequesterUserId, row.RequestedAt);
                f.PendingRows[0] = row;
                await Reject("Pending " + damage, () => f.Read(), "leave_benefit_kpi_pending_evidence_conflict");
            });
        await Scenario("pending revision bound", async () => {
            var f = new BenefitKpiFixture(); f.Pending(f.To, f.To.AddYears(1));
            while (f.PendingRows.Count < 128) f.PendingRows.Add(f.PendingRows[0]);
            Check((await f.Read()).Score.Id == f.Final!.Id, "128 non-overlapping pending revisions fit the existing bound");
            f.PendingRows.Add(f.PendingRows[0]);
            await Reject("129 pending revisions", () => f.Read(), "kpi_revision_limit");
        });
        foreach (var outcome in new KpiDisputeOutcome?[] { null, KpiDisputeOutcome.Returned, KpiDisputeOutcome.CorrectionRequired })
            await Scenario("unresolved dispute", async () => {
                var f = new BenefitKpiFixture(); f.Dispute(outcome);
                await Reject("Unresolved " + (outcome?.ToString() ?? "Pending"), () => f.Read(), "leave_benefit_kpi_dispute_unresolved");
            });
        await Scenario("verified dispute identities", async () => {
            var f = new BenefitKpiFixture(); var a = f.Dispute(KpiDisputeOutcome.ReviewedNoChange); var b = f.Dispute(KpiDisputeOutcome.ReviewedNoChange);
            Check((await f.Read()).ResolvedDisputeIds.SequenceEqual(new[] { a.Request.Id, b.Request.Id }.Order()),
                "Resolved IDs contain all verified reviews in deterministic order");
        });
        await Scenario("returned resubmission chain", async () => {
            var f = new BenefitKpiFixture(); var returned = f.Dispute(KpiDisputeOutcome.Returned);
            var resubmission = f.Dispute(KpiDisputeOutcome.ReviewedNoChange, returned.Request.Id);
            Check((await f.Read()).ResolvedDisputeIds.SequenceEqual(new[] { returned.Request.Id, resubmission.Request.Id }.Order()),
                "Returned dispute with a resolved resubmission retains both identities");
        });
        await Scenario("pending resubmission", async () => {
            var f = new BenefitKpiFixture(); var returned = f.Dispute(KpiDisputeOutcome.Returned); f.Dispute(null, returned.Request.Id);
            await Reject("Returned chain with pending child", () => f.Read(), "leave_benefit_kpi_dispute_unresolved");
        });
        await Scenario("verified amendment", async () => {
            var f = new BenefitKpiFixture(); var correction = f.Dispute(KpiDisputeOutcome.CorrectionRequired);
            var previous = f.Final!; var amended = previous with { Id = Guid.NewGuid(), Revision = 2, SupersedesId = previous.Id, FinalizedAtUtc = f.Now.AddDays(-1) };
            f.Amend(correction, amended);
            await Reject("Amendment newer than selected final", () => f.Read(), "leave_benefit_kpi_dispute_unresolved");
            f.Final = amended;
            Check((await f.Read()).ResolvedDisputeIds.SequenceEqual(new[] { correction.Request.Id }), "Verified amendment included in selected final resolves correction");
        });
        foreach (var damage in new[] { "audit", "response", "amendment" }) await Scenario("unverified dispute " + damage, async () => {
            var f = new BenefitKpiFixture();
            var row = f.Dispute(damage == "amendment" ? KpiDisputeOutcome.CorrectionRequired : KpiDisputeOutcome.ReviewedNoChange);
            if (damage == "audit") f.Audits.Clear();
            if (damage == "response") f.Tamper<KpiDisputeResponseSnapshot>(row.Decisions[0].ProtectedChangeReference!, x => x with { DraftDigest = new string('F', 64) });
            if (damage == "amendment") {
                var amended = f.Final! with { Id = Guid.NewGuid(), Revision = 2, SupersedesId = Guid.NewGuid(), FinalizedAtUtc = f.Now.AddDays(-1) };
                f.Amend(row, amended); f.Final = amended;
            }
            await Reject("Damaged dispute " + damage, () => f.Read(), "kpi_dispute_evidence_conflict");
        });
        await Scenario("dispute history bound", async () => {
            var f = new BenefitKpiFixture(); var row = f.Dispute(KpiDisputeOutcome.ReviewedNoChange);
            f.YearRows = Enumerable.Repeat(row.Request, 129).ToArray();
            await Reject("Oversized dispute history", () => f.Read(), "kpi_dispute_history_limit");
        });
        var changedFinals = new (string Name, Func<FinalizedKpiScore, FinalizedKpiScore> Change)[] {
            ("source fingerprint", x => x with { SourceFingerprint = new string('C', 64) }),
            ("calculation fingerprint", x => x with { CalculationFingerprint = new string('C', 64) }),
            ("policy version", x => x with { PolicyVersionId = Guid.NewGuid() }),
            ("exact score", x => x with { ExactScore = 83.456788m }),
            ("display score", x => x with { DisplayScore = 83.45m })
        };
        foreach (var entry in changedFinals) await Scenario("source mismatch " + entry.Name, async () => {
            var f = new BenefitKpiFixture(); f.Final = entry.Change(f.Final!);
            await Reject("Changed " + entry.Name, () => f.Read(), "leave_benefit_kpi_source_changed");
        });
        foreach (var damage in new[] { "manifest", "saved-day", "calculation", "employee", "year", "scope", "unavailable" })
            await Scenario("source proof " + damage, async () => {
                var f = new BenefitKpiFixture(); var expected = "kpi_dispute_source_evidence_conflict";
                switch (damage) {
                    case "manifest": f.Source = f.Source with { SourceManifest = null }; expected = "kpi_verified_calculation_required"; break;
                    case "saved-day": f.Source = f.Source with { SourceManifest = f.Source.SourceManifest! with { SavedDays = [] } }; expected = "kpi_calculation_manifest_conflict"; break;
                    case "calculation": f.Source = f.Source with { Calculation = f.Source.Calculation with { ExactScore = 90m } }; expected = "kpi_calculation_manifest_conflict"; break;
                    case "employee": f.Source = f.Source with { EmployeeId = Guid.NewGuid() }; break;
                    case "year": f.Source = f.BuildSource(f.From.AddYears(-1), 83.456789m); break;
                    case "scope": f.Source = f.Source with { Scopes = [] }; break;
                    case "unavailable": f.Source = f.Source with { Calculation = f.Source.Calculation with { State = "unavailable" } }; expected = "kpi_verified_calculation_required"; break;
                }
                await Reject("Invalid current " + damage, () => f.Read(), expected);
            });
        await Scenario("cancellation", async () => {
            var f = new BenefitKpiFixture(); using var cancellation = new CancellationTokenSource();
            f.Token = cancellation.Token; cancellation.Cancel();
            try { await f.Read(); Check(false, "Cancelled read returned evidence"); }
            catch (OperationCanceledException e) { Check(e.CancellationToken == f.Token, "Cancellation propagates without evidence"); }
        });
        Console.WriteLine($"Leave benefit KPI: {checks - failures}/{checks} checks passed.");
        return failures == 0 ? 0 : 1;
    }
}

internal sealed class BenefitKpiFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Account = Guid.NewGuid();
    public readonly DateOnly From, To;
    public readonly DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public CancellationToken Token = new CancellationTokenSource().Token;
    public FinalizedKpiScore? Final;
    public KpiSource Source;
    public readonly List<ApprovalRequest> PendingRows = [];
    public readonly List<KpiDisputeRow> Disputes = [];
    public readonly List<AuditEvent> Audits = [];
    public IReadOnlyList<ApprovalRequest>? YearRows;
    private readonly Dictionary<string, (byte[] Bytes, string Purpose)> files = [];
    private readonly LeaveBenefitKpiReader reader;
    public BenefitKpiFixture(DateOnly? from = null, decimal score = 83.456789m)
    {
        From = from ?? new DateOnly(2025, 4, 1); To = From.AddYears(1); Source = BuildSource(From, score);
        Final = new(1, Guid.NewGuid(), Employee, From, To, 1, null, Guid.NewGuid(), Guid.NewGuid(), Source.PolicyVersionId!.Value,
            score, Source.Calculation.DisplayScore!.Value, Source.Calculation.Fingerprint, Source.Fingerprint, Guid.NewGuid(), Now.AddDays(-10), Source.Scopes);
        var store = Boundary<IPrivateFileStore>((m, a) => m.Name == "ReadAsync"
            ? ReadFile((PrivateFileReference)a[0]!, (string)a[1]!) : throw new Exception("Unexpected file write"));
        var operation = new AttendanceOperation(Forbidden<ICurrentAuthentication>(), Forbidden<IAccountAccessReader>(),
            Forbidden<IAccessManagementRepository>(), Forbidden<ISchedulingJournalRepository>(), Forbidden<IAuthenticationRepository>(), TimeProvider.System, store);
        var finalized = Boundary<IFinalizedKpiReader>((m, a) => {
            Require(m.Name == "ReadAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == From, "Final read must target employee and exact year");
            return Task.FromResult(Final);
        });
        var requests = Boundary<IKpiRepository>((m, a) => {
            Require(m.Name == "PendingAsync" && (Guid)a[0]! == Employee, "Only employee pending reads are allowed");
            return Task.FromResult<IReadOnlyList<ApprovalRequest>>(PendingRows.ToArray());
        });
        var sources = Boundary<IKpiSourceReader>((m, a) => {
            Require(m.Name == "ReadAsync" && (Guid)a[0]! == Employee && (DateOnly)a[1]! == To.AddDays(-1), "Current KPI must be read on the year's last date");
            return Task.FromResult(Source);
        });
        var disputes = Boundary<IKpiDisputeRepository>((m, a) => {
            if (m.Name == "RequireTransaction") return null;
            Require((Guid)a[0]! == Employee, "Disputes must stay employee scoped");
            return m.Name switch {
                "YearAsync" when (DateOnly)a[1]! == From && a[2] is null && a[3] is null => Task.FromResult(YearRows ?? Disputes.Select(x => x.Request).ToArray()),
                "FindAsync" => Task.FromResult(Disputes.SingleOrDefault(x => x.Request.Id == (Guid)a[1]!)),
                "EvidenceAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.TargetId == ((Guid)a[1]!).ToString() && x.EventType == (string)a[2]!).ToArray()),
                _ => throw new Exception("Unexpected dispute operation " + m.Name)
            };
        });
        reader = new(operation, finalized, disputes, new(disputes, operation, store), requests, sources);
    }
    public Task<LeaveBenefitKpiEvidence> Read(DateOnly? from = null, DateOnly? to = null, DateTimeOffset? now = null) =>
        reader.ReadAsync(Employee, from ?? From, to ?? To, now ?? Now, Token);
    private T Boundary<T>(Func<MethodInfo, object?[], object?> call) where T : class => BenefitKpiProxy.Make<T>((m, a) => {
        if (a.LastOrDefault() is CancellationToken ct) { Require(ct == Token, "Cancellation token was not forwarded"); ct.ThrowIfCancellationRequested(); }
        return call(m, a);
    });
    private static T Forbidden<T>() where T : class => BenefitKpiProxy.Make<T>((m, _) => throw new Exception("Unexpected authority/write call " + m.Name));
    private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private string Store<T>(T value, string purpose)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value); var key = Guid.NewGuid().ToString("N");
        files.Add(key, (bytes, purpose));
        return AttendanceOperation.Reference(new PrivateFileReference(key, "synthetic-version", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    private Task<byte[]> ReadFile(PrivateFileReference reference, string purpose)
    {
        var entry = files[reference.StorageKey]; Require(entry.Purpose == purpose, "Private evidence purpose mismatch");
        return Task.FromResult(entry.Bytes.ToArray());
    }
    public void Tamper<T>(string reference, Func<T, T> change)
    {
        var key = JsonSerializer.Deserialize<PrivateFileReference>(reference)!.StorageKey; var entry = files[key];
        files[key] = (JsonSerializer.SerializeToUtf8Bytes(change(JsonSerializer.Deserialize<T>(entry.Bytes)!)), entry.Purpose);
    }
    public void Pending(DateOnly from, DateOnly to, string? malformed = null)
    {
        var snapshot = new KpiRequestSnapshot(1, malformed == "employee" ? Guid.NewGuid() : Employee, Account, to.AddDays(-1),
            malformed == "source" ? null! : Source with { From = from, ToExclusive = to }, Final!.Id);
        var reference = Store(snapshot, AttendanceKpiService.RequestPurpose(Employee));
        if (malformed == "json") {
            var key = JsonSerializer.Deserialize<PrivateFileReference>(reference)!.StorageKey;
            files[key] = (new byte[] { 123 }, files[key].Purpose);
        }
        PendingRows.Add(new(AttendanceKpiService.RequestType, Employee, Account.ToString(), reference, 1, Now));
    }
    public KpiSource BuildSource(DateOnly from, decimal score)
    {
        var to = from.AddYears(1);
        var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SYNTHETIC"], new("Synthetic approval", KpiPermissions.Decide, false, []),
            new KpiRules(score, from.Month, 2, AdvancedRounding.AwayFromZero, 0, 100, KpiLateBasis.PercentagePoints, 0.8m, 0.5m,
                KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var policy = Guid.NewGuid(); var assignment = Guid.NewGuid();
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber).Select(i => new KpiDayInput(from.AddDays(i), Guid.NewGuid(), new string('A', 64), false, null, null, null, null)).ToArray();
        var input = new KpiInput(rules, from, to, days, []); var calculation = AttendanceKpiCalculation.Calculate(input);
        Require(calculation.State == "ready_for_review", "Invalid synthetic KPI calculation");
        var manifest = new KpiSourceManifest(AttendanceKpiCalculation.Version, input, days.Select(x => new KpiSavedDay(x.Date, x.SummaryId, 1,
            "synthetic-v1", x.SourceFingerprint, Guid.NewGuid(), Guid.NewGuid(), policy, [assignment], new string('A', 64))).ToArray(), null);
        return new(Employee, from, to, policy, calculation, new string('B', 64), [new([new(EmployeeId: Employee)])], [assignment]) { SourceManifest = manifest };
    }
    public KpiDisputeRow Dispute(KpiDisputeOutcome? outcome, Guid? resubmission = null)
    {
        var at = Now.AddDays(-4); var device = Guid.NewGuid();
        var requester = new AuthenticatedAccount(Account, Employee, Guid.NewGuid(), 1, SessionClient.Mobile, false);
        var draft = new KpiDisputeDraft(1, Guid.NewGuid(), requester, device, "Employee", "synthetic-dispute", at, Final!, Source,
            KpiDisputeReason.Calculation, "Synthetic evidence statement", "Synthetic dispute", [], resubmission);
        var reference = Store(draft, KpiDisputeRules.DraftPurpose(Employee));
        var request = new ApprovalRequest(KpiDisputeRules.RequestType(Final!), Employee, Account.ToString(), reference, 1, at);
        IReadOnlyList<ApprovalDecision> decisions = [];
        if (outcome is not null) {
            var reviewedAt = Now.AddDays(-3); var authority = Authority(reviewedAt);
            var response = new KpiDisputeResponseSnapshot(1, request.Id, Employee, Final!.Id, KpiDisputeRules.Hash(draft), outcome.Value,
                "Synthetic review", reviewedAt, Final, Source, [], authority);
            var responseRef = Store(response, KpiDisputeRules.ResponsePurpose(Employee, request.Id));
            decisions = [request.Decide(KpiDisputeRules.State(outcome.Value), authority.Actor.AccountId.ToString(), authority.RoleCode, response.Reason, reviewedAt, responseRef)];
        }
        var row = new KpiDisputeRow(request, decisions); Disputes.Add(row);
        Audits.Add(new(KpiDisputeRules.SubmittedEvent, Account.ToString(), "Employee", KpiDisputeRules.TargetType, request.Id.ToString(), at,
            "synthetic-dispute", Employee, reference, deviceReference: device.ToString()));
        return row;
    }
    public void Amend(KpiDisputeRow row, FinalizedKpiScore amended)
    {
        var at = Now; var authority = Authority(at);
        var amendment = new KpiDisputeAmendmentSnapshot(1, row.Request.Id, Employee, Final!.Id, amended, amended.SourceFingerprint,
            "Synthetic amendment", at, authority);
        var reference = Store(amendment, KpiDisputeRules.AmendmentPurpose(Employee, row.Request.Id));
        Audits.Add(new(KpiDisputeRules.AmendmentEvent, authority.Actor.AccountId.ToString(), authority.RoleCode,
            KpiDisputeRules.TargetType, row.Request.Id.ToString(), at, "synthetic-amendment", Employee, reference,
            deviceReference: authority.Actor.SessionId.ToString()));
    }
    private KpiDisputeReviewAuthority Authority(DateTimeOffset at) => new(
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), "Admin", at,
        [new(KpiDisputePermissionDefs.Decide, true, Guid.NewGuid(), AccessScopeKind.All, null, at.AddDays(-1), null)], Source.Scopes);
}

public class BenefitKpiProxy : DispatchProxy
{
    private Func<MethodInfo, object?[], object?> call = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => call(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> call) where T : class
    {
        var proxy = Create<T, BenefitKpiProxy>(); ((BenefitKpiProxy)(object)proxy).call = call; return proxy;
    }
}
