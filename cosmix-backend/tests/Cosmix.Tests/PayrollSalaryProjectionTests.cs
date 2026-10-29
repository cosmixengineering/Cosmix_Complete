using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Domain.HR.Policies;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSalaryProjectionTests
{
    [Fact]
    public async Task Employee_selection_freezes_complete_salary_and_cannot_duplicate_a_prepared_employee()
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(WageBasis.Monthly, [new(31000, 31000, 8)]);
        var service = await f.HandoffAsync();
        var preview = await service.PreviewAsync(2026, 10, Fixture.Context, default);
        var request = SelectedRequest(preview.PreviewFingerprint, [f.Employee]);
        var result = await service.PrepareAsync(request, Guid.NewGuid().ToString(), Fixture.Context, default);
        Assert.Equal(f.Employee, Assert.Single(result.Batch.EmployeeTotals).EmployeeId);
        Assert.Equal(900m, Assert.Single(result.Batch.EmployeeTotals).NetPay);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => service.PrepareAsync(
            request, Guid.NewGuid().ToString(), Fixture.Context, default));
        Assert.Equal("payroll_handoff_employee_already_prepared", error.Code);
    }

    [Fact]
    public async Task Selected_employee_must_exist_in_the_authoritative_month()
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(WageBasis.Monthly, [new(31000, 31000, 8)]);
        var service = await f.HandoffAsync();
        var preview = await service.PreviewAsync(2026, 10, Fixture.Context, default);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => service.PrepareAsync(
            SelectedRequest(preview.PreviewFingerprint, [Guid.NewGuid()]), Guid.NewGuid().ToString(), Fixture.Context, default));
        Assert.Equal("payroll_handoff_employee_not_in_month", error.Code);
        Assert.Null(f.SavedBatch);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"00000000-0000-0000-0000-000000000000\"]")]
    [InlineData("[\"11111111-1111-1111-1111-111111111111\",\"11111111-1111-1111-1111-111111111111\"]")]
    public async Task Invalid_employee_selection_is_rejected_before_preparation(string selection)
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(WageBasis.Monthly, [new(31000, 31000, 8)]);
        var service = await f.HandoffAsync();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => service.PrepareAsync(
            SelectedRequest(new('A', 64), JsonSerializer.Deserialize<Guid[]>(selection)!),
            Guid.NewGuid().ToString(), Fixture.Context, default));
        Assert.Equal("invalid_payroll_handoff_employee_selection", error.Code);
    }

    private static PreparePayrollHandoffRequest SelectedRequest(string fingerprint, Guid[] employees)
        => new(2026, 10, fingerprint, "Independent employee payroll group") { EmployeeIds = employees };

    [Fact]
    public async Task Protected_monthly_rate_and_component_revisions_reconcile_and_round_once()
    {
        var f = await Fixture.CreateAsync(PayrollGrossTreatment.ReconcileFixedEarnings);
        var initial = await f.ComponentAsync("HOUSING", 3100, Fixture.Month);
        var revised = await f.ComponentAsync("HOUSING", 6200, Fixture.Month.AddDays(15), initial.Id);
        await f.AttendanceAsync(WageBasis.Monthly, Enumerable.Range(0, 31).Select(i =>
            new DayTerms(i < 15 ? 31000 : 62000, i < 15 ? 34100 : 68200, i == 4 ? 0 : 8)).ToArray());
        var result = await f.ProjectAsync();
        Assert.Equal(47000m, Assert.Single(result.Lines, x => x.Line.Component == "BaseCompensation").Line.Amount);
        var allowance = Assert.Single(result.Lines, x => x.Line.Component == "FixedAllowance");
        Assert.Equal(4700m, allowance.Line.Amount);
        Assert.Equal(revised.Id, allowance.Line.SourceId);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(51700m, result.Lines.Sum(x => x.Line.Amount));
        // The 1000 unpaid-day deduction remains separate from monthly entitlement.
        Assert.Equal(1000m, f.Evidence.Calculation.Lines.Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(WageBasis.Daily, 1000, 2000)]
    [InlineData(WageBasis.Hourly, 125, 250)]
    public async Task Protected_nonmonthly_projection_uses_each_effective_rate_and_retained_paid_units(WageBasis basis, decimal firstRate, decimal secondRate)
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(basis, [new(firstRate, firstRate, 8), new(secondRate, secondRate, 4), new(secondRate, secondRate, 0)]);
        var result = await f.ProjectAsync();
        Assert.Equal(2000m, Assert.Single(result.Lines).Line.Amount);
        Assert.Equal(0m, f.Evidence.Calculation.Lines.Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(WageBasis.Daily)]
    [InlineData(WageBasis.Hourly)]
    public async Task All_unpaid_nonmonthly_days_keep_a_zero_base_line(WageBasis basis)
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(basis, [new(100, 100, 0), new(100, 100, 0)]);
        var result = await f.ProjectAsync();
        var line = Assert.Single(result.Lines).Line;
        Assert.Equal("BaseCompensation", line.Component);
        Assert.Equal(f.Revision.Id, line.SourceId);
        Assert.Equal(0m, line.Amount);
    }

    [Theory]
    [InlineData(WageBasis.Daily, 1000)]
    [InlineData(WageBasis.Hourly, 125)]
    public async Task Projection_pays_retained_paid_leave_and_excludes_unpaid_leave_once(WageBasis basis, decimal rate)
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(basis, [new(rate, rate, 0, PaidLeaveHours: 8), new(rate, rate, 0, UnpaidLeaveHours: 8)]);
        var result = await f.ProjectAsync();
        Assert.Equal(1000m, Assert.Single(result.Lines).Line.Amount);
        Assert.Equal(0m, f.Evidence.Calculation.Lines.Sum(x => x.Amount));
    }

    [Fact]
    public async Task Monthly_gross_cannot_claim_an_unapproved_fixed_allowance()
    {
        var f = await Fixture.CreateAsync(PayrollGrossTreatment.ReconcileFixedEarnings);
        await f.ComponentAsync("HOUSING", 3100, Fixture.Month, approve: false);
        await f.AttendanceAsync(WageBasis.Monthly, [new(31000, 34100, 8)]);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.ProjectAsync());
        Assert.Equal("salary_gross_components_do_not_reconcile", failure.Code);
    }

    [Fact]
    public async Task Component_database_amount_must_match_its_approved_protected_intake()
    {
        var f = await Fixture.CreateAsync();
        var input = await f.ComponentAsync("HOUSING", 3100, Fixture.Month);
        await f.AttendanceAsync(WageBasis.Monthly, [new(31000, 31000, 8)]);
        var row = f.Components.Single(x => x.Id == input.Id);
        typeof(PayrollSalaryComponent).GetProperty(nameof(PayrollSalaryComponent.Amount))!.SetValue(row, 6200m);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.ProjectAsync());
        Assert.Equal("salary_component_evidence_invalid", failure.Code);
    }

    [Theory]
    [InlineData(WageBasis.Monthly, 31000, 3000)]
    [InlineData(WageBasis.Daily, 1000, 1500)]
    [InlineData(WageBasis.Hourly, 125, 1500)]
    public async Task Real_preview_and_prepare_generate_base_pay_from_retained_attendance_and_freeze_matching_net(WageBasis basis, decimal rate, decimal expectedBase)
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(basis, [new(rate, rate, 8), new(rate, rate, 4), new(rate, rate, 0)]);
        var service = await f.HandoffAsync();
        var preview = await service.PreviewAsync(2026, 10, Fixture.Context, default);
        Assert.Equal("salary-v2", preview.CalculationVersion);
        Assert.Equal(expectedBase, Assert.Single(preview.Lines, x => x.Component == "BaseCompensation").Amount);
        Assert.Equal(1400m, Assert.Single(preview.EmployeeTotals).NetPay);
        var prepared = await service.PrepareAsync(new(2026, 10, preview.PreviewFingerprint, "Synthetic preparation"), Guid.NewGuid().ToString(), Fixture.Context, default);
        Assert.True(prepared.Batch.SalaryComplete);
        Assert.Equal("prepared", prepared.Batch.State);
        Assert.Equal(1400m, Assert.Single(prepared.Batch.EmployeeTotals).NetPay);
        Assert.Equal(expectedBase, Assert.Single(prepared.Lines, x => x.Component == "BaseCompensation").Amount);
        var frozen = await PayrollFrozenBatchReader.ValidateAsync(f.SavedBatch!, f.HandoffRepository!, f.Files, false, default);
        Assert.Equal(1400m, Assert.Single(frozen.Employees).NetPay);
        Assert.Equal(prepared.Batch.FrozenFingerprint, frozen.Fingerprint);
        Assert.All(frozen.Lines, line => Assert.Equal(f.Employee, line.EmployeeId));
    }

    [Fact]
    public async Task Preparing_a_stale_preview_does_not_create_a_salary_run()
    {
        var f = await Fixture.CreateAsync();
        await f.AttendanceAsync(WageBasis.Daily, [new(1000, 1000, 8)]);
        var service = await f.HandoffAsync();
        var preview = await service.PreviewAsync(2026, 10, Fixture.Context, default);
        await f.ComponentAsync("BONUS", 3100, Fixture.Month);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => service.PrepareAsync(
            new(2026, 10, preview.PreviewFingerprint, "Stale synthetic preview"), Guid.NewGuid().ToString(), Fixture.Context, default));
        Assert.Equal("payroll_handoff_preview_changed", failure.Code);
        Assert.Null(f.SavedBatch);
    }

    private sealed record DayTerms(decimal Rate, decimal Gross, int DutyHours, int PaidLeaveHours = 0, int UnpaidLeaveHours = 0);

    private sealed class Fixture
    {
        internal static readonly DateOnly Month = new(2026, 10, 1);
        internal static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-salary-projection");
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        private const string Version = "AAAAAAAAAAA=";
        internal readonly Guid Employee = Guid.NewGuid();
        private readonly Guid session = Guid.NewGuid(), actorEmployee = Guid.NewGuid(), attendancePolicy = Guid.NewGuid(), site = Guid.NewGuid(), assignment = Guid.NewGuid();
        private Guid actorId = Guid.NewGuid();
        private readonly List<PayrollSalaryPolicy> policies = [];
        internal readonly List<PayrollSalaryComponent> Components = [];
        private readonly List<ApprovalDecision> decisions = [];
        internal readonly MemoryFiles Files = new();
        private IPayrollSalaryRepository salaryRepository = null!;
        private PayrollSalaryService sources = null!;
        private AttendanceOperation operation = null!;
        private IAccountAccessReader access = null!;
        internal AttendancePayrollInputEvidence Evidence = null!;
        internal AttendancePayrollInputRevision Revision = null!;
        internal PayrollHandoffBatch? SavedBatch;
        internal IPayrollHandoffRepository? HandoffRepository;
        private AuthenticatedAccount Identity => new(actorId, actorEmployee, session, 1, SessionClient.Web, true);

        internal static async Task<Fixture> CreateAsync(PayrollGrossTreatment gross = PayrollGrossTreatment.BaseOnly)
        {
            var f = new Fixture();
            var permissions = PayrollSalaryPermissions.Definitions.Concat(PayrollHandoffPermissions.Definitions).Concat(AttendancePayrollInputPermissions.Definitions);
            var grants = permissions.Select(p => new PermissionGrant(p.Code, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = Now.AddDays(-2) }).ToArray();
            f.access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(grants));
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticSalaryAuthority"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, _) => method.Name switch
            {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult<IdempotencyReceipt?>(null),
                "AddReceipt" or "AddAudit" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            f.operation = new(Proxy<ICurrentAuthentication>((_, _) => f.Identity), f.access, authorization, journal, null!, new Clock(), f.Files);
            f.salaryRepository = Proxy<IPayrollSalaryRepository>((method, args) => method.Name switch
            {
                "PoliciesAsync" => Task.FromResult<IReadOnlyList<PayrollSalaryPolicy>>(f.policies.ToArray()),
                "PolicyAsync" => Task.FromResult(f.policies.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "ComponentsAsync" => Task.FromResult<IReadOnlyList<PayrollSalaryComponent>>(f.Components.Where(x => args[0] is null || x.EmployeeId == (Guid)args[0]!).ToArray()),
                "ComponentAsync" => Task.FromResult(f.Components.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "DecisionsAsync" => Task.FromResult<IReadOnlyList<ApprovalDecision>>(f.decisions.Where(x => x.ApprovalRequestId == (Guid)args[0]!).ToArray()),
                "EmployeeApprovedAsync" => Task.FromResult((Guid)args[0]! == f.Employee),
                "HasPreparedPeriodAsync" or "HasPreparedEmployeePeriodAsync" => Task.FromResult(f.SavedBatch is not null),
                "Add" => f.AddSalary(args),
                "ExpectVersion" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            f.sources = new(f.operation, f.salaryRepository, f.Files);
            var policy = await f.sources.RequestPolicyAsync(new(Month, new(1, PayrollProration.CalendarDays, null,
                PayrollMoneyRounding.HalfUp, gross), null, "Synthetic explicit salary policy"), Guid.NewGuid().ToString(), Context, default);
            f.actorId = Guid.NewGuid();
            await f.sources.ReviewPolicyAsync(policy.Id, new(Version, ApprovalState.Approved, "Synthetic independent policy approval"), Guid.NewGuid().ToString(), Context, default);
            return f;
        }

        internal async Task<PayrollSalaryComponentResponse> ComponentAsync(string code, decimal amount, DateOnly from, Guid? previous = null, bool approve = true)
        {
            actorId = Guid.NewGuid();
            var result = await sources.RequestComponentAsync(new(Employee, code, PayrollSalaryComponentKind.FixedEarning,
                amount, "PKR", from, null, true, previous, "Synthetic fixed earnings"), Guid.NewGuid().ToString(), Context, default);
            if (!approve) return result;
            actorId = Guid.NewGuid();
            return await sources.ReviewComponentAsync(result.Id, new(Version, ApprovalState.Approved, "Synthetic independent component approval"), Guid.NewGuid().ToString(), Context, default);
        }

        internal async Task AttendanceAsync(WageBasis basis, IReadOnlyList<DayTerms> terms)
        {
            var rules = AttendanceRules();
            var days = new List<AttendancePayrollDaySource>(); var inputs = new List<AttendancePayrollInputDay>();
            Guid rateId = Guid.NewGuid(); DateOnly rateFrom = Month;
            for (var i = 0; i < terms.Count; i++)
            {
                var date = Month.AddDays(i); var term = terms[i];
                if (i > 0 && (term.Rate != terms[i - 1].Rate || term.Gross != terms[i - 1].Gross)) { rateId = Guid.NewGuid(); rateFrom = date; }
                var next = Enumerable.Range(i + 1, terms.Count - i - 1).FirstOrDefault(j => terms[j].Rate != term.Rate || terms[j].Gross != term.Gross, -1);
                var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(3, 0)), TimeSpan.Zero);
                var leave = new List<AttendanceLeaveTime>();
                if (term.PaidLeaveHours > 0) leave.Add(new(new(start.AddHours(term.DutyHours), start.AddHours(term.DutyHours + term.PaidLeaveHours)), true));
                if (term.UnpaidLeaveHours > 0) leave.Add(new(new(start.AddHours(term.DutyHours + term.PaidLeaveHours),
                    start.AddHours(term.DutyHours + term.PaidLeaveHours + term.UnpaidLeaveHours)), false));
                var timeInput = new AttendanceTimeInput(rules, start, start.AddHours(8), 480, 0, false, false,
                    term.DutyHours == 0 ? [] : [new(start, start.AddHours(term.DutyHours))], leave);
                var time = AttendanceDailyTimeCalculation.Calculate(timeInput);
                Assert.Equal("calculated", time.Status);
                var policy = new AttendancePolicyResolution("effective", attendancePolicy, [assignment], rules,
                    AttendancePolicyRulesCodec.Canonicalize(rules), new('A', 64));
                var roster = new EffectiveRosterResponse(Employee, date, "scheduled", "Asia/Karachi", SiteId: site,
                    AssignmentId: assignment, ShiftVersionId: Guid.NewGuid(), ConfiguredScheduledMinutes: 480);
                var snapshot = new AttendanceCalculationSnapshot(1, Employee, date, AttendanceDailyTimeCalculation.Version,
                    new('A', 64), roster, new(assignment, Guid.NewGuid(), "SITE", site, Month, Month.AddDays(terms.Count)),
                    policy, new(LeaveContextStatus.Complete, [], new('B', 64), []), [], [], [], [], timeInput, 0);
                var compensation = new AttendancePayrollCompensation(rateId, term.Rate, term.Gross, "PKR", basis,
                    rateFrom, next < 0 ? null : Month.AddDays(next), new('C', 64), [rateId]);
                days.Add(new(date, "available", new('D', 64), snapshot, time, compensation, policy));
                inputs.Add(new(date, "available", attendancePolicy, rules, timeInput, time, compensation, new('D', 64), [rateId]));
            }
            var deductions = AttendancePayrollInputRules.Calculate(Month, inputs);
            Assert.Equal("ready", deductions.State);
            IReadOnlyList<AttendanceResourceScope> scopes = [new([new(EmployeeId: Employee, SiteId: site)])];
            var version = AttendancePayrollInputRules.VersionFor(days.Select(x => x.Compensation));
            var manifest = new AttendancePayrollInputManifest(1, Employee, Month, Month.AddMonths(1), Month, Month.AddDays(terms.Count - 1),
                version, new('E', 64), scopes, days);
            var calculation = new AttendancePayrollInputCalculation(Employee, Month, Month.AddMonths(1), Month, manifest.SourceTo,
                "ready", new('F', 64), deductions.Lines, deductions.Issues, manifest);
            IReadOnlyList<AttendancePayrollJurisdictionContext> jurisdictions = days.Select(x =>
                new AttendancePayrollJurisdictionContext(x.BusinessDate, site, Guid.NewGuid(), "PK-SD", "configured")).ToArray();
            var fingerprint = MonthLockEvidence.Fingerprint(new { Calculation = calculation, Jurisdictions = jurisdictions });
            Evidence = new(1, Employee, Month, 1, fingerprint, calculation, jurisdictions, scopes, actorId, "Synthetic retained attendance", Now.AddDays(-1));
            var reference = await Files.PutAsync(Evidence, $"attendance:payroll-inputs:{Employee:N}:{Month:yyyy-MM}:1");
            Revision = new(Employee, Month, 1, null, version, fingerprint, reference, actorId, Evidence.CreatedAtUtc);
        }

        internal Task<PayrollSalaryProjectionResult> ProjectAsync() =>
            operation.ReadAsync(PayrollSalaryPermissions.Read, "synthetic.salary.project", null, Context,
                (actor, ct) => new PayrollSalaryProjection(salaryRepository, sources, operation).ProjectAsync(Revision, Evidence, actor, ct), default);

        internal async Task<PayrollHandoffService> HandoffAsync()
        {
            var summaries = Evidence.Calculation.SourceManifest.Days.Select(day => new DailyAttendanceSummary(Employee, day.BusinessDate,
                1, day.Time!.DayStatus!.Value, attendancePolicy, day.Snapshot!.Roster.ShiftVersionId, AttendanceDailyTimeCalculation.Version,
                day.Fingerprint, 480, (int)(day.Time.PayableTicks!.Value / TimeSpan.TicksPerMinute),
                (int)(day.Time.PayableTicks.Value / TimeSpan.TicksPerMinute), 0, 0, 0, Now.AddDays(-1))).ToArray();
            var period = new AttendancePeriodLock(Month, Month.AddMonths(1)); var eventId = Guid.NewGuid();
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(Revision.ProtectedEvidenceReference)!;
            var snapshot = new MonthLockSnapshot(3, eventId, period.Id, 1, Month, Month.AddMonths(1), actorId.ToString(), "Synthetic month lock",
                Now.AddDays(-1), new('A', 64), summaries.Select(x => new MonthLockSummaryEntry(Employee, x.AttendanceDate, x.Id,
                    x.InputFingerprint, attendancePolicy, x.CalculationVersion)).ToArray(), [],
                [new(Employee, Month, Revision.Id, Revision.Revision, Revision.InputFingerprint, Revision.CalculationVersion,
                    reference.Sha256, Guid.NewGuid(), new('B', 64), new('C', 64))], []);
            var snapshotReference = await Files.PutAsync(snapshot, MonthLockEvidence.Purpose(period.Id, eventId));
            var locked = period.RecordDirectLock(eventId, snapshot.Actor, snapshot.Reason, snapshot.LockedAtUtc, snapshotReference);
            var existing = new MonthLockExisting(period, locked, summaries, true);
            var months = Proxy<IMonthLockRepository>((method, args) => method.Name == "FindAsync" && (DateOnly)args[0]! == Month
                ? Task.FromResult<MonthLockExisting?>(existing) : throw new InvalidOperationException(method.Name));
            var monthReader = Proxy<IMonthLockSnapshotReader>((method, _) => method.Name == "ReadRetainedAsync"
                ? ReadMonthAsync(snapshotReference, period.Id, eventId) : throw new InvalidOperationException(method.Name));
            var ledger = Proxy<IAttendancePayrollInputLedger>((method, args) => method.Name == "FindAsync" &&
                (Guid)args[0]! == Employee && (Guid)args[1]! == Revision.Id ? Task.FromResult<AttendancePayrollInputRevision?>(Revision)
                : throw new InvalidOperationException(method.Name));
            HandoffRepository = Proxy<IPayrollHandoffRepository>((method, _) => method.Name switch
            {
                "PeriodPreparedAsync" or "EmployeesPreparedAsync" => Task.FromResult(SavedBatch is not null),
                "AllowancesAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffAllowanceSource>>([]),
                "LoanSchedulesAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffLoanScheduleSource>>([]),
                "FuelClaimsAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffFuelClaimSource>>([]),
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            }, (method, args) => { if (method.Name != "Add" || args[0] is not PayrollHandoffBatch batch) return false; SavedBatch = batch; return true; });
            var statutorySource = Guid.NewGuid();
            var statutoryReference = await Files.PutAsync(new { Employee, Amount = 100m, Period = Month }, "synthetic:statutory-source");
            var statutory = Proxy<IPayrollStatutorySourceReader>((method, args) =>
            {
                if (method.Name != "ProjectAsync") throw new InvalidOperationException(method.Name);
                var basis = Assert.Single((IReadOnlyList<PayrollSalaryEmployeeBasis>)args[2]!);
                Assert.Equal(Employee, basis.EmployeeId);
                return Task.FromResult(new PayrollStatutoryProjection(new('A', 64),
                    [new(new(Employee, "StatutoryEmployeeDeduction", "StatutoryContributionEntry", statutorySource, 1, -100m, "PKR", null, null), statutoryReference)]));
            });
            return new(operation, HandoffRepository, months, ledger, null!, null!, access, Files, monthReader,
                salaryRepository: salaryRepository, salarySources: sources, statutory: statutory);
        }

        private async Task<MonthLockSnapshot> ReadMonthAsync(string reference, Guid periodId, Guid eventId) =>
            JsonSerializer.Deserialize<MonthLockSnapshot>(await Files.ReadAsync(JsonSerializer.Deserialize<PrivateFileReference>(reference)!,
                MonthLockEvidence.Purpose(periodId, eventId), default))!;
        private object? AddSalary(object?[] args)
        {
            if (args[0] is ApprovalDecision decision) { decisions.Add(decision); return null; }
            var approval = (ApprovalRequest)args[1]!;
            typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.RowVersion))!.SetValue(approval, new byte[8]);
            if (args[0] is PayrollSalaryPolicy policy)
            { typeof(PayrollSalaryPolicy).GetProperty(nameof(PayrollSalaryPolicy.ApprovalRequest))!.SetValue(policy, approval); policies.Add(policy); }
            else if (args[0] is PayrollSalaryComponent component)
            { typeof(PayrollSalaryComponent).GetProperty(nameof(PayrollSalaryComponent.ApprovalRequest))!.SetValue(component, approval); Components.Add(component); }
            else throw new InvalidOperationException("Unexpected salary write");
            return null;
        }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
        private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    }

    private static AttendancePolicyRules AttendanceRules() => new(3, new(900, 3600, 9000),
        new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
            AttendanceEarlyExitTreatment.HalfDayCredit, 14400, AttendanceNonScheduledDayTreatment.NoCredit,
            AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
        new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
            AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange, AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
            true, AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
        new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, .5m,
            AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));

    private sealed class MemoryFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> contents = [];
        internal async Task<string> PutAsync<T>(T value, string purpose) => JsonSerializer.Serialize(await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), purpose, default));
        public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N"); contents.Add(key, (purpose, content.ToArray()));
            return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic", content.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var stored = contents[reference.StorageKey]; Assert.Equal(stored.Purpose, purpose);
            Assert.Equal(reference.Sha256, Convert.ToHexString(SHA256.HashData(stored.Bytes)));
            Assert.Equal(reference.SizeBytes, stored.Bytes.Length);
            return Task.FromResult(stored.Bytes.ToArray());
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback, Func<MethodInfo, object?[], bool>? mutation = null) where T : class
    {
        var value = DispatchProxy.Create<T, CallbackProxy>();
        ((CallbackProxy)(object)value).Callback = (method, args) => mutation?.Invoke(method, args) == true ? null : callback(method, args);
        return value;
    }
    public class CallbackProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Callback = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Callback(method!, args ?? []);
    }
}
