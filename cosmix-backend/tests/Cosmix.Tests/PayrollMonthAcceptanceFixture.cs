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
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollImports;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

/// <summary>One explicitly selected synthetic SQL scenario. Only authentication/session and upstream
/// hiring/attendance capture are fixtures. Salary, import, approval, finance and payslip services use
/// production repositories, SQL transactions, audit receipts and purpose-bound encrypted files.</summary>
internal sealed class PayrollMonthAcceptanceFixture : IAsyncDisposable
{
    internal const string EvidenceRoot = "D:/CosmixPayrollAcceptance20261028";
    internal static readonly DateOnly Month = new(2026, 9, 1);
    internal static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-month-acceptance-20261028");
    internal readonly CosmixDbContext Db;
    internal readonly AcceptanceClock Clock = new();
    internal Guid ActorId = Guid.NewGuid();
    internal Guid ActorEmployeeId = Guid.NewGuid();
    internal Guid EmployeeId => Employee.Id;
    internal Employee Employee = null!;
    internal UserAccount PayeeAccount = null!;
    internal readonly IPrivateFileStore Files;
    internal readonly ICurrentAuthentication Current;
    internal readonly IAccountAccessReader Access;
    internal readonly IAccessManagementRepository Authorization;
    internal readonly ISchedulingJournalRepository Journal;
    internal readonly AttendanceOperation Operation;
    internal readonly PayrollSalaryRepository SalaryRepository;
    internal readonly PayrollSalaryService Salary;
    internal readonly EmployeeCompensationService Compensation;
    internal readonly PayrollImportService Imports;
    private readonly HashSet<string> permissions = [];
    private readonly Guid session = Guid.NewGuid(), assignment = Guid.NewGuid();

