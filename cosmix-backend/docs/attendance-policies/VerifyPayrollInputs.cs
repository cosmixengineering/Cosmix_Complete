#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.Policies;

// Standalone synthetic verification; no database, network, test project or compensation mutation.
int checks = 0;
void Check(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
async Task Conflict(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception(code); }
var month = new DateOnly(2026, 10, 1);
AttendancePayrollInputResult Calculate(params AttendancePayrollInputDay[] days) => AttendancePayrollInputRules.Calculate(month, days);
var absent = Fixture.Day(month, AttendanceDayStatus.Absent, 0);
Check(Calculate(absent).Lines.Single().Amount == 1000m, "Full unpaid day uses basic/actual payroll-month days");
var half = Fixture.Day(month, AttendanceDayStatus.HalfDay, 270);
Check(Calculate(half).Lines.Single().Amount == 500m, "Explicit unpaid half-day factor");
var hourly = Fixture.Day(month, AttendanceDayStatus.Hourly, 480);
var hourLine = Calculate(hourly).Lines.Single();
Check(hourLine.Amount == 111.11m && hourLine.ExactAmountNumerator == "1000" && hourLine.ExactAmountDenominator == "9", "Exact one-hour shortfall, round final amount once");
var full = Fixture.Day(month, AttendanceDayStatus.Present, 540);
Check(Calculate(full).State == "ready" && Calculate(full).Lines.Count == 0, "Full duty has no ordinary deduction");
Check(Calculate(full with { Time = full.Time! with { DayStatus = AttendanceDayStatus.RestDay, PayableTicks = 0 } }).Lines.Count == 0, "Weekly off exempt");
Check(Calculate(full with { Time = full.Time! with { DayStatus = AttendanceDayStatus.Holiday, PayableTicks = 0 } }).Lines.Count == 0, "Applicable holiday exempt");
var paidLeave = full with { Time = full.Time! with { DayStatus = AttendanceDayStatus.Leave }, TimeInput = full.TimeInput! with
{ Leave = [new(new(full.TimeInput!.ShiftStartsAtUtc!.Value, full.TimeInput.ShiftEndsAtUtc!.Value), true)] } };
Check(Calculate(paidLeave).Lines.Count == 0, "Approved full paid leave exempt");
var partial = half with { Time = half.Time! with { PayableTicks = TimeSpan.FromHours(6).Ticks }, TimeInput = half.TimeInput! with
{ Leave = [new(new(half.TimeInput!.ShiftStartsAtUtc!.Value, half.TimeInput.ShiftStartsAtUtc.Value.AddHours(6)), true)] } };
Check(Calculate(partial).Lines.Single().Amount == 333.33m, "Half-day capped to remaining unpaid capacity, paid leave exempt");
var leaveHalfRules = Fixture.Rules with { OrdinaryPay = Fixture.Rules.OrdinaryPay! with { UnpaidHalfDayDeductionDays = .25m } };
var leaveHalf = Fixture.Day(month, AttendanceDayStatus.Present, 270, leaveHalfRules);
var leaveHalfStart = leaveHalf.TimeInput!.ShiftStartsAtUtc!.Value;
var leaveHalfInput = leaveHalf.TimeInput with { Leave = [new(new(leaveHalfStart, leaveHalfStart.AddHours(4.5)), false)],
    Duty = [new(leaveHalfStart.AddHours(4.5), leaveHalfStart.AddHours(9))] };
leaveHalf = leaveHalf with { TimeInput = leaveHalfInput, Time = AttendanceDailyTimeCalculation.Calculate(leaveHalfInput) };
Check(leaveHalf.Time.DayStatus == AttendanceDayStatus.Present, "Approved unpaid half-day shifts expected arrival without a late half-day classification");
Check(Calculate(leaveHalf).Lines.Single().Amount == 250m, "Exact approved unpaid half-day with completed remainder uses configured .25 factor");
var zeroHalf = leaveHalfRules with { OrdinaryPay = leaveHalfRules.OrdinaryPay! with { UnpaidHalfDayDeductionDays = 0 } };
Check(Calculate(leaveHalf with { Rules = zeroHalf }).Lines.Count == 0, "Exact approved unpaid half-day honors explicit zero factor");
var almostHalfInput = leaveHalfInput with { Leave = [new(new(leaveHalfStart, leaveHalfStart.AddHours(4.5).AddTicks(1)), false)],
    Duty = [new(leaveHalfStart.AddHours(4.5).AddTicks(1), leaveHalfStart.AddHours(9))] };
Check(Calculate(leaveHalf with { TimeInput = almostHalfInput, Time = AttendanceDailyTimeCalculation.Calculate(almostHalfInput) })
    .Lines.Single().Kind == "ordinary_shortfall", "No rounding a partial leave interval into half-day eligibility");
var shortHalfInput = leaveHalfInput with { Duty = [new(leaveHalfStart.AddHours(4.5), leaveHalfStart.AddHours(6))],
    Rules = leaveHalfRules with { PaidTime = leaveHalfRules.PaidTime! with { EarlyExitTreatment = AttendanceEarlyExitTreatment.ActualWorkedTime } } };
Check(Calculate(leaveHalf with { Rules = shortHalfInput.Rules, TimeInput = shortHalfInput, Time = AttendanceDailyTimeCalculation.Calculate(shortHalfInput) })
    .Lines.Single().Kind == "ordinary_shortfall", "Incomplete remaining duty retains hourly shortfall instead of reduced half-day factor");
var breakRules = Fixture.Rules with { PaidTime = Fixture.Rules.PaidTime! with { BreakTreatment = AttendanceBreakTreatment.DeductScheduledUnpaidBreak } };
var breakDay = Fixture.Day(month, AttendanceDayStatus.Present, 480, breakRules);
breakDay = breakDay with { TimeInput = breakDay.TimeInput! with { UnpaidBreakMinutes = 60 } };
Check(Calculate(breakDay).Lines.Count == 0, "Completed duty does not charge the configured unpaid break");
var late1 = Fixture.Day(month, AttendanceDayStatus.Present, 540) with { Time = full.Time! with { CountsAsLateOccurrence = true } };
var late2 = Fixture.Day(month.AddDays(1), AttendanceDayStatus.Present, 540) with { Time = full.Time! with { CountsAsLateOccurrence = true } };
var late3 = Fixture.Day(month.AddDays(2), AttendanceDayStatus.Present, 540) with { Time = full.Time! with { CountsAsLateOccurrence = true } };
Check(Calculate(late1, late2).Lines.Count == 0, "Incomplete threshold does not deduct");
Check(Calculate(late1, late2, late3).Lines.Single().Amount == 1000m, "Threshold completion emits one deduction");
var newSalary = late3.Compensation! with { Id = Guid.NewGuid(), BaseAmount = 62000, GrossAmount = 62000, EffectiveFrom = month.AddDays(2), Fingerprint = new('D', 64) };
Check(Calculate(late1, late2, late3 with { Compensation = newSalary }).Lines.Single().Amount == 2000m, "Completion-date salary and no salary-reset of prior count");
var ordinaryChanged = Fixture.Rules with { OrdinaryPay = Fixture.Rules.OrdinaryPay! with { UnpaidHalfDayDeductionDays = .4m } };
Check(Calculate(late1, late2, late3 with { Rules = ordinaryChanged, PolicyVersionId = Guid.NewGuid() }).Lines.Single().Kind == "late_threshold", "Unrelated policy/new version does not reset late count");
var arrivalChanged = Fixture.Rules with { Arrival = Fixture.Rules.Arrival! with { LateAfterShiftStartSeconds = 600 } };
Check(AttendancePayrollInputRules.LateSignature(arrivalChanged) == AttendancePayrollInputRules.LateSignature(Fixture.Rules), "Signature excludes arrival rules");
var lateChanged = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with { OccurrenceThreshold = 2 } };
Check(Calculate(late1, late2, late3 with { Rules = lateChanged }).Lines.Count == 0, "Late-rule change starts a new period");
var disabled = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with { Enabled = false } };
Check(Calculate(late1 with { Rules = disabled }, late2 with { Rules = disabled }, late3 with { Rules = disabled }).Lines.Count == 0, "Disabled late penalty produces no threshold line");
var halfLate = Fixture.Day(month, AttendanceDayStatus.HalfDay, 270) with { Time = half.Time! with { CountsAsLateOccurrence = true } };
var max = Calculate(halfLate, late2, late3);
Check(max.State == "ready" && max.Lines.Sum(x => x.Amount) == 1000m && max.Lines.Single(x => x.Kind == "late_threshold").CandidateAmount == 1000m, "Larger-only composition retains ordinary and only late excess");
var maxOrdinary = Calculate(halfLate, halfLate with { BusinessDate = month.AddDays(1) }, halfLate with { BusinessDate = month.AddDays(2) });
Check(maxOrdinary.Lines.Sum(x => x.Amount) == 1500m && maxOrdinary.Lines.Single(x => x.Kind == "late_threshold").Amount == 0, "Higher ordinary amount suppresses late candidate");
var fixedRules = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with
{ DeductionKind = AttendanceLateDeductionKind.FixedAmount, DeductionDaysPerThreshold = null, FixedAmountPkr = 75.25m } };
Check(Calculate(late1 with { Rules = fixedRules }, late2 with { Rules = fixedRules }, late3 with { Rules = fixedRules }).Lines.Single().Amount == 75.25m, "Configured fixed PKR penalty");
var hoursRules = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with
{ DeductionKind = AttendanceLateDeductionKind.Hours, DeductionDaysPerThreshold = null, DeductionHoursPerThreshold = 2 } };
Check(Calculate(late1 with { Rules = hoursRules }, late2 with { Rules = hoursRules }, late3 with { Rules = hoursRules }).Lines.Single().Amount == 222.22m, "Configured penalty hours use completion scheduled hours");
var weekly = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with
{ ResetPeriod = AttendanceLateResetPeriod.Weekly, WeekStartsOn = AttendanceLateWeekStartDay.Monday } };
var week = AttendanceDailyTimeCalculation.LatePeriod(month, AttendanceLateResetPeriod.Weekly, AttendanceLateWeekStartDay.Monday);
Check(week.From == new DateOnly(2026, 9, 28) && week.ToExclusive == new DateOnly(2026, 10, 5), "Explicit weekly boundary");
Check(Calculate(late1 with { Rules = weekly }, late2 with { Rules = weekly }, late3 with { BusinessDate = month.AddDays(4), Rules = weekly }).Lines.Count == 0, "New weekly period resets count");
var yearly = Fixture.Rules with { LateDeduction = Fixture.Rules.LateDeduction! with { ResetPeriod = AttendanceLateResetPeriod.CalendarYear } };
var prior = halfLate with { BusinessDate = month.AddDays(-1), Rules = yearly };
Check(Calculate(prior, late2 with { Rules = yearly }, late3 with { Rules = yearly }).Issues.Any(x => x.Code == "prior_deduction_reconciliation_required"), "Cross-month prior ordinary charge cannot be assumed settled");
Check(Calculate(late1 with { State = "leave_decision_pending" }, late2, late3).State == "unavailable", "Missing day cannot silently preserve threshold counts");
Check(Calculate(absent with { Compensation = null }).Issues.Any(x => x.Code == "compensation_evidence_unavailable"), "Missing salary explicit");
Check(Calculate(absent with { Compensation = absent.Compensation! with { Currency = "USD" } }).Issues.Any(x => x.Code == "pkr_compensation_required"), "Other currency unavailable");
Check(Calculate(absent with { Compensation = absent.Compensation! with { WageBasis = WageBasis.Daily } }).Issues.Any(x => x.Code == "monthly_compensation_required"), "Other wage basis unavailable");
Check(Calculate(absent, absent).State == "unavailable", "Duplicate source day rejected");
var legacy = Fixture.Rules with { SchemaVersion = 2, OrdinaryPay = null,
    LateDeduction = new(3, AttendanceLateResetPeriod.CalendarYear, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp) };
