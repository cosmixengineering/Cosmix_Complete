#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.HR.Policies;
using System.Text.Json;
var checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
var start = new DateTimeOffset(2026, 10, 12, 5, 0, 0, TimeSpan.Zero);
var rules = new AttendancePolicyRules(2, new(900, 3600, 10740),
    new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
        AttendanceEarlyExitTreatment.ActualWorkedTime, 16200, AttendanceNonScheduledDayTreatment.NoCredit,
        AttendanceNonScheduledDayTreatment.NoCredit, AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
    new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
var input = new AttendanceTimeInput(rules, start, start.AddHours(9), 540, 0, false, false,
    [new(start.AddMinutes(-30), start.AddHours(9))], []);
var old = AttendanceDailyTimeCalculation.Calculate(input);
Check(old.PayableTicks == 9 * TimeSpan.TicksPerHour, "Unapproved early work is excluded.");
Check(!JsonSerializer.Serialize(input).Contains("ReviewedWork"), "Null optional property keeps legacy bytes.");
Check(AttendanceDailyTimeCalculation.CalculationVersionFor(input) == AttendanceDailyTimeCalculation.VersionFor(rules), "Legacy version unchanged.");
var reviewed = new ReviewedAttendanceWork([new(start.AddMinutes(-30), start)], [], [], null, null);
var early = AttendanceDailyTimeCalculation.Calculate(input with { ReviewedWork = reviewed });
Check(early.PayableTicks == 9 * TimeSpan.TicksPerHour + 30 * TimeSpan.TicksPerMinute, "Actual approved early credit.");
Check(AttendanceDailyTimeCalculation.CalculationVersionFor(input with { ReviewedWork = reviewed }).Contains("v3"), "New interpretation has distinct version.");
Check(early.Lines.Single(x => x.Rule == "approved_named_early_credit").Ticks == 30 * TimeSpan.TicksPerMinute, "Early proof amount explained.");
Check(AttendanceDailyTimeCalculation.Calculate(input with { ReviewedWork = reviewed with { EarlyCredit = [new(start.AddHours(-1), start)] } }).Status != "calculated",
    "Claim not inside accepted duty rejected.");
Check(AttendanceDailyTimeCalculation.Calculate(input with { ReviewedWork = reviewed with { EarlyCredit = [new(start.AddMinutes(-30), start.AddMinutes(1))] } }).Status != "calculated",
    "Not an early range after shift start.");
var reserved = input with { ReviewedWork = new([], [new(start.AddHours(1), start.AddHours(2))], [], null, null) };
Check(AttendanceDailyTimeCalculation.Calculate(reserved).PayableTicks == 8 * TimeSpan.TicksPerHour, "Reserved physical hour cannot be ordinary time too.");
Check(AttendanceDailyTimeCalculation.Calculate(input with { ReviewedWork = reviewed with { ReservedNonOrdinary = reviewed.EarlyCredit } }).Status != "calculated",
    "Same interval cannot be early and another work allocation.");
var studyInput = input with { Duty = [new(start.AddHours(2), start.AddHours(9))],
    ReviewedWork = new([], [], [new(start, start.AddHours(2))], true, true) };
var study = AttendanceDailyTimeCalculation.Calculate(studyInput);
Check(study.Status == "calculated" && study.DayStatus == AttendanceDayStatus.Hourly, "Explicit academic hourly choice.");
Check(study.CountsAsLateOccurrence == true && study.KpiCountsAsLateOccurrence == false,
    "Academic KPI exception does not silently change ordinary financial late evidence.");
Check(AttendanceDailyTimeCalculation.Calculate(studyInput with { ReviewedWork = studyInput.ReviewedWork! with { StudyExcludeFromKpi = false } }).KpiCountsAsLateOccurrence == true,
    "Explicit ordinary academic KPI treatment remains distinct.");
Check(AttendanceDailyTimeCalculation.Calculate(studyInput with { ReviewedWork = studyInput.ReviewedWork! with { StudyHourlyTreatment = null } }).Status != "calculated",
    "No guessed hourly academic treatment.");
Check(AttendanceDailyTimeCalculation.Calculate(input with { ReviewedWork = studyInput.ReviewedWork }).Status != "calculated",
    "No academic excuse during accepted actual duty.");
var overtime = input with { Duty = [new(start, start.AddHours(10))], ApprovedOvertime = [new(start.AddHours(9), start.AddHours(10))],
    ReviewedWork = new([], [new(start.AddHours(9), start.AddHours(10))], [], null, null) };
Check(AttendanceDailyTimeCalculation.Calculate(overtime).Status != "calculated", "Same physical time cannot be reserved and approved OT.");
Check(AttendanceDailyTimeCalculation.Calculate(input with { IsHoliday = true, ReviewedWork = reviewed }).Status != "calculated",
    "No invented rest/holiday special credit interpretation.");
var resetInput = input with { Duty = [new(start, start.AddHours(4.5))],
    ReviewedWork = new([], [], [], null, null) { RequiredDutyTicks = (long)(4.5m * TimeSpan.TicksPerHour) } };
var reset = AttendanceDailyTimeCalculation.Calculate(resetInput);
Check(reset.RequiredDutyShortfallTicks == 0 && reset.PayableTicks == 4.5m * TimeSpan.TicksPerHour,
    "Approved reduced next-day requirement does not manufacture a full payable day.");
Check(AttendanceDailyTimeCalculation.Calculate(resetInput with { Duty = [new(start, start.AddHours(4))] }).RequiredDutyShortfallTicks == 30 * TimeSpan.TicksPerMinute,
    "Exact approved requirement shortfall.");
Check(AttendanceDailyTimeCalculation.Calculate(resetInput with { ReviewedWork = resetInput.ReviewedWork! with { RequiredDutyTicks = 0 } }).Status != "calculated",
    "Missing or invalid approved duty cannot become zero hours.");
Console.WriteLine($"C03 actual time/KPI consumer: {checks} checks passed. Synthetic approved inputs; server evidence/SQL acceptance separate.");