    internal PayrollMonthAcceptanceFixture()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_ACCEPTANCE_CONNECTION")
            ?? throw new InvalidOperationException("Explicit synthetic acceptance target is required.");
        var target = new SqlConnectionStringBuilder(connection);
        if (target.DataSource != "(localdb)\\CosmixUat_d893653346" || target.InitialCatalog != "Cosmix_Payroll_Acceptance_20261028" || !target.IntegratedSecurity)
            throw new InvalidOperationException("Refusing any target except the dedicated synthetic payroll database.");
        Db = new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
        var privateRoot = Path.Combine(EvidenceRoot, "private");
        Directory.CreateDirectory(Path.Combine(privateRoot, "objects"));
        var key = Path.Combine(privateRoot, "storage.key");
        if (!File.Exists(key)) File.WriteAllBytes(key, RandomNumberGenerator.GetBytes(32));
        Files = new EncryptedPrivateFileStore(new() { Directory = Path.Combine(privateRoot, "objects"), KeyFile = key, KeyReference = "synthetic-month-v1" });
        Current = Proxy<ICurrentAuthentication>((_, _) => new AuthenticatedAccount(ActorId, ActorEmployeeId, session, 1, SessionClient.Web, true));
        AddPermissions(PayrollSalaryPermissions.Definitions.Concat(PayrollHandoffPermissions.Definitions)
            .Concat(PayrollImportPermissions.Definitions).Concat(AttendancePayrollInputPermissions.Definitions).Select(x => x.Code).ToArray());
        AddPermissions([EmployeeCompensationPermissions.Read, EmployeeCompensationPermissions.Request, EmployeeCompensationPermissions.Review,
            EmployeeSelfServicePermissions.FinancesRead]);
        Access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(permissions.Select(p =>
            new PermissionGrant(p, true, assignment, AccessScopeKind.All, null) { AuthorityTimingKnown = true,
                ValidFromUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }).ToArray()));
        Authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => typeof(PayrollMonthAcceptanceFixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            "IsCurrentAdministratorAsync" or "IsCurrentSystemRoleAssignmentAsync" or "IsEmployeeActiveForAccessAsync" => Task.FromResult(true),
            _ => throw new InvalidOperationException("Unimplemented synthetic authority boundary: " + method.Name)
        });
        var persistedJournal = new SchedulingJournalRepository(Db, null!, null!);
        Journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name == "IsCurrentSessionAsync"
            ? Task.FromResult(true) : method.Invoke(persistedJournal, args));
        Operation = new(Current, Access, Authorization, Journal, null!, Clock, Files);
        var hiring = new EmployeeHiringRepository(Db, Clock);
        var compensationRepo = new EmployeeCompensationRepository(Db);
        Compensation = new(new EmployeeOperation(Current, Access, Authorization, hiring, Clock, Files), hiring,
            new EmployeeLifecycleRepository(Db), compensationRepo, Files, new MobileAuthenticationOptions { BusinessTimeZoneId = "Asia/Karachi" });
        SalaryRepository = new(Db); Salary = new(Operation, SalaryRepository, Files);
        Imports = new(Operation, new PayrollImportRepository(Db), Files, Compensation, compensationRepo, Salary, SalaryRepository, Access, Authorization, Clock);
    }

    internal void AddPermissions(string[] values) { foreach (var value in values) permissions.Add(value); }
    internal async Task SeedEmployeeAsync()
    {
        Assert.Empty(await Db.Employees.AsNoTracking().ToArrayAsync());
        Assert.Empty(await Db.Database.GetPendingMigrationsAsync());
        await using var seedTransaction = await Db.Database.BeginTransactionAsync();
        Employee = new(Clock.Now.AddDays(-1), ActorId.ToString());
        Employee.UpdateDraft(new() { FullLegalName = "SYNTHETIC Payroll Acceptance", IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityDocumentNumber = "SYNTHETIC-PAYROLL-202609", PersonalPhone = "+923001234567", JoinedOn = Month });
        Employee.SelectLoginPhone("+923001234567", true);
        var proof = await Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { Fixture = "Synthetic upstream hiring only", Name = Employee.FullLegalName }), "synthetic:upstream:hiring", default);
        var role = Role.CreateSystem(SystemRoleKind.Admin, ActorId, Clock.Now.AddDays(-1));
        Db.AddRange(Employee, role); await Db.SaveChangesAsync();
        var submission = Employee.SubmitForApproval(ActorId, Clock.Now, proof.StorageKey, proof.ObjectVersion, proof.Sha256, proof.KeyReference, 1);
        Db.Add(submission); await Db.SaveChangesAsync();
        var decision = Employee.DecideSubmission(submission, OnboardingDecision.Approve, Guid.NewGuid(), role.Id, "Synthetic upstream fixture", Clock.Now);
        Db.Add(decision);
        await Db.SaveChangesAsync();
        // The retained upstream attendance ledger requires an actual account FK. Leave this
        // synthetic employee's account dormant; the separate mobile harness owns access and OTP.
        PayeeAccount = new(Employee, decision, "synthetic.payroll@example.invalid", Clock.Now);
        Db.Add(PayeeAccount); await Db.SaveChangesAsync();
        Assert.Equal(1, Employee.EmployeeNumber);
        await seedTransaction.CommitAsync();
        await SaveEvidenceAsync("synthetic-employee.json", new { Employee.Id, Employee.EmployeeNumber, Employee.FullLegalName,
            Employee.ApprovedOnboardingDecisionId, Employee.ApprovedLoginPhoneNumber, Employee.MobileAccessRequested,
            PayeeAccountId = PayeeAccount.Id, Month,
            PrivateFiles = new { Directory = Path.Combine(EvidenceRoot, "private", "objects"),
                KeyFile = Path.Combine(EvidenceRoot, "private", "storage.key"), KeyReference = "synthetic-month-v1" },
            Boundary = "Domain hiring fixture, no real hiring/OTP/provider approval" });
    }

    internal async Task ImportAndApproveAsync()
    {
        var path = Path.Combine(EvidenceRoot, "source-import.xlsx");
        var upload = await Imports.InspectAsync(new(Path.GetFileName(path), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Convert.ToBase64String(await File.ReadAllBytesAsync(path)), "SYNTHETIC September source"), Key(), Context, default);
        var sheet = Assert.Single(upload.Sheets);
        var mapping = sheet.Columns.Where(c => PayrollImportService.Targets.Any(t => t.Key == c)).ToDictionary(c => c, c => c);
        var preview = await Imports.PreviewAsync(new(upload.SourceId, sheet.Name, mapping), Context, default);
        await SaveEvidenceAsync("import-preview.json", preview);
        Assert.True(preview.CanApply, JsonSerializer.Serialize(preview)); Assert.Equal(3, preview.Summary.ValidRowCount);
        var applied = await Imports.ApplyAsync(new(upload.SourceId, sheet.Name, mapping, preview.PreviewFingerprint, "SYNTHETIC canonical requests"), Key(), Context, default);
        Assert.Equal(3, applied.Requests.Count);
        ActorId = Guid.NewGuid();
        foreach (var link in applied.Requests)
        {
            if (link.RequestType == EmployeeCompensationService.RequestType)
            {
                var request = await Db.ApprovalRequests.SingleAsync(x => x.Id == link.RequestId);
                await Compensation.DecideAsync(EmployeeId, link.RequestId, new(Convert.ToBase64String(request.RowVersion),
                    Convert.ToBase64String(Employee.RowVersion), ApprovalState.Approved, "SYNTHETIC independent base approval"), Key(), Context, default);
            }
            else
            {
                var row = await Db.Set<PayrollSalaryComponent>().Include(x => x.ApprovalRequest).SingleAsync(x => x.ApprovalRequestId == link.RequestId);
                await Salary.ReviewComponentAsync(row.Id, new(Convert.ToBase64String(row.ApprovalRequest.RowVersion), ApprovalState.Approved,
                    "SYNTHETIC independent component approval"), Key(), Context, default);
            }
        }
        var policy = await Salary.RequestPolicyAsync(new(Month, new(1, PayrollProration.CalendarDays, null, PayrollMoneyRounding.HalfUp,
            PayrollGrossTreatment.BaseOnly), null, "SYNTHETIC calendar-day policy"), Key(), Context, default);
        ActorId = Guid.NewGuid();
        await Salary.ReviewPolicyAsync(policy.Id, new(policy.Version, ApprovalState.Approved, "SYNTHETIC independent policy approval"), Key(), Context, default);
        await SaveEvidenceAsync("import-submission.json", applied);
    }

    internal static string Key() => Guid.NewGuid().ToString();
    internal async Task<PayrollHandoffService> RetainSyntheticAttendanceAsync()
    {
        Clock.Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);
        var rules = new AttendancePolicyRules(3, new(900, 3600, 9000),
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
        var policy = new Policy("SYNTHETIC-ATTENDANCE", "Synthetic upstream attendance fixture", PolicyCategory.Attendance);
        var version = new PolicyVersion(policy.Id, 1, Month, null, "3", AttendancePolicyRulesCodec.Canonicalize(rules), null, null,
            ActorId.ToString(), Clock.Now.AddMonths(-1), PolicyAuthority.DirectHr);
        Db.AddRange(policy, version); await Db.SaveChangesAsync();
        var compensationRow = await Db.CompensationRecords.AsNoTracking().SingleAsync(x => x.EmployeeId == EmployeeId);
        var policyFingerprint = MonthLockEvidence.Fingerprint(new { version.Id, version.RulesJson });
        var compensationFingerprint = MonthLockEvidence.Fingerprint(new { compensationRow.Id, compensationRow.BaseAmount,
            compensationRow.GrossAmount, compensationRow.EffectiveFrom, compensationRow.ApprovalRequestId });
        var days = new List<AttendancePayrollDaySource>(); var inputs = new List<AttendancePayrollInputDay>();
        var summaries = new List<DailyAttendanceSummary>();
        var site = Guid.NewGuid(); var rosterAssignment = Guid.NewGuid();
        for (var i = 0; i < 30; i++)
        {
            var date = Month.AddDays(i); var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(3, 0)), TimeSpan.Zero);
            var timeInput = new AttendanceTimeInput(rules, start, start.AddHours(8), 480, 0, false, false, [new(start, start.AddHours(8))], []);
            var time = AttendanceDailyTimeCalculation.Calculate(timeInput); Assert.Equal("calculated", time.Status);
            var resolved = new AttendancePolicyResolution("effective", version.Id, [rosterAssignment], rules, AttendancePolicyRulesCodec.Canonicalize(rules), policyFingerprint);
            var roster = new EffectiveRosterResponse(EmployeeId, date, "scheduled", "Asia/Karachi", SiteId: site,
                AssignmentId: rosterAssignment, ConfiguredScheduledMinutes: 480);
            var snapshot = new AttendanceCalculationSnapshot(1, EmployeeId, date, AttendanceDailyTimeCalculation.Version,
                MonthLockEvidence.Fingerprint(new { EmployeeId, date, timeInput }), roster, new(rosterAssignment, Guid.NewGuid(), "SITE", site, Month, Month.AddMonths(1)),
                resolved, new(LeaveContextStatus.Complete, [], MonthLockEvidence.Fingerprint(Array.Empty<Guid>()), []), [], [], [], [], timeInput, 0);
            var comp = new AttendancePayrollCompensation(compensationRow.Id, compensationRow.BaseAmount, compensationRow.GrossAmount,
                compensationRow.Currency, compensationRow.WageBasis, compensationRow.EffectiveFrom, compensationRow.EffectiveTo,
                compensationFingerprint, [compensationRow.Id]);
            var fingerprint = MonthLockEvidence.Fingerprint(new { date, snapshot, time, comp });
            days.Add(new(date, "available", fingerprint, snapshot, time, comp, resolved));
            inputs.Add(new(date, "available", version.Id, rules, timeInput, time, comp, fingerprint, [compensationRow.Id]));
            summaries.Add(new(EmployeeId, date, 1, time.DayStatus!.Value, version.Id, null, AttendanceDailyTimeCalculation.Version,
                fingerprint, 480, 480, 480, 0, 0, 0, Clock.Now));
        }
        var deductions = AttendancePayrollInputRules.Calculate(Month, inputs);
        Assert.Equal("ready", deductions.State); Assert.Equal(0m, deductions.Lines.Sum(x => x.Amount));
        IReadOnlyList<AttendanceResourceScope> scopes = [new([new(EmployeeId: EmployeeId, SiteId: site)])];
        var calculationVersion = AttendancePayrollInputRules.VersionFor(days.Select(x => x.Compensation));
        var manifest = new AttendancePayrollInputManifest(1, EmployeeId, Month, Month.AddMonths(1), Month, Month.AddDays(29),
            calculationVersion, MonthLockEvidence.Fingerprint(days), scopes, days);
        var calculation = new AttendancePayrollInputCalculation(EmployeeId, Month, Month.AddMonths(1), Month, manifest.SourceTo,
            "ready", MonthLockEvidence.Fingerprint(new { manifest, deductions }), deductions.Lines, deductions.Issues, manifest);
        IReadOnlyList<AttendancePayrollJurisdictionContext> jurisdictions = days.Select(x =>
            new AttendancePayrollJurisdictionContext(x.BusinessDate, site, Guid.NewGuid(), "PK-SD", "configured")).ToArray();
        var fingerprintAll = MonthLockEvidence.Fingerprint(new { Calculation = calculation, Jurisdictions = jurisdictions });
        var evidence = new AttendancePayrollInputEvidence(1, EmployeeId, Month, 1, fingerprintAll, calculation, jurisdictions, scopes,
            PayeeAccount.Id, "SYNTHETIC upstream attendance, no live punches or account activation", Clock.Now);
        var retained = await Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(evidence), $"attendance:payroll-inputs:{EmployeeId:N}:{Month:yyyy-MM}:1", default);
        var revision = new AttendancePayrollInputRevision(EmployeeId, Month, 1, null, calculationVersion, fingerprintAll,
            JsonSerializer.Serialize(retained), PayeeAccount.Id, Clock.Now);
        var period = new AttendancePeriodLock(Month, Month.AddMonths(1)); var eventId = Guid.NewGuid();
        var lockSnapshot = new MonthLockSnapshot(3, eventId, period.Id, 1, Month, Month.AddMonths(1), ActorId.ToString(),
            "SYNTHETIC upstream month fixture", Clock.Now, MonthLockEvidence.Fingerprint(summaries), summaries.Select(x => new MonthLockSummaryEntry(EmployeeId,
                x.AttendanceDate, x.Id, x.InputFingerprint, version.Id, x.CalculationVersion)).ToArray(), [],
            [new(EmployeeId, Month, revision.Id, revision.Revision, revision.InputFingerprint, revision.CalculationVersion,
                retained.Sha256, Guid.NewGuid(), MonthLockEvidence.Fingerprint(new { SyntheticNoCharge = true }),
                MonthLockEvidence.Fingerprint(new { SyntheticFinancialMembership = revision.Id }))], []);
        var lockProof = await Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(lockSnapshot), MonthLockEvidence.Purpose(period.Id, eventId), default);
        var locked = period.RecordDirectLock(eventId, lockSnapshot.Actor, lockSnapshot.Reason, Clock.Now, JsonSerializer.Serialize(lockProof));
        Db.AddRange(summaries); Db.AddRange(revision, period, locked);
        Db.AddRange(summaries.Select(x => new AttendancePeriodSummary(period, locked, x)));
        await Db.SaveChangesAsync();
        var months = new MonthLockRepository(Db);
        var reader = new SyntheticRetainedMonthReader(Files);
        await SaveEvidenceAsync("synthetic-attendance-manifest.json", new { RevisionId = revision.Id, PeriodId = period.Id, EventId = locked.Id, Count = summaries.Count,
            Boundary = "Persisted synthetic upstream evidence, actual time/payroll calculators; no device/biometric/attendance-policy acceptance" });
        return new(Operation, new PayrollHandoffRepository(Db), months, new AttendancePayrollInputLedger(Db), null!, null!, Access, Files,
            reader, salaryRepository: SalaryRepository, salarySources: Salary,
            statutory: new PayrollStatutorySourceReader(Db, Files));
    }

    private sealed class SyntheticRetainedMonthReader(IPrivateFileStore files) : IMonthLockSnapshotReader
    {
        public async Task<MonthLockSnapshot> ReadRetainedAsync(MonthLockExisting existing, AttendanceActor actor, CancellationToken ct)
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(existing.Event!.ProtectedEvidenceReference!)!;
            var result = JsonSerializer.Deserialize<MonthLockSnapshot>(await files.ReadAsync(reference,
                MonthLockEvidence.Purpose(existing.Period.Id, existing.Event.Id), ct))!;
            MonthLockEvidence.Validate(existing, result); return result;
        }
    }
    internal static Task SaveEvidenceAsync<T>(string name, T value) => File.WriteAllTextAsync(Path.Combine(EvidenceRoot, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (Db.Database.CurrentTransaction is not null || Db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Acceptance mutations must be staged inside their own audited transaction.");
        Db.ChangeTracker.Clear();
        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try { var result = await action(ct); await transaction.CommitAsync(ct); return result; }
        catch (Exception original)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackFailure)
            {
                // A repository that wrongly commits its caller's transaction must surface its
                // original failure, while keeping a separate diagnostic for the failed rollback.
                try { await File.AppendAllTextAsync(Path.Combine(EvidenceRoot, "rollback-diagnostics.log"),
                    $"{DateTimeOffset.UtcNow:O} Original: {original}\nRollback: {rollbackFailure}\n"); }
                catch { /* Preserve the business exception even if diagnostic storage fails. */ }
            }
            Db.ChangeTracker.Clear(); throw;
        }
        finally { Db.ChangeTracker.Clear(); }
    }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
    internal sealed class AcceptanceClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T, PayrollMonthCallbackProxy>(); ((PayrollMonthCallbackProxy)(object)value).Handler = handler; return value; }
}

public class PayrollMonthCallbackProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
}