var beforeLegacy = late1 with { BusinessDate = month.AddDays(-3), Rules = legacy };
var startedV3 = late1 with { BusinessDate = month.AddDays(-2), Rules = yearly };
var legacyReset=Calculate(beforeLegacy, startedV3, late2 with { Rules = yearly }, late3 with { Rules = yearly });
Check(legacyReset.State == "unavailable" && legacyReset.Issues.Any(x=>x.Code=="prior_deduction_reconciliation_required") &&
    legacyReset.Issues.All(x=>x.Code!="late_period_history_unavailable"),
    "Known legacy-to-schema3 late-rule change resets count uncertainty without inventing prior posting evidence");
var confirmedResetDay=new AttendancePayrollConfirmedOrdinaryDay(startedV3.BusinessDate,
    new(startedV3.BusinessDate.Year,startedV3.BusinessDate.Month,1),Guid.NewGuid(),new('A',64),Guid.NewGuid(),new('B',64),
    "posted",startedV3.SourceFingerprint,startedV3.PolicyVersionId!.Value,startedV3.Compensation!.Id,
    startedV3.Compensation.Fingerprint,null);
var confirmedReset=AttendancePayrollInputRules.Calculate(month,
    [beforeLegacy,startedV3,late2 with {Rules=yearly},late3 with {Rules=yearly}],[confirmedResetDay]);
