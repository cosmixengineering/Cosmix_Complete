using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Xunit;

namespace Cosmix.Tests;

public class PayrollSupplementalAuthorityExpiryTests
{
    public static IEnumerable<object[]> ExpiryCases() =>
        from kind in new[] { "read", "write", "replay" }
        from consumer in new[] { "input", "attestation", "financial-source", "financial-adjustment", "overtime" }
        from stage in new[] { "grant-read", "audit-save" }
        select new object[] { kind, consumer, stage };

    [Theory, MemberData(nameof(ExpiryCases))]
    public async Task Monthly_source_permission_must_survive_read_save_and_exact_retry(string kind, string consumer, string stage)
    {
        var f = new Fixture(consumer, stage);
        if (kind == "replay") await f.PrimeReplay();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(kind == "replay" ? 1 : 0, f.BusinessRows);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Receipts.Count);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Audits.Count);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("replay")]
    public async Task Protected_input_read_cannot_restore_expired_original_scope(string kind)
    {
        var f = new Fixture("input", "private-read");
        if (kind == "replay") await f.PrimeReplay();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
    }

    public static IEnumerable<object[]> ValidCases() =>
        from kind in new[] { "read", "write", "replay" }
        from consumer in new[] { "input", "attestation", "financial-source", "financial-adjustment", "overtime" }
        select new object[] { kind, consumer };

    [Theory, MemberData(nameof(ValidCases))]
    public async Task Valid_scoped_sources_preserve_read_write_and_idempotency(string kind, string consumer)
    {
        var f = new Fixture(consumer);
        if (kind == "replay") await f.PrimeReplay();
        Assert.Equal(42, await f.Execute(kind));
        Assert.True(f.Committed);
        Assert.Equal(kind == "read" ? 0 : 1, f.BusinessRows);
        Assert.Equal(kind == "read" ? 0 : 1, f.Receipts.Count);
        Assert.Equal(kind == "replay" ? 2 : 1, f.Audits.Count);
    }

    [Fact]
    public async Task Scope_resolution_requirements_survive_the_audit_role_actor_copy()
    {
        var f = new Fixture("input", "audit-save");
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(f.RegisterDuringScopeResolution);
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Supplemental_scopes_are_copied_before_io_and_cannot_be_replaced(bool duringGrantRead)
    {
        var f = new Fixture("input", "audit-save");
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.MutableScopes(duringGrantRead));
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
    }

    [Fact]
    public async Task Supplemental_requirements_do_not_leak_to_a_later_request_on_the_same_service()
    {
        var f = new Fixture("input");
        await f.Execute("read");
        f.ExpireSourcePermission();
        Assert.Equal(42, await f.PrimaryOnly());
        Assert.True(f.Committed);
    }

    [Fact]
    public async Task Failed_or_malformed_supplemental_authority_is_not_registered()
    {
        var f = new Fixture("input");
        Assert.Equal(42, await f.RejectedRegistration());
        Assert.True(f.Committed);
    }

    [Fact]
    public void Request_local_authority_is_not_part_of_serialized_actor_contract()
    {
        var actor = new AttendanceActor(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true),
            "ConfiguredFinance", DateTimeOffset.UtcNow, null);
        actor.SupplementalAuthority.Register(AttendancePayrollInputPermissions.Read, [new([new(EmployeeId: Guid.NewGuid())])]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(actor));
        Assert.Equal(new[] { "Identity", "RoleCode", "Now", "DeviceId" }, json.RootElement.EnumerateObject().Select(x => x.Name));
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Month = new(2026, 10, 1);
        private readonly Clock clock = new(Start);
        private readonly string consumer;
        private readonly string? stage;
        private bool enabled = true;
        private bool inTransaction;
        private bool consuming;
        private readonly Guid employee = Guid.NewGuid();
        private readonly Guid otherEmployee = Guid.NewGuid();
        private readonly Guid resource = Guid.NewGuid();
        private readonly Guid currentSite = Guid.NewGuid();
        private readonly Guid retainedSite = Guid.NewGuid();
        private readonly AttendanceOperation operation;
        private readonly IAccountAccessReader access;
        private readonly IAttendancePayrollAttestationRepository attestations;
        private readonly IAttendancePayrollInputProducer producer;
        private readonly Dictionary<string, (string Purpose, byte[] Content)> files = [];
        private readonly AttendancePayrollInputRevision row;
        private readonly AttendancePayrollInputEvidence evidence;
        private readonly IReadOnlyList<AttendanceResourceScope> scopes;
        private readonly IMonthLockOvertimeReader overtime;
        private readonly MonthLockOvertimeDependency overtimeDependency;
        private Action? afterGrantRead;
        public bool Advanced { get; private set; }
        public bool Committed { get; private set; }
        public int BusinessRows { get; private set; }
        public List<IdempotencyReceipt> Receipts { get; } = [];
        public List<AuditEvent> Audits { get; } = [];
        private readonly AuthRequestContext context = new("127.0.0.1", "synthetic-payroll-authority-expiry");

        public Fixture(string consumer, string? stage = null)
        {
            this.consumer = consumer; this.stage = stage;
            var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var current = Proxy<ICurrentAuthentication>((_, _) => actor);
            scopes = [new([new(EmployeeId: employee)])];
            var expiring = consumer == "input" ? AttendancePayrollInputPermissions.Read : consumer == "overtime"
                ? OvertimePermissions.FinancialRead : AttendancePayrollAttestationPermissions.Read;
            var grants = new List<PermissionGrant>();
            foreach (var permission in new[] { MonthLockPermissions.Read, MonthLockPermissions.Finalize,
                AttendancePayrollInputPermissions.Read, AttendancePayrollAttestationPermissions.Read, OvertimePermissions.FinancialRead })
                grants.Add(new(permission, true, Guid.NewGuid(), permission.StartsWith("attendance.month-locks.")
                    ? AccessScopeKind.All : AccessScopeKind.Employee, permission.StartsWith("attendance.month-locks.") ? null : employee)
                {
                    AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1),
                    ValidUntilUtc = permission == expiring ? Start.AddMinutes(1) : Start.AddHours(1)
                });
            // Another employee's still-current authority must never replace the retained source scope.
            grants.Add(new(expiring, true, Guid.NewGuid(), AccessScopeKind.Employee, otherEmployee)
                { AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1), ValidUntilUtc = Start.AddHours(1) });
            grants.Add(new(OvertimePermissions.FinancialRead, true, Guid.NewGuid(), AccessScopeKind.Site, currentSite)
                { AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1), ValidUntilUtc = Start.AddHours(1) });
            access = Proxy<IAccountAccessReader>((_, args) =>
            {
                Assert.True(inTransaction);
                IReadOnlyList<PermissionGrant> result = grants.Where(g => g.IsEffectiveAt((DateTimeOffset)args[1]!)).ToArray();
                Advance("grant-read");
                if (consuming) { var hook = afterGrantRead; afterGrantRead = null; hook?.Invoke(); }
                return Task.FromResult(result);
            });
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args)!,
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ConfiguredFinance"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentSessionAsync": return Task.FromResult(true);
                    case "FindReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(x => x.Key == (string)args[2]!));
                    case "AddReceipt": Receipts.Add((IdempotencyReceipt)args[0]!); return null;
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": Advance("audit-save"); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var storage = Proxy<IPrivateFileStore>((method, args) =>
            {
                if (method.Name == "WriteAsync") return Task.FromResult(Store((byte[])args[0]!, (string)args[1]!));
                var reference = (PrivateFileReference)args[0]!;
                var stored = files[reference.StorageKey];
                Assert.Equal((string)args[1]!, stored.Purpose);
                if (stored.Purpose.StartsWith("attendance:payroll-inputs:")) Advance("private-read");
                return Task.FromResult(stored.Content.ToArray());
            });
            operation = new(current, access, authorization, journal, null!, clock, storage);
            attestations = Proxy<IAttendancePayrollAttestationRepository>((method, _) => method.Name == "HistoryAsync"
                ? Task.FromResult<IReadOnlyList<AttendancePayrollAttestation>>([]) : throw new InvalidOperationException(method.Name));
            producer = Proxy<IAttendancePayrollInputProducer>((method, _) => method.Name == "ResourceScopesAsync"
                ? Task.FromResult(scopes) : throw new InvalidOperationException(method.Name));
            var calculation = new AttendancePayrollInputCalculation(employee, Month, Month.AddMonths(1), Month, Month,
                "ready", new('A', 64), [], [], new(1, employee, Month, Month.AddMonths(1), Month, Month,
                    AttendancePayrollInputRules.Version, new('B', 64), scopes, []));
            AttendancePayrollJurisdictionContext[] jurisdictions = [];
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Calculation = calculation, Jurisdictions = jurisdictions })));
            evidence = new(1, employee, Month, 1, fingerprint, calculation, jurisdictions, scopes, actor.AccountId, "Synthetic retained source", Start);
            var reference = AttendanceOperation.Reference(Store(JsonSerializer.SerializeToUtf8Bytes(evidence),
                $"attendance:payroll-inputs:{employee:N}:{Month:yyyy-MM}:1"));
            row = new(employee, Month, 1, null, AttendancePayrollInputRules.Version, fingerprint, reference, actor.AccountId, Start);
            (overtime, overtimeDependency) = OvertimeFixture(storage);
        }

        public Task<int> Execute(string kind) => kind == "read"
            ? operation.ReadAsync(MonthLockPermissions.Read, "synthetic.month.read", null, context, async (actor, ct) =>
            { await Consume(actor, ct); return 42; }, default)
            : operation.WriteAsync(MonthLockPermissions.Finalize, "synthetic.month.finalize", null, "synthetic-exact-retry",
                new { value = 42 }, "Synthetic authority regression", context, (_, _) =>
                { BusinessRows++; return Task.FromResult(new AttendanceChange<int>(resource, null, 42)); }, default,
                beforeReplay: Consume);

        public Task<int> RegisterDuringScopeResolution() => operation.ReadAsync(MonthLockPermissions.Read,
            "synthetic.scopes", null, context, (_, _) => Task.FromResult(42), default, resourceScopes: async (actor, ct) =>
            { await Consume(actor, ct); return scopes; });

        public Task<int> MutableScopes(bool duringGrantRead) => operation.ReadAsync(MonthLockPermissions.Read,
            "synthetic.mutable-scopes", null, context, async (actor, ct) =>
            {
                var alternatives = new List<AccessResource> { new(EmployeeId: employee) };
                var mutable = new List<AttendanceResourceScope> { new(alternatives) };
                void Replace()
                {
                    alternatives[0] = new(EmployeeId: otherEmployee);
                    mutable.Clear(); mutable.Add(new([new(EmployeeId: otherEmployee)]));
                }
                consuming = true;
                if (duringGrantRead) afterGrantRead = Replace;
                await operation.RequireScopedPermissionAsync(actor, AttendancePayrollInputPermissions.Read, mutable, ct);
                if (!duringGrantRead) Replace();
                return 42;
            }, default);

        public void ExpireSourcePermission() => clock.Now = Start.AddMinutes(1);
        public Task<int> PrimaryOnly() => operation.ReadAsync(MonthLockPermissions.Read,
            "synthetic.primary-only", null, context, (_, _) => Task.FromResult(42), default);
        public Task<int> RejectedRegistration() => operation.ReadAsync(MonthLockPermissions.Read,
            "synthetic.rejected-registration", null, context, async (actor, ct) =>
            {
                var missing = await Assert.ThrowsAsync<AuthenticationFailure>(() => operation.RequireScopedPermissionAsync(actor,
                    AttendancePayrollAttestationPermissions.Attest, scopes, ct));
                var malformed = await Assert.ThrowsAsync<AuthenticationFailure>(() => operation.RequireScopedPermissionAsync(actor,
                    AttendancePayrollInputPermissions.Read, [], ct));
                Assert.Equal(403, missing.StatusCode); Assert.Equal(403, malformed.StatusCode); return 42;
            }, default);

        private async Task Consume(AttendanceActor actor, CancellationToken ct)
        {
            consuming = true;
            switch (consumer)
            {
                case "input":
                    await AttendancePayrollInputService.ReadValidatedEvidenceAsync(row, actor,
                        AttendancePayrollInputPermissions.Read, operation, access, ct);
                    break;
                case "attestation":
                    await AttendancePayrollAttestationService.ReadValidatedHistoryAsync(row, evidence, actor, operation, attestations, access, ct);
                    break;
                case "financial-source":
                    var reader = new AttendancePayrollFinancialSourceReader(null!, attestations, producer, null!, null!, null!, operation, access);
                    await InvokeRequire(reader, scopes, actor, ct);
                    break;
                case "financial-adjustment":
                    var service = new AttendancePayrollFinancialService(operation, null!, null!, null!, producer, null!, null!, null!);
                    var revision = AttendancePayrollFinancialRevision.From(row);
                    var source = new AttendancePayrollFinancialSources(revision, evidence.Calculation, null!, new('A', 64),
                        revision with { PeriodStart = Month.AddMonths(1) }, [], scopes);
                    await InvokeRequire(service, source, actor, false, ct);
                    break;
                case "overtime":
                    var awards = await overtime.ReadAsync(Month, Month.AddMonths(1), [overtimeDependency], actor, ct);
                    Assert.Single(awards);
                    break;
                default: throw new InvalidOperationException(consumer);
            }
        }

        private static Task InvokeRequire(object service, params object[] args) =>
            (Task)service.GetType().GetMethod("Require", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, args)!;

        private (IMonthLockOvertimeReader, MonthLockOvertimeDependency) OvertimeFixture(IPrivateFileStore storage)
        {
            var day = new DateOnly(2026, 10, 26);
            var shift = Guid.NewGuid(); var start = Start.AddHours(-12); var end = Start.AddHours(-1);
            var session = new WorkSession(Guid.NewGuid(), 1, employee, day, start, end, shift, null, Start,
                null, correctionId: Guid.NewGuid(), siteId: retainedSite);
            var roster = new EffectiveRosterResponse(employee, day, "scheduled", RosterSchedule.TimeZoneId,
                SiteId: retainedSite, ShiftVersionId: shift, StartsAt: start, EndsAt: start.AddHours(9));
            var retainedSession = new OvertimeSessionSource(session.Id, session.SessionKey, 1, start, end, retainedSite,
                shift, null, null, null, Guid.NewGuid(), true, session.CorrectionId);
            var duration = new OvertimeDurationResult("evaluated", null, OvertimeFormula.Version, true,
                [new(start.AddHours(9), end)], OvertimeFormula.Version);
            var source = new OvertimeSourceSnapshot(new(employee, day, "SITE", retainedSite, roster, [retainedSession],
                null, null, false, new("configured", Guid.NewGuid(), true), duration, ""), Guid.NewGuid(),
                new(Guid.NewGuid(), Guid.NewGuid(), "SITE", retainedSite, Month, null),
                [new([new(EmployeeId: employee, SiteId: retainedSite)])]);
            source = source with { Source = source.Source with { SourceFingerprint = OvertimeRules.Fingerprint(source) } };
            var proposal = new SubmitOvertimeRequest(day, session.Id, start.AddHours(9), end, OvertimeActivity.Work,
                source.Source.SourceFingerprint, "Synthetic approved overtime", "Controlled synthetic evidence");
            var maker = Guid.NewGuid(); var submission = Guid.NewGuid();
            var snapshot = new OvertimeSubmissionSnapshot(1, submission, employee, maker, proposal, source);
            var reference = AttendanceOperation.Reference(Store(JsonSerializer.SerializeToUtf8Bytes(snapshot),
                $"attendance:overtime:{employee:N}:proposal"));
            var approval = new ApprovalRequest(OvertimeService.RequestType, submission, maker.ToString(), reference, 1, Start);
            var request = new OvertimeRequest(employee, session.Id, proposal.StartsAtUtc, proposal.EndsAtUtc, 120,
                duration.PolicyVersionId, duration.RateRuleCode, proposal.Reason, reference, maker.ToString(), Start, approval.Id, retainedSite);
            var ticks = TimeSpan.FromHours(2).Ticks;
            var proof = new OvertimeDecisionSnapshot(1, request.Id, employee, source.Source.SourceFingerprint, ApprovalState.Approved,
                120, duration.PolicyVersionId, duration.CalculationVersion, source.Source.Eligibility.VersionId, source.CompensationRecordId,
                duration.RateRuleCode, "calculated_pending_payroll", ticks,
                new(OvertimeFormula.Version, source.CompensationRecordId!.Value, day, 31000m, 31, ticks,
                    OvertimeFormula.WholeRupees(31000m, day, ticks), "PKR"));
            var decisionReference = AttendanceOperation.Reference(Store(JsonSerializer.SerializeToUtf8Bytes(proof),
                $"attendance:overtime:{employee:N}:{request.Id:N}:decision"));
            var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "ConfiguredHR",
                "Synthetic independent approval", Start, decisionReference);
            var record = new OvertimeRecord { Request = request, Approval = approval, Decision = decision, BusinessDate = day };
            var repository = Proxy<IOvertimeRepository>((method, _) => method.Name == "FindAsync"
                ? Task.FromResult<OvertimeRecord?>(record) : throw new InvalidOperationException(method.Name));
            var summaries = Proxy<IAttendanceSummaryRepository>((method, _) => method.Name == "ResourceScopesAsync"
                ? Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId: employee, SiteId: currentSite)])])
                : throw new InvalidOperationException(method.Name));
            var recovery = Proxy<ILeaveRecoveryCreditReader>((method, _) => method.Name == "ReadReservedAsync"
                ? Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>([]) : throw new InvalidOperationException(method.Name));
            return (new OvertimeService(operation, repository, null!, summaries, null!, storage, access, null!, null!, recoveryCredits: recovery),
                new(request, session, approval, decision));
        }

        private PrivateFileReference Store(byte[] bytes, string purpose)
        {
            var key = Guid.NewGuid().ToString("N"); files.Add(key, (purpose, bytes.ToArray()));
            return new(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length);
        }

        public async Task PrimeReplay()
        {
            enabled = false; await Execute("write"); enabled = true; consuming = false; Committed = false;
        }

        private void Advance(string point)
        {
            if (enabled && consuming && !Advanced && stage == point)
            { Advanced = true; clock.Now = Start.AddMinutes(1); }
        }

        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Assert.False(inTransaction);
            var rows = BusinessRows; var audits = Audits.Count; var receipts = Receipts.Count;
            inTransaction = true;
            try { var result = await action(ct); Committed = true; return result; }
            catch
            {
                BusinessRows = rows; Audits.RemoveRange(audits, Audits.Count - audits);
                Receipts.RemoveRange(receipts, Receipts.Count - receipts); throw;
            }
            finally { inTransaction = false; }
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)proxy).Handler = handler; return proxy;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    }
}