Check(confirmedReset.State=="ready" && confirmedReset.Lines.Count==1,
    "Known late-rule reset still completes its exact threshold when prior no-ordinary evidence is explicitly confirmed");
var oldMissing = late1 with { BusinessDate = month.AddDays(-2), State = "leave_decision_pending", Rules = yearly };
Check(Calculate(oldMissing, late1, late2, late3).State == "ready", "Calendar-month boundary clears previous period uncertainty");
var tickDay = hourly with { Time = hourly.Time! with { PayableTicks = TimeSpan.FromHours(9).Ticks - 1 } };
Check(Calculate(tickDay).Lines.Single().ExactAmountNumerator == "1" && Calculate(tickDay).Lines.Single().Amount == 0,
    "One-tick shortfall preserved through rational calculation despite zero rounded amount");
var leap = new DateOnly(2024, 2, 1);
Check(AttendancePayrollInputRules.Calculate(leap, [Fixture.Day(leap, AttendanceDayStatus.Absent, 0)]).Lines.Single().SalaryDivisorDays == 29, "Leap payroll month divisor");

// Exercise the real producer and protected compensation verifier, with synthetic repository seams.
var fixture = new Fixture();
var producer = fixture.Producer();
var scopes = await producer.ResourceScopesAsync(fixture.Employee, month, default);
var actual = await producer.CalculateAsync(fixture.Employee, month, default);
Check(scopes.Count == 1 && actual.SourceFrom == month && actual.SourceTo == month.AddDays(2), "Actual producer clamps lookback to verified employment and emits scopes");
Check(actual.State == "ready" && actual.Lines.Count == 3 && actual.Lines.Sum(x => x.Amount) == 3000, "Actual producer calculates from verified salary and daily snapshots");
Check(fixture.Files.Reads == 2 && fixture.CompensationReads == 1 && fixture.SessionReads == 1, "Compensation protected evidence cached per revision; source reads batched");
Check(actual.Lines.All(x => x.SourceIds.Contains(fixture.Salary.Id) && x.SourceIds.Contains(fixture.Decision.Id)), "Candidate source identities include compensation approval decision");
Check(actual.SourceManifest.Days.All(x => x.Snapshot is not null && x.Compensation?.Fingerprint.Length == 64), "Protected manifest retains exact daily and salary provenance");
var again = await producer.CalculateAsync(fixture.Employee, month, default);
Check(again.InputFingerprint == actual.InputFingerprint, "Unchanged source produces stable fingerprint");
fixture.DailyState = "leave_decision_pending";
var pending = await producer.CalculateAsync(fixture.Employee, month, default);
Check(pending.State == "unavailable" && pending.InputFingerprint != actual.InputFingerprint, "Pending leave changes actual producer state and fingerprint");
fixture.DailyState = "complete";
fixture.Files.Tamper = true;
await Conflict(() => producer.CalculateAsync(fixture.Employee, month, default), "payroll_compensation_protected_evidence_conflict");
fixture.Files.Tamper = false; fixture.HistoryConflict = true;
await Conflict(() => producer.CalculateAsync(fixture.Employee, month, default), "payroll_compensation_history_conflict");
var initial = new Fixture(); initial.UseInitialHiring();
var hired = await initial.Producer().CalculateAsync(initial.Employee, month, default);
Check(hired.State == "ready" && initial.Files.Reads == 1, "Initial salary bound to one protected hiring snapshot read");
Check(hired.Lines.All(x => x.SourceIds.Contains(initial.Hiring!.Decision.Id) && x.SourceIds.Contains(initial.Hiring.Submission.Id)), "Initial salary retains onboarding decision and submission IDs");
initial.Files.Tamper = true;
await Conflict(() => initial.Producer().CalculateAsync(initial.Employee, month, default), "payroll_initial_compensation_protected_evidence_conflict");
var officeFixture = new Fixture { Office = true };
var officeInputs = await officeFixture.Producer().CalculateAsync(officeFixture.Employee, month, default);
Check(officeInputs.State == "ready" && officeInputs.SourceManifest.Days.All(x => x.BranchId == officeFixture.Branch &&
    x.OfficeSource?.BranchId == officeFixture.Branch && x.Snapshot?.Roster.SiteId is null), "Office payroll manifest has exact branch source and no fabricated Site");
Check(officeInputs.Lines.All(x => new[] { officeFixture.Branch, officeFixture.Boundary, officeFixture.Assignment,
    officeFixture.Member, officeFixture.Shift, officeFixture.Calendar }.All(x.SourceIds.Contains)), "Office payroll lines retain all historical source identities");
var originalBoundary = officeFixture.Boundary; officeFixture.Boundary = Guid.NewGuid();
var reboundOffice = await officeFixture.Producer().CalculateAsync(officeFixture.Employee, month, default);
Check(reboundOffice.InputFingerprint != officeInputs.InputFingerprint, "Office boundary changes alter the payroll fingerprint");
officeFixture.Boundary = originalBoundary; officeFixture.MisbindOffice = true;
await Conflict(() => officeFixture.Producer().CalculateAsync(officeFixture.Employee, month, default), "payroll_office_source_conflict");
officeFixture.MisbindOffice = false; officeFixture.DailyState = "office_boundary_not_configured";
var missingOffice = await officeFixture.Producer().CalculateAsync(officeFixture.Employee, month, default);
Check(missingOffice.State == "unavailable" && missingOffice.Lines.Count == 0 && missingOffice.SourceManifest.Days.All(x =>
    x.BranchId == officeFixture.Branch && x.OfficeSource is null && x.Snapshot is null), "Missing Office proof retains branch and pending state, not synthetic money");
var broaderPayroll = new Fixture { Office = true, CategoryCode = "SUPPORT" };
broaderPayroll.Taxonomy = new("SUPPORT", Guid.NewGuid(), new('D', 64), StaffWorkLocation.Office, StaffScheduleBasis.Office, false);
var broaderInputs = await broaderPayroll.Producer().CalculateAsync(broaderPayroll.Employee, month, default);
Check(broaderInputs.State == "ready" && broaderInputs.SourceManifest.Days.All(x => x.Snapshot!.Employment.StaffCategory == "SUPPORT" &&
    x.Snapshot.Roster.Taxonomy == broaderPayroll.Taxonomy), "Approved broader Office taxonomy remains pinned in actual payroll producer");
broaderPayroll.Taxonomy = null;
await Conflict(() => broaderPayroll.Producer().CalculateAsync(broaderPayroll.Employee, month, default), "payroll_office_source_conflict");
broaderPayroll.Taxonomy = new("OTHER", Guid.NewGuid(), new('D', 64), StaffWorkLocation.Office, StaffScheduleBasis.Office, false);
await Conflict(() => broaderPayroll.Producer().CalculateAsync(broaderPayroll.Employee, month, default), "payroll_office_source_conflict");
broaderPayroll.Taxonomy = new("SUPPORT", Guid.NewGuid(), new('D', 64), StaffWorkLocation.Site, StaffScheduleBasis.Site, true);
await Conflict(() => broaderPayroll.Producer().CalculateAsync(broaderPayroll.Employee, month, default), "payroll_office_source_conflict");
Console.WriteLine($"PASS: {checks} payroll-input pure rules and real producer/protected compensation assertions; no DB/network or salary writes.");

sealed class Fixture : IAttendancePayrollInputRepository, IAttendanceSummaryRepository, IIndividualRosterResolver,
    IAttendanceCalculationSnapshotProducer, IEmployeeCompensationRepository, IAttendancePolicyResolver
{
    public string? CategoryCode;
    public RosterStaffTaxonomySource? Taxonomy;
    public static AttendancePolicyRules Rules => new(3, new(900, 3600, 9000),
        new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
            AttendanceEarlyExitTreatment.HalfDayCredit, 16200, AttendanceNonScheduledDayTreatment.NoCredit,
            AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
        new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
            AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange, AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
            true, AttendanceLateDeductionKind.DayFraction, null, null, null, AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate),
        new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, .5m,
            AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
            AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp));
    public static AttendancePayrollInputDay Day(DateOnly day, AttendanceDayStatus status, int payable, AttendancePolicyRules? rules = null)
    {
        rules ??= Rules;
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        return new(day, "available", Guid.Parse("10000000-0000-0000-0000-000000000001"), rules,
            new(rules, start, start.AddHours(9), 540, 0, false, false, [], []),
            new("calculated", status, payable * TimeSpan.TicksPerMinute, 0, false, []),
            new(Guid.Parse("10000000-0000-0000-0000-000000000002"), 31000, 31000, "PKR", WageBasis.Monthly,
                new(2020, 1, 1), null, new('C', 64), []), new('A', 64), []);
    }
    public Guid Employee = Guid.NewGuid(), PolicyId = Guid.NewGuid(), Assignment = Guid.NewGuid();
    public MemoryFiles Files = new();
    public CompensationRecord Salary;
    public ApprovalRequest Request;
    public ApprovalDecision Decision;
    public AttendancePayrollHiringSource? Hiring;
    public string DailyState = "complete";
    public bool HistoryConflict;
    public bool Office, MisbindOffice;
    public Guid Branch = Guid.NewGuid(), Boundary = Guid.NewGuid(), Member = Guid.NewGuid(), Shift = Guid.NewGuid(), Calendar = Guid.NewGuid();
    public int CompensationReads, SessionReads;
    public Fixture()
    {
        var date = new DateOnly(2026, 10, 1); var now = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var terms = new CompensationTerms(31000, 31000, "PKR", WageBasis.Monthly, PaymentMethod.Bank);
        var proposal = new CompensationChangeSnapshot(1, Employee, null, terms, date, "Synthetic salary source");
        Request = new(EmployeeCompensationService.RequestType, Employee, Guid.NewGuid().ToString(), Files.Put(proposal, "proposal"), 1, now);
        var actor = Guid.NewGuid().ToString();
        Salary = new(Employee, 1, null, 31000, 31000, "PKR", WageBasis.Monthly, PaymentMethod.Bank, date, null, Request.Id, actor, now.AddMinutes(1));
        var after = new CompensationEntry(Salary.Id, 1, null, terms, date, null, null, Request.Id, actor, Salary.RecordedAt);
        Decision = Request.Decide(ApprovalState.Approved, actor, "Admin", null, Salary.RecordedAt, Files.Put(new { before = (CompensationEntry?)null, after }, "change"));
    }
    public void UseInitialHiring()
    {
        Salary = new(Employee, 1, null, 31000, 31000, "PKR", WageBasis.Monthly, PaymentMethod.Bank,
            new(2026, 10, 1), null, null, Salary.RecordedByUserId, Salary.RecordedAt);
        var snapshot = new { EmployeeId = Employee, EmployeeNumber = 100001L, DraftRevision = 1,
            Profile = new EmployeeProfileInput(), Financial = new EmployeeFinancialInput { BaseSalary = 31000, GrossSalary = 31000,
                Currency = "PKR", WageBasis = WageBasis.Monthly, PaymentMethod = PaymentMethod.Bank, EffectiveFrom = Salary.EffectiveFrom },
            Medical = (EmployeeMedicalInput?)null, Documents = Array.Empty<EmployeeDocumentResponse>() };
        var serialized = Files.Put(snapshot, "initial");
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized)!;
        var submission = (EmployeeSubmission)Activator.CreateInstance(typeof(EmployeeSubmission), true)!;
        var decision = (EmployeeOnboardingDecision)Activator.CreateInstance(typeof(EmployeeOnboardingDecision), true)!;
        void Set(object row, string name, object? value) => row.GetType().GetProperty(name)!.SetValue(row, value);
        Set(submission, "Id", Guid.NewGuid()); Set(submission, "EmployeeId", Employee); Set(submission, "DraftRevision", 1);
        Set(submission, "Version", 1); Set(submission, "Provenance", OnboardingProvenance.HrWorkflow);
        Set(submission, "SubmittedByUserId", Guid.NewGuid()); Set(submission, "SnapshotSchemaVersion", 1);
        Set(submission, "SnapshotStorageKey", serialized); Set(submission, "SnapshotObjectVersion", reference.ObjectVersion);
        Set(submission, "SnapshotHash", reference.Sha256); Set(submission, "EncryptionKeyReference", reference.KeyReference);
        Set(decision, "Id", Guid.NewGuid()); Set(decision, "EmployeeId", Employee); Set(decision, "SubmissionId", submission.Id);
        Set(decision, "Decision", OnboardingDecision.Approve); Set(decision, "Provenance", OnboardingProvenance.HrWorkflow);
        Set(decision, "ActorUserId", Guid.Parse(Salary.RecordedByUserId)); Set(decision, "DecidedAt", Salary.RecordedAt);
        Set(decision, "ActorRoleId", Guid.NewGuid()); Set(submission, "SubmittedAt", Salary.RecordedAt.AddMinutes(-1));
        Hiring = new(submission, decision);
    }
    public AttendancePayrollInputProducer Producer() => new(this, this, this, this, this,
        new(null!, null!, null!, null!, null!, TimeProvider.System, Files), this);
    public Task<MonthLockPopulation> CoverageAsync(Guid id, DateOnly from, DateOnly end, CancellationToken ct) => Task.FromResult(
        new MonthLockPopulation(Enumerable.Range(0, 3).Select(x => new MonthLockCell(id, new DateOnly(2026, 10, 1).AddDays(x))).ToArray(), new('A', 64), []));
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid id, DateOnly from, DateOnly end, CancellationToken ct)
    { SessionReads++; return Task.FromResult<IReadOnlyList<WorkSession>>([]); }
    public Task<IReadOnlyList<AttendancePayrollCompensationSource>> CompensationAsync(Guid id, CancellationToken ct)
    { CompensationReads++; return Task.FromResult<IReadOnlyList<AttendancePayrollCompensationSource>>([new(Salary, Salary.ApprovalRequestId.HasValue ? Request : null, Salary.ApprovalRequestId.HasValue ? [Decision] : [])]); }
    public Task<AttendancePayrollHiringSource?> HiringAsync(Guid id, CancellationToken ct) => Task.FromResult(Hiring);
    public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid id, DateOnly from, DateOnly to, CancellationToken ct) => Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId: id)]);
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid id, DateOnly day, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkSession>>([]);
    public Task<int> ExceptionCountAsync(Guid id, DateOnly day, CancellationToken ct) => Task.FromResult(0);
    public Task<bool> IsLockedAsync(DateOnly day, CancellationToken ct) => Task.FromResult(false);
    public Task<DailyAttendanceSummary?> LatestAsync(Guid id, DateOnly day, CancellationToken ct) => Task.FromResult<DailyAttendanceSummary?>(null);
    public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid id, DateOnly day, int page, int size, CancellationToken ct) => throw new NotSupportedException();
    public void Add(DailyAttendanceSummary summary, IReadOnlyList<AttendanceSummarySession> sessions) => throw new Exception("No writes");
    public Task<EffectiveRosterResponse> ResolveAsync(Guid id, DateOnly day, CancellationToken ct)
    {
        var roster = new EffectiveRosterResponse(id, day, "scheduled", "Asia/Karachi", AssignmentId: Assignment, ConfiguredScheduledMinutes: 540);
        if (Office) roster = roster with { BranchId = Branch, ShiftVersionId = Shift,
            Explanation = new("office", null, Member, null, null, "synthetic") { OfficeRosterAssignmentId = Assignment },
            Holiday = new("configured", day, Calendar, "synthetic", 1, []) };
        return Task.FromResult(roster with { Taxonomy = Taxonomy });
    }
    Task<AttendancePolicyResolution> IAttendancePolicyResolver.ResolveAsync(Guid id, DateOnly day, CancellationToken ct) => Task.FromResult(Policy());
    AttendancePolicyResolution Policy() => new("effective", PolicyId, [Assignment], Rules, AttendancePolicyRulesCodec.Canonicalize(Rules), new('A', 64));
    public async Task<AttendanceCalculationSnapshotResult> ReadAsync(Guid id, DateOnly day, FactualAttendance facts, EffectiveRosterResponse roster, CancellationToken ct)
    {
        var input = Day(day, AttendanceDayStatus.Absent, 0);
        if (DailyState != "complete") return new(DailyState, null, new('B', 64));
        OfficeAttendanceSource? office = null;
        if (Office)
        {
            office = OfficeAttendanceSource.Capture(id, day, roster, new(Boundary, Branch, 1, new(2026, 9, 1),
                24m, 67m, 100m, 20m, "PK-SD", "synthetic", "synthetic", new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
            if (MisbindOffice) office = office! with { BranchId = Guid.NewGuid() };
        }
        return new("complete", new(1, id, day, AttendanceDailyTimeCalculation.Version, facts.InputFingerprint,
            await ResolveAsync(id, day, ct), new(Office ? Member : Assignment, Guid.Parse("10000000-0000-0000-0000-000000000003"),
                CategoryCode ?? (Office ? "OFFICE" : "SITE"), null, new(2026, 10, 1), null, Office ? Branch : null),
            Policy(), new(LeaveContextStatus.Complete, [], new('A', 64), []), [], [], [], [], input.TimeInput!, 0, office), new('A', 64));
    }
    public Task<bool> HasConflictingHistoryAsync(Guid id, CancellationToken ct) => Task.FromResult(HistoryConflict);
    public Task<CompensationWindow> WindowAsync(Guid id, DateOnly date, CancellationToken ct) => throw new NotSupportedException();
    public Task<PagedResult<CompensationEntry>> HistoryAsync(Guid id, CompensationPageQuery q, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasPendingRequestAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<ApprovalRequest?> FindRequestAsync(Guid id, Guid request, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<PagedResult<CompensationRequestSummary>> RequestsAsync(Guid? id, CompensationRequestQuery q, CancellationToken ct) => throw new NotSupportedException();
    public void ExpectVersion(ApprovalRequest request, byte[] version) => throw new Exception("No writes");
    public void AddRequest(ApprovalRequest request) => throw new Exception("No writes");
    public void AddDecision(ApprovalDecision decision) => throw new Exception("No writes");
    public void AddCompensation(CompensationRecord record) => throw new Exception("No writes");
}
sealed class MemoryFiles : IPrivateFileStore
{
    readonly Dictionary<string, byte[]> values = []; public int Reads; public bool Tamper;
    public string Put(object value, string key)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value); values[key] = data;
        return JsonSerializer.Serialize(new PrivateFileReference(key, "v1", Convert.ToHexString(SHA256.HashData(data)), "synthetic", data.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        Reads++; var value = values[reference.StorageKey];
        if (Tamper && reference.StorageKey is "change" or "initial") value = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(value).Replace("31000", "32000"));
        return Task.FromResult(value);
    }
    public Task<PrivateFileReference> WriteAsync(byte[] data, string purpose, CancellationToken ct) => throw new Exception("No writes");
}
