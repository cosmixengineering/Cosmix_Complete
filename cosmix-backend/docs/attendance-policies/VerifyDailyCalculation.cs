#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;

// Synthetic in-memory evidence exercises real pure rules, snapshot producer and calculator.
// No test project, database, provider, private file, compensation mutation or real employee is used.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Conflict(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception("Expected " + code); }
var start = new DateTimeOffset(2026, 10, 12, 5, 0, 0, TimeSpan.Zero);
var rules = Fixture.Rules;
AttendanceTimeInput Input(params AttendanceTimeInterval[] duty) => new(rules, start, start.AddHours(9), 540, 60, false, false, duty, []);
AttendanceTimeResult Calc(AttendanceTimeInput input) => AttendanceDailyTimeCalculation.Calculate(input);
var full = Calc(Input(new AttendanceTimeInterval(start, start.AddHours(9))));
Check(full.Status == "calculated" && full.PayableTicks == TimeSpan.FromHours(9).Ticks && full.DayStatus == AttendanceDayStatus.Present, "Explicit count-all-breaks full day");
Check(Calc(Input(new AttendanceTimeInterval(start.AddMinutes(15), start.AddHours(9)))).CountsAsLateOccurrence == false, "Exact late threshold excluded");
Check(Calc(Input(new AttendanceTimeInterval(start.AddMinutes(15).AddTicks(1), start.AddHours(9)))).CountsAsLateOccurrence == true, "One tick after threshold included");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(1), start.AddHours(9)))).DayStatus == AttendanceDayStatus.Present, "Exact half threshold excluded");
var half = Calc(Input(new AttendanceTimeInterval(start.AddHours(1).AddTicks(1), start.AddHours(9))));
Check(half.DayStatus == AttendanceDayStatus.HalfDay && half.PayableTicks == TimeSpan.FromHours(4.5).Ticks, "Half-day uses fixed explicit credit");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(2.5), start.AddHours(9)))).DayStatus == AttendanceDayStatus.HalfDay, "Exact hourly threshold retains half day");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(2.5).AddTicks(1), start.AddHours(9)))).DayStatus == AttendanceDayStatus.Hourly, "Hourly begins one tick later");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddMinutes(20)))).PayableTicks == TimeSpan.FromHours(4.5).Ticks, "Early exit fixed credit is not a cap on worked time");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddMinutes(20))) with { ScheduledMinutes = 180 }).PayableTicks == TimeSpan.FromHours(3).Ticks, "Fixed credit capacity uses configured scheduled minutes, not wider clock window");
var actual = rules with { PaidTime = rules.PaidTime! with { Basis = AttendancePaidTimeBasis.ActualElapsedDuty, EarlyExitTreatment = AttendanceEarlyExitTreatment.ActualWorkedTime } };
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(-1), start.AddHours(10))) with { Rules = actual }).PayableTicks == TimeSpan.FromHours(10).Ticks, "Actual elapsed excludes early time but includes after-shift time");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(11))) with { Rules = actual, ApprovedOvertime = [new(start.AddHours(9), start.AddHours(11))] }).PayableTicks == TimeSpan.FromHours(9).Ticks, "Verified awarded OT excluded from ordinary actual time");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(11))) with { ApprovedOvertime = [new(start.AddHours(9), start.AddHours(11))] }).PayableTicks == TimeSpan.FromHours(9).Ticks, "Window basis does not deduct outside-window OT twice");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(11))) with { ApprovedOvertime = [new(start.AddHours(8), start.AddHours(10))] }).Status == "attendance_overtime_interval_conflict", "OT cannot overlap ordinary scheduled window");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(-1), start.AddHours(10)))).PayableTicks == TimeSpan.FromHours(9).Ticks, "Window basis excludes both outside boundaries");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(10), start.AddHours(11))) with { Rules = actual }).DayStatus == AttendanceDayStatus.Hourly, "Post-shift-only actual duty classified hourly");
var deduct = rules with { PaidTime = rules.PaidTime! with { BreakTreatment = AttendanceBreakTreatment.DeductScheduledUnpaidBreak, EarlyExitTreatment = AttendanceEarlyExitTreatment.ActualWorkedTime } };
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(4)), new(start.AddHours(5), start.AddHours(9))) with { Rules = deduct }).PayableTicks == TimeSpan.FromHours(7).Ticks, "Explicit daily break duration deducted once across sessions");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddSeconds(35)), new(start.AddMinutes(1), start.AddMinutes(1).AddSeconds(35))) with { Rules = actual }).PayableTicks == TimeSpan.FromSeconds(70).Ticks, "Sub-minute ticks summed before public rounding");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(2)), new(start.AddHours(1), start.AddHours(3)))).Status == "attendance_interval_conflict", "Overlapping duty refused");
var partial = Input(new AttendanceTimeInterval(start.AddHours(2), start.AddHours(5))) with { Leave = [new(new(start, start.AddHours(2)), true)] };
Check(Calc(partial).PayableTicks == TimeSpan.FromHours(4.5).Ticks, "Partial paid leave included in fixed half-day total");
Check(Calc(partial with { Leave = [new(new(start, start.AddHours(2)), false)] }).PayableTicks == TimeSpan.FromHours(4.5).Ticks, "Half-day remains within available day excluding unpaid leave");
var unpaid = Input(new AttendanceTimeInterval(start.AddHours(6), start.AddHours(7))) with { Leave = [new(new(start, start.AddHours(6)), false)] };
Check(Calc(unpaid).PayableTicks == TimeSpan.FromHours(3).Ticks, "Long unpaid leave caps fixed half day to available capacity");
var paid = Input(new AttendanceTimeInterval(start.AddHours(6), start.AddHours(7))) with { Leave = [new(new(start, start.AddHours(6)), true)] };
Check(Calc(paid).PayableTicks == TimeSpan.FromHours(6).Ticks, "Fixed credit never removes explicit paid-leave credit");
Check(Calc(partial with { Rules = rules with { SchemaVersion = 1, PaidTime = rules.PaidTime! with { PartialLeaveHalfDayTreatment = null } } }).Status == "partial_leave_half_day_interpretation_required", "Legacy schema cannot guess partial-leave composition");
Check(Calc(partial with { Leave = [new(new(start.AddHours(3), start.AddHours(4)), true)] }).Status == "leave_duty_overlap", "Duty cannot overlap leave");
Check(Calc(Input() with { Leave = [new(new(start, start.AddHours(9)), true)] }).DayStatus == AttendanceDayStatus.Leave, "Verified full leave classified explicitly");
Check(Calc(Input()).DayStatus == AttendanceDayStatus.Absent, "Complete sources support factual no-duty absence classification");
Check(Calc(Input() with { IsHoliday = null }).Status == "holiday_calendar_configuration_required", "Missing holiday context is not normal attendance");
Check(Calc(Input() with { IsRestDay = true }).DayStatus == AttendanceDayStatus.RestDay, "Explicit rest-day treatment");
Check(Calc(Input() with { IsHoliday = true }).DayStatus == AttendanceDayStatus.Holiday, "Explicit holiday treatment");
Check(Calc(Input(new AttendanceTimeInterval(start, start.AddHours(1))) with { IsRestDay = true, Rules = actual with { PaidTime = actual.PaidTime! with { OffDayTreatment = AttendanceNonScheduledDayTreatment.ActualWorkedTime } } }).Status == "rest_day_work_authorization_required", "Rest-day work does not invent early reporting authorization");
Check(Calc(Input(new AttendanceTimeInterval(start.AddHours(15), start.AddHours(24))) with { ShiftStartsAtUtc = start.AddHours(15), ShiftEndsAtUtc = start.AddHours(24) }).PayableTicks == TimeSpan.FromHours(9).Ticks, "Cross-midnight retains exact continuous interval");
Check(AttendanceDailyTimeCalculation.LatePeriod(new(2026, 6, 30), AttendanceLateResetPeriod.FiscalJulyJune) == (new DateOnly(2025, 7, 1), new DateOnly(2026, 7, 1)), "Fiscal late boundary");
Check(AttendanceDailyTimeCalculation.LatePeriod(new(2026, 7, 1), AttendanceLateResetPeriod.CalendarMonth) == (new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1)), "Monthly late reset");

var f = new Fixture();
var result = await f.Calculate();
Check(result.Status == "calculated" && result.PayableMinutes == 540 && result.OvertimeMinutes == 0 && result.PayrollReady == false, "Real producer plus calculator yields time only");
Check(result.InputProvenance is { Length: 64 } && result.PolicyVersionId == f.Policy.PolicyVersionId, "Actual policy version and snapshot digest retained");
Check(!JsonSerializer.Serialize(result).Contains("Salary") && !JsonSerializer.Serialize(result).Contains("Amount"), "Ordinary result contains no monetary values");
var repeat = await f.Calculate();
Check(result.InputProvenance == repeat.InputProvenance, "Identical source read has stable digest");
f.Policy = f.Policy with { Fingerprint = new string('D', 64) };
Check((await f.Calculate()).InputProvenance != result.InputProvenance, "Policy provenance change alters digest at equal visible time");
f.Policy = f.Policy with { State = "missing", PolicyVersionId = null, Rules = null, CanonicalJson = null };
Check((await f.Calculate()) is { Status: "policy_configuration_required", PayableMinutes: null, PayrollReady: false }, "Missing policy does not synthesize zero payable time");
f = new(); f.Leave = f.Leave with { Status = LeaveContextStatus.Pending };
Check((await f.Calculate()).Status == "leave_decision_pending", "Pending leave blocks daily calculation");
f = new(); f.Leave = f.Leave with { Status = LeaveContextStatus.Conflict };
Check((await f.Calculate()).Status == "leave_effect_conflict", "Conflicting leave blocks daily calculation");
f = new(); f.Roster = f.Roster with { Holiday = null }; f.Punches = f.Punches.Select(x => x with { HolidayCalendarId = null }).ToList();
Check((await f.Calculate()).Status == "holiday_calendar_configuration_required", "Actual producer carries unknown calendar to rule engine");
f = new(); f.ValidDuty = false;
await Conflict(async () => await f.Calculate(), "attendance_duty_evidence_conflict");
f = new(); f.Punches[0] = f.Punches[0] with { AssessmentOutcome = PunchAssessmentOutcome.AcceptedWithException };
Check((await f.Calculate()) is { Status: "attendance_exception_review_required", PayableMinutes: null, DayStatus: null }, "Unresolved exception cannot become present/payable ordinary duty");
f = new(); f.ValidEmployment = false;
Check((await f.Calculate()).Status == "attendance_historical_employment_unavailable", "Transfer/lifecycle uncertainty blocks calculation");
f = new(); f.CrossDateOverlap = true;
await Conflict(async () => await f.Calculate(), "attendance_cross_date_session_overlap");
f = new(); f.Punches[0] = f.Punches[0] with { EmployeeAssignmentId = Guid.NewGuid() };
await Conflict(async () => await f.Calculate(), "attendance_punch_context_conflict");
f = new(); f.Punches.RemoveAt(0);
await Conflict(async () => await f.Calculate(), "attendance_punch_source_conflict");
f = new(); f.Facts = f.Facts with { WorkedMinutes = 1 };
await Conflict(async () => await f.Calculate(), "attendance_factual_source_changed");
f = new(); f.Facts = f.Facts with { OpenSessions = 1 };
Check((await f.Calculate()).Status == "attendance_session_still_open", "Open duty never claims final policy time");
f = new(); f.Leave = f.Leave with { Effects = [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), f.Date, f.Start, f.Start.AddMinutes(10), 9, LeavePayTreatment.Paid, f.Roster.ShiftVersionId)] };
await Conflict(async () => await f.Calculate(), "attendance_leave_contract_invalid");
f = new(); f.Leave = f.Leave with { Effects = [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), f.Date, f.Start, f.Start.AddMinutes(10), 10, LeavePayTreatment.Paid, f.Roster.ShiftVersionId)] };
Check((await f.Calculate()).Status == "leave_duty_overlap", "Actual leave effects reach pure conflict validation");
f.Leave = f.Leave with { Effects = [f.Leave.Effects[0] with { ShiftVersionId = Guid.NewGuid() }] };
await Conflict(async () => await f.Calculate(), "attendance_leave_contract_invalid");
f = new(); f.NoSessions = true; f.Facts = AttendanceFacts.Calculate(f.Employee, f.Date, []); f.Punches.Clear();
Check((await f.Calculate()) is { Status: "calculated", DayStatus: AttendanceDayStatus.Absent, PayableMinutes: 0 }, "Verified complete empty sources allow absence");
f.Now = f.Start.AddHours(8);
Check((await f.Calculate()) is { Status: "attendance_day_not_complete", DayStatus: null, PayableMinutes: null }, "No absence before scheduled end");
f.Now = f.Start.AddDays(-1);
Check((await f.Calculate()).Status == "attendance_day_not_complete", "Future dates cannot become absence");
f.Now = f.Start.AddHours(20);
f.Policy = f.Policy with { State = "missing" };
Check((await f.Calculate()) is { PayableMinutes: null, DayStatus: null }, "Empty evidence plus missing policy never asserts absence or zero pay");
f = new(); await f.Manual();
Check((await f.Calculate()) is { Status: "calculated", PayableMinutes: 540 }, "Real snapshot producer verifies approved manual duty after failed raw Check In without fake face success");
Check(f.CorrectionRow!.AppliedSummaryRevisionId is null, "Initial correction summary has no circular saved-summary prerequisite");
f.ValidCorrectionDecision = false;
await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
f = new(); await f.Manual();
typeof(WorkSession).GetProperty("StartsAtUtc")!.SetValue(f.Session, f.Start.AddMinutes(1));
f.Facts = AttendanceFacts.Calculate(f.Employee, f.Date, [f.Session]);
await Conflict(async () => await f.Calculate(), "attendance_correction_application_conflict");
f = new(11); await f.Overtime(false);
Check((await f.Calculate()) is { Status: "overtime_decision_pending", PayableMinutes: null, PayrollReady: false }, "Actual retained pending OT is not silently zeroed");
f = new(11); f.UseActualElapsed(); await f.Overtime(true);
var awarded = await f.Calculate();
Check(awarded is { Status: "calculated", PayableMinutes: 540, OvertimeMinutes: 120, PayrollReady: false }, "Actual protected OT evidence verified and deduplicated from ordinary time");
Check(!JsonSerializer.Serialize(awarded).Contains("100000"), "Retained salary stays in private financial evidence");
f.AssessmentId = Guid.NewGuid(); f.Punches = f.Punches.Select(x => x.Action == PunchAction.CheckIn ? x with { AssessmentId = f.AssessmentId, AssessmentRevision = 2 } : x).ToList();
Check((await f.Calculate()).Status == "overtime_award_source_changed", "Later assessment provenance cannot silently reuse stale approved OT");
f = new(11); await f.Overtime(true); await f.Overtime(true);
await Conflict(async () => await f.Calculate(), "attendance_overtime_award_conflict");
f = new(11); await f.Overtime(true); f.CorruptOvertimeAmount();
await Conflict(async () => await f.Calculate(), "overtime_decision_evidence_unavailable");
f = new(); f.UseOffice();
Check((await f.Calculate()) is { Status: "calculated", PayableMinutes: 540, PayrollReady: false },
    "B01 RED: actual Office raw/session history must support ordinary time without a fabricated site");
var retainedOffice = await f.Snapshot();
Check(retainedOffice.Snapshot is { } officeSnapshot && officeSnapshot.OfficeSource == f.OfficeSource && officeSnapshot.Punches.All(x =>
    x.BranchId == f.OfficeSource!.BranchId && x.SiteId is null), "Office snapshot retains exact boundary/member/calendar and no fake Site");
foreach (var name in new[] { "BranchId", "OfficeRosterAssignmentId", "OfficeGeofenceVersionId", "EmployeeAssignmentId", "HolidayCalendarId" })
{
    f = new(); f.UseOffice();
    var punch = f.Punches[0]; typeof(AttendanceCalculationPunch).GetProperty(name)!.SetValue(punch, Guid.NewGuid());
    await Conflict(async () => await f.Calculate(), "attendance_punch_context_conflict");
}
f = new(); f.UseOffice(); f.OfficeStatus = "office_boundary_not_configured";
Check((await f.Calculate()) is { Status: "office_boundary_not_configured", PayableMinutes: null, PayrollReady: false },
    "missing Office boundary is unavailable rather than zero pay");
f = new(); f.UseOffice(); f.ValidEmployment = false;
Check((await f.Calculate()).Status == "attendance_historical_employment_unavailable", "Office historical membership/lifecycle uncertainty blocks calculation");
f = new(); f.UseOffice();
typeof(WorkSession).GetProperty("EndsAtUtc")!.SetValue(f.Session, f.Start.AddHours(22));
typeof(WorkSession).GetProperty("CalculatedAtUtc")!.SetValue(f.Session, f.Start.AddHours(22));
f.Punches = f.Punches.Select(x => x.Action == PunchAction.CheckOut ? x with { ReceivedAtUtc = f.Start.AddHours(22) } : x).ToList();
f.Facts = AttendanceFacts.Calculate(f.Employee, f.Date, [f.Session]); f.Now = f.Start.AddHours(30); f.MembershipEnds = f.Date.AddDays(1);
Check((await f.Calculate()).Status == "attendance_historical_employment_unavailable", "overnight Office transfer cannot reuse the prior membership");
f = new(); f.UseOffice(); await f.Manual();
Check((await f.Calculate()) is { Status: "calculated", PayableMinutes: 540 } &&
    f.Session.CheckInPunchId is null && f.Session.CheckOutPunchId is null && f.Punches.Count == 0,
    "schema-4 approved manual Office duty calculates without fabricated raw punches or face proof");
f.ValidCorrectionDecision = false;
await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
foreach (var schema in new[] { 1, 2, 3, 5 })
{
    f = new(); f.UseOffice(); await f.Manual(schema);
    await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
}
f = new(); f.UseOffice(); await f.Manual(wrongOffice: true);
await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
foreach (var name in new[] { "BranchId", "OfficeRosterAssignmentId", "EmployeeAssignmentId", "OfficeGeofenceVersionId", "HolidayCalendarId" })
{
    f = new(); f.UseOffice(); typeof(WorkSession).GetProperty(name)!.SetValue(f.Session, Guid.NewGuid());
    f.Facts = AttendanceFacts.Calculate(f.Employee, f.Date, [f.Session]);
    await Conflict(async () => await f.Calculate(), name == "OfficeGeofenceVersionId" ? "office_boundary_source_changed" : "attendance_office_source_conflict");
}
f = new(11); f.UseOffice(); await f.Overtime(false);
await Conflict(async () => await f.Calculate(), "attendance_office_overtime_ineligible");
foreach (var office in new[] { false, true })
foreach (var kind in Enum.GetValues<CorrectionAlternativeKind>())
{
    f = new(); if (office) f.UseOffice(); await f.Manual(alternativeKind: kind);
    var sourceType = typeof(AttendanceCorrectionService).Assembly.GetType("Cosmix.Application.Attendance.Corrections.CorrectionSourceEvidence")!;
    Check((bool)sourceType.GetMethod("Matches")!.Invoke(null, [f.LastCorrectionEvidence])!, "actual typed Alternative intake evidence is valid before calculation");
    Check((await f.Calculate()) is { Status: "calculated", PayableMinutes: 540, PayrollReady: false },
        "B03 RED: independently approved Admin Alternative duty must not be rejected by ordinary Site/Office schema selection");
    Check(f.Session.CheckInPunchId is null && f.Session.CheckOutPunchId is null && f.Punches.Count == 0,
        "Alternative duty has no fabricated raw punch or face success");
    Check(f.AlternativeRevalidations == 1 && f.ApprovalReads == 1, "staged application calls the typed approval hook and direct Alternative reader once");
}
f = new(); await f.Manual(); f.IncludeAlternativeReader = false;
Check((await f.Calculate()).Status == "calculated" && f.ApprovalReads == 0 && f.AlternativeRevalidations == 0,
    "ordinary HR manual duty does not acquire new Alternative dependencies or authority");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); f.ApprovalProof = null;
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); f.IncludeAlternativeReader = false;
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); f.AlternativeAvailable = false;
await Conflict(async () => await f.Calculate(), "alternative_evidence_version_conflict");
foreach (var mutate in new Func<AttendanceAlternativeCorrectionApprovalProof, AttendanceAlternativeCorrectionApprovalProof>[] {
    p => p with { SystemRole = SystemRoleKind.None }, p => p with { SystemRole = SystemRoleKind.SuperAdmin },
    p => p with { RoleAssignmentId = Guid.NewGuid() }, p => p with { Actor = p.Actor with { AccountId = Guid.NewGuid() } },
    p => p with { Actor = p.Actor with { MfaSatisfied = false } }, p => p with { Actor = p.Actor with { Client = SessionClient.Mobile } },
    p => p with { Actor = p.Actor with { SessionId = Guid.Empty } },
    p => p with { Permissions = [p.Permissions[0] with { Permission = CorrectionPermissions.Decide }] },
    p => p with { Permissions = [p.Permissions[0] with { RoleAssignmentId = Guid.NewGuid() }] },
    p => p with { Permissions = [p.Permissions[0] with { TargetId = Guid.NewGuid() }] },
    p => p with { Permissions = [p.Permissions[0] with { AuthorityTimingKnown = false }] },
    p => p with { Permissions = [p.Permissions[0] with { ValidFromUtc = p.AuthorityAtUtc.AddTicks(1) }] },
    p => p with { Permissions = [p.Permissions[0] with { ValidUntilUtc = p.AuthorityAtUtc }] },
    p => p with { IsAuthorizedStagedApplication = false, AuthorityAtUtc = p.Decision.DecidedAt, Audit = null }
})
{
    f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Accessibility); f.ApprovalProof = mutate(f.ApprovalProof!);
    await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
}
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.OutOfBoundary);
typeof(ApprovalDecision).GetProperty("ActorRoleCode")!.SetValue(f.CorrectionDecision, "Configured-HR");
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical);
typeof(ApprovalDecision).GetProperty("ActorUserId")!.SetValue(f.CorrectionDecision, f.CorrectionRow!.Approval.RequesterUserId);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical);
f.ApprovalProof = f.ApprovalProof! with { Permissions = [f.ApprovalProof.Permissions[0] with { ValidUntilUtc = f.Now.AddSeconds(30) }] };
f.ApprovalReadAdvance = TimeSpan.FromMinutes(1);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical);
f.ApprovalProof = f.ApprovalProof! with { Permissions = [f.ApprovalProof.Permissions[0] with { ValidUntilUtc = f.Now.AddSeconds(30) }] };
f.AlternativeReadAdvance = TimeSpan.FromMinutes(1);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); f.ApprovalReadAdvance = TimeSpan.FromTicks(-1);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); f.UseOffice(); await f.Manual(alternativeKind: CorrectionAlternativeKind.OutOfBoundary);
var stagedAlternative = await f.Snapshot();
await f.CommitAlternative(); f.AlternativeAvailable = false; f.IncludeAlternativeReader = false; f.Now = f.Now.AddMonths(1);
var retainedAlternative = await f.Snapshot();
Check(retainedAlternative.State == "complete" && retainedAlternative.Fingerprint == stagedAlternative.Fingerprint && f.AlternativeRevalidations == 1,
    "committed Office Alternative retains approved policy/docs/permission evidence without re-resolving later states or changing its hash");
Check(!JsonSerializer.Serialize(retainedAlternative.Snapshot).Contains("synthetic-scanner", StringComparison.Ordinal),
    "daily calculation exposes only Alternative provenance digest, not private medical/document metadata");
foreach (var office in new[] { false, true })
foreach (var kind in Enum.GetValues<CorrectionAlternativeKind>())
{
    f = new(); if (office) f.UseOffice(); await f.Manual(alternativeKind: kind);
    var authorityEndsAt = f.Now.AddSeconds(30);
    f.ApprovalProof = f.ApprovalProof! with { Permissions = [f.ApprovalProof.Permissions[0] with { ValidUntilUtc = authorityEndsAt }] };
    var staged = await f.Snapshot();
    Check(staged.State == "complete" && f.AlternativeRevalidations == 1,
        $"{office}/{kind}: staged schema5 validates evidence while dedicated Admin authority is effective");
    await f.CommitAlternative(); f.Now = authorityEndsAt.AddTicks(1);
    f.AlternativeAvailable = false; f.IncludeAlternativeReader = false;
    var historical = await f.Snapshot();
    Check(historical.State == "complete" && historical.Fingerprint == staged.Fingerprint &&
        !f.ApprovalProof!.Permissions[0].IsEffectiveAt(f.Now) && f.ApprovalProof.Permissions[0].IsEffectiveAt(f.ApprovalProof.Decision.DecidedAt),
        $"{office}/{kind}: post-approval authority revocation preserves exact retained historical schema5 fingerprint");
    Check((await f.Calculate()) is { Status: "calculated", PayableMinutes: 540, PayrollReady: false } && f.AlternativeRevalidations == 1,
        $"{office}/{kind}: historical calculation does not recapture later unavailable/revoked policy or documents");
    foreach (var revokedBeforeOrAtApproval in new[] { -1L, 0L })
    {
        f = new(); if (office) f.UseOffice(); await f.Manual(alternativeKind: kind); await f.CommitAlternative();
        f.ApprovalProof = f.ApprovalProof! with { Permissions = [f.ApprovalProof.Permissions[0] with {
            ValidUntilUtc = f.ApprovalProof.Decision.DecidedAt.AddTicks(revokedBeforeOrAtApproval) }] };
        await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
    }
}
foreach (var name in new[] { "ActorRoleCode", "ActorUserId", "TargetId", "EventType" })
{
    f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); await f.CommitAlternative();
    typeof(AuditEvent).GetProperty(name)!.SetValue(f.ApprovalProof!.Audit, "not-the-retained-Admin-decision");
    await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
}
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); await f.CommitAlternative(CorrectionAlternativeKind.Accessibility);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); await f.CommitAlternative(nullAfter: true);
await Conflict(async () => await f.Calculate(), "attendance_alternative_approval_unavailable");
foreach (var mutate in new Func<CorrectionEvidenceSnapshot, CorrectionEvidenceSnapshot>[] {
    s => s with { Proposal = s.Proposal with { AlternativeKind = CorrectionAlternativeKind.OutOfBoundary } },
    s => s with { Proposal = s.Proposal with { ExpectedAlternativePolicyId = Guid.NewGuid() } },
    s => s with { Proposal = s.Proposal with { AlternativeEvidenceDocumentIds = [Guid.NewGuid()] } },
    s => s with { AlternativeEvidence = s.AlternativeEvidence! with { Policy = s.AlternativeEvidence.Policy with { Fingerprint = new('F', 64) } } },
    s => s with { AlternativeEvidence = s.AlternativeEvidence! with { Documents = [s.AlternativeEvidence.Documents[0] with { EmployeeId = Guid.NewGuid() }] } },
    s => s with { SchemaVersion = s.Source.OfficeSource is null ? 3 : 4 }
})
{
    f = new(); await f.Manual(alternativeKind: CorrectionAlternativeKind.Medical); f.ReplaceCorrectionEvidence(mutate);
    await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
}
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", true);
Check((await f.Calculate()).Status == "calculated", "Approved Office taxonomy supports ordinary calculation with original category retained");
var broaderOffice = await f.Snapshot();
Check(broaderOffice.Snapshot!.Employment.StaffCategory == "SUPPORT" && broaderOffice.Snapshot.Roster.Taxonomy!.PolicyVersionId == f.Taxonomy!.PolicyVersionId,
    "Calculation freezes exact published taxonomy and employment code");
var broaderFingerprint = broaderOffice.Fingerprint;
f.Taxonomy = f.Taxonomy! with { PolicyVersionId = Guid.NewGuid(), Fingerprint = new('E', 64) };
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f.Roster = f.Roster with { Taxonomy = RosterStaffTaxonomy.Capture(f.Taxonomy) };
Check((await f.Snapshot()).Fingerprint != broaderFingerprint, "Changed published taxonomy changes calculation provenance");
f.IncludeTaxonomyReader = false;
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", true); f.Roster = f.Roster with { Taxonomy = null };
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseTaxonomy("FIELD_ENGINEER", false);
Check((await f.Calculate()).Status == "calculated", "Approved Site taxonomy supports ordinary factual duty");
f.Roster = f.Roster with { Taxonomy = f.Roster.Taxonomy! with { StaffCategoryCode = "OTHER" } };
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", true); f.Taxonomy = f.Taxonomy! with { State = "unavailable", Mapping = null };
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", false);
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", true); await f.Manual();
f.TaxonomyAfterPrivateRead = f.Taxonomy! with { PolicyVersionId = Guid.NewGuid(), Fingerprint = new('E', 64) };
await Conflict(async () => await f.Calculate(), "attendance_taxonomy_source_changed");
f = new(); f.UseOffice(); f.UseTaxonomy("SUPPORT", true); await f.Manual();
f.Taxonomy = f.Taxonomy! with { PolicyVersionId = Guid.NewGuid(), Fingerprint = new('E', 64) };
f.Roster = f.Roster with { Taxonomy = RosterStaffTaxonomy.Capture(f.Taxonomy) };
await Conflict(async () => await f.Calculate(), "attendance_correction_source_conflict");
Console.WriteLine($"PASS: {checks} daily rule/service checks; synthetic in-memory sources, no SQL or financial acceptance.");

sealed class Fixture : IAttendancePolicyResolver, ILeaveAttendanceContextReader, IAttendanceSummaryRepository,
    IOvertimeRepository, IAttendanceCalculationRepository, ICorrectionReviewRepository, IPrivateFileStore, IOfficeAttendanceSourceReader,
    ICorrectionAlternativeEvidenceReader, IAttendanceAlternativeCorrectionApprovalReader, IStaffTaxonomyResolver, ILeaveRecoveryCreditReader
{
    public Task<IReadOnlyList<Cosmix.Application.Attendance.WorkTreatments.WorkTreatmentCreditEvidence>> ReadReservedAsync(Guid employeeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Cosmix.Application.Attendance.WorkTreatments.WorkTreatmentCreditEvidence>>([]);
    public static AttendancePolicyRules Rules => new(2, new(900, 3600, 9000),
        new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks, AttendanceEarlyExitTreatment.HalfDayCredit,
            16200, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit,
            AttendancePartialLeaveHalfDayTreatment.HalfDayTotalIncludesPaidLeave),
        new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
            AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
    public readonly Guid Employee = Guid.NewGuid();
    public readonly DateOnly Date = new(2026, 10, 12);
    public readonly DateTimeOffset Start = new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero);
    public EffectiveRosterResponse Roster;
    public OfficeAttendanceSource? OfficeSource;
    public OfficeBoundaryVersionResponse? OfficeBoundary;
    public string OfficeStatus = "configured";
    public string? StaffCategoryCode;
    public StaffTaxonomyResolution? Taxonomy;
    public StaffTaxonomyResolution? TaxonomyAfterPrivateRead;
    public bool IncludeTaxonomyReader;
    public void UseTaxonomy(string code, bool office)
    {
        StaffCategoryCode = code; IncludeTaxonomyReader = true;
        Taxonomy = new("effective", new(code, code, office ? StaffWorkLocation.Office : StaffWorkLocation.Site,
            office ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, false, !office, []), Guid.NewGuid(), new('D', 64),
            EffectiveFrom: Date.AddDays(-1));
        Roster = Roster with { Taxonomy = RosterStaffTaxonomy.Capture(Taxonomy) };
    }
    Task<StaffTaxonomyResolution> IStaffTaxonomyResolver.ResolveAsync(string code, DateOnly date, CancellationToken ct) => Task.FromResult(Taxonomy!);
    Task<StaffTaxonomyResolution> IStaffTaxonomyResolver.ResolveForRangeAsync(string code, DateOnly from, DateOnly to, CancellationToken ct) => Task.FromResult(Taxonomy!);
    public DateOnly? MembershipEnds;
    public AttendancePolicyResolution Policy = new("effective", Guid.NewGuid(), [Guid.NewGuid()], Rules, AttendancePolicyRulesCodec.Canonicalize(Rules), new string('A', 64));
    public LeaveAttendanceContext Leave = new(LeaveContextStatus.Complete, [], new string('B', 64), []);
    public FactualAttendance Facts;
    public WorkSession Session;
    public List<AttendanceCalculationPunch> Punches = [];
    public bool ValidDuty = true, ValidEmployment = true, CrossDateOverlap;
    public bool NoSessions, ValidCorrectionDecision = true;
    public Guid AssessmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public CorrectionRecord? CorrectionRow;
    public ApprovalDecision? CorrectionDecision;
    public CorrectionEvidenceSnapshot? LastCorrectionEvidence;
    public CorrectionAlternativeEvidence? AlternativeEvidence;
    public AttendanceAlternativeCorrectionApprovalProof? ApprovalProof;
    public int AlternativeRevalidations, ApprovalReads;
    public bool AlternativeAvailable = true, IncludeAlternativeReader = true;
    public TimeSpan ApprovalReadAdvance, AlternativeReadAdvance;
    public List<OvertimeRecord> OvertimeRows = [];
    readonly Dictionary<string, (string Purpose, byte[] Content)> files = [];
    public DateTimeOffset Now = new(2026, 10, 13, 1, 0, 0, TimeSpan.Zero);
    public Fixture(int hours = 9)
    {
        var site = Guid.NewGuid(); var shift = Guid.NewGuid(); var rosterId = Guid.NewGuid(); var member = Guid.NewGuid(); var calendar = Guid.NewGuid();
        Roster = new(Employee, Date, "scheduled", RosterSchedule.TimeZoneId, site, rosterId, Guid.NewGuid(), shift, 1,
            Start, Start.AddHours(9), 540, 0, 60, new("site", rosterId, member, null, null, "synthetic"),
            new("configured", Date, calendar, "SYNTHETIC", 1, []));
        RawPunchEvent Punch(PunchAction action, DateTimeOffset at) => new(Employee, action, "synthetic", Guid.NewGuid().ToString("N"),
            new string('C', 64), at, false, siteId: site, employeeAssignmentId: member, shiftVersionId: shift,
            businessDate: Date, siteRosterAssignmentId: rosterId, holidayCalendarId: calendar);
        var first = Punch(PunchAction.CheckIn, Start); var last = Punch(PunchAction.CheckOut, Start.AddHours(hours));
        Session = new(Guid.NewGuid(), 1, Employee, Date, Start, Start.AddHours(hours), shift, null, Start.AddHours(hours), first, last, siteId: site);
        Punches = new[] { first, last }.Select(x => new AttendanceCalculationPunch(x.Id, x.EmployeeId, x.Action, x.BusinessDate,
            x.ReceivedAtUtc, x.SiteId, x.EmployeeAssignmentId, x.ShiftVersionId, x.SiteRosterAssignmentId, x.TemporaryShiftChangeId, x.HolidayCalendarId,
            x.Action == PunchAction.CheckIn ? AssessmentId : Guid.Parse("22222222-2222-2222-2222-222222222222"), 1, PunchAssessmentOutcome.Accepted, 1)).ToList();
        Facts = AttendanceFacts.Calculate(Employee, Date, [Session]);
    }
    AttendanceOperation Operation => new(null!, null!, null!, null!, null!, TimeProvider.System, this);
    AttendanceCalculationSnapshotProducer Producer()
    {
        return new(this, this, this, this, this, this,
            new OvertimeService(Operation, this, null!, this, this, this, null!, null!, null!, recoveryCredits: this),
            Operation, new FixtureClock(this), this, IncludeAlternativeReader ? this : null,
            IncludeTaxonomyReader ? this : null, reviewedWork: null);
    }
    public Task<AttendanceCalculationSnapshotResult> Snapshot() => Producer().ReadAsync(Employee, Date, Facts, Roster, default);
    public Task<AttendancePolicyResult> Calculate() => new ConfiguredAttendancePolicyCalculator(Producer()).CalculateAsync(Employee, Date, Facts, Roster, default);
    public Task<OfficeAttendanceContext> ReadAsync(Guid e, DateOnly d, EffectiveRosterResponse roster, Guid? pinned, CancellationToken ct)
    {
        if (pinned.HasValue && pinned != OfficeSource?.OfficeGeofenceVersionId) throw AttendanceOperation.Conflict("office_boundary_source_changed");
        return Task.FromResult(new OfficeAttendanceContext(OfficeStatus, OfficeStatus == "configured" ? OfficeSource : null,
            OfficeStatus == "configured" ? OfficeBoundary : null));
    }
    public Task<bool> CurrentMembershipMatchesAsync(Guid e, Guid b, Guid? m, DateOnly d, CancellationToken ct) =>
        Task.FromResult(ValidEmployment && (!MembershipEnds.HasValue || d < MembershipEnds));
    public Task<AttendancePolicyResolution> ResolveAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult(Policy);
    public Task<LeaveAttendanceContext> ReadAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult(Leave);
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkSession>>(NoSessions ? [] : [Session]);
    public Task<OvertimeEmploymentSource?> EmploymentAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult<OvertimeEmploymentSource?>(new(Roster.Explanation!.MembershipRecordId!.Value, Guid.Parse("55555555-5555-5555-5555-555555555555"), StaffCategoryCode ?? (Roster.BranchId.HasValue ? "OFFICE" : "SITE"), Roster.SiteId, Date.AddMonths(-1), null, Roster.BranchId));
    public Task<IReadOnlyList<OvertimeSessionSource>> SessionSourcesAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult<IReadOnlyList<OvertimeSessionSource>>(NoSessions ? [] : [
        new(Session.Id, Session.SessionKey, Session.Revision, Session.StartsAtUtc, Session.EndsAtUtc, Session.SiteId, Session.ShiftVersionId,
            null, Session.CheckInPunchId is null ? null : AssessmentId, Session.CheckOutPunchId is null ? null : Guid.Parse("22222222-2222-2222-2222-222222222222"), CorrectionDecision?.Id, ValidDuty, Session.CorrectionId,
            Session.BranchId, Session.OfficeRosterAssignmentId, Session.BranchId.HasValue ? Session.EmployeeAssignmentId : null,
            Session.OfficeGeofenceVersionId, Session.BranchId.HasValue ? Session.HolidayCalendarId : null)]);
    public Task<IReadOnlyList<AttendanceCalculationPunch>> PunchesAsync(Guid e, IReadOnlyList<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<AttendanceCalculationPunch>>(Punches);
    public Task<IReadOnlyList<AttendanceCalculationOvertimeReference>> OvertimeAsync(Guid e, DateOnly d, CancellationToken ct) => Task.FromResult<IReadOnlyList<AttendanceCalculationOvertimeReference>>(OvertimeRows.Select(x => new AttendanceCalculationOvertimeReference(x.Request.Id, x.Decision is null ? 0 : 1)).ToArray());
    public Task<bool> HistoricallyEligibleAsync(Guid e, DateTimeOffset s, DateTimeOffset n, CancellationToken ct) => Task.FromResult(ValidEmployment);
    public Task<bool> PreservesAssignmentAsync(Guid e, Guid? a, Guid? site, DateTimeOffset s, DateTimeOffset n, CancellationToken ct) => Task.FromResult(ValidEmployment);
    public Task<bool> OverlapsAsync(Guid e, Guid key, DateTimeOffset s, DateTimeOffset n, CancellationToken ct) => Task.FromResult(CrossDateOverlap);
    public Task<bool> CorrectionDecisionMatchesAsync(Guid d, Guid a, string r, CancellationToken ct) => Task.FromResult(ValidCorrectionDecision && CorrectionDecision?.Id == d && CorrectionDecision.ApprovalRequestId == a && CorrectionDecision.ProtectedChangeReference == r);
    public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid e, DateOnly f, DateOnly t, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId: e, SiteId: Roster.SiteId)]);
    public Task<int> ExceptionCountAsync(Guid e, DateOnly d, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> IsLockedAsync(DateOnly d, CancellationToken ct) => throw new NotSupportedException();
    public Task<DailyAttendanceSummary?> LatestAsync(Guid e, DateOnly d, CancellationToken ct) => throw new NotSupportedException();
    public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid e, DateOnly d, int p, int s, CancellationToken ct) => throw new NotSupportedException();
    public void Add(DailyAttendanceSummary s, IReadOnlyList<AttendanceSummarySession> l) => throw new NotSupportedException();
    public Task<bool> ApprovedPolicyAsync(Guid id, DateOnly d, CancellationToken ct) => throw new NotSupportedException();
    Task<OvertimeRecord?> IOvertimeRepository.FindAsync(Guid e, Guid id, CancellationToken ct) => Task.FromResult(OvertimeRows.SingleOrDefault(x => x.Request.Id == id && x.Request.EmployeeId == e));
    public Task<IReadOnlyList<OvertimeRecord>> CandidatesAsync(OvertimeQueueRequest r, AttendanceCursorPosition? a, int l, Guid? u, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasConflictingRequestAsync(Guid e, DateTimeOffset s, DateTimeOffset n, Guid? id, CancellationToken ct) => throw new NotSupportedException();
    public void Add(OvertimeRequest r, ApprovalRequest a) => throw new NotSupportedException();
    public void Add(ApprovalDecision d) => throw new NotSupportedException();
    public void ExpectVersion(ApprovalRequest a, byte[] v) => throw new NotSupportedException();
    public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    Task<CorrectionRecord?> ICorrectionReviewRepository.FindAsync(Guid e, Guid id, CancellationToken ct) => Task.FromResult(CorrectionRow);
    public Task<IReadOnlyList<CorrectionRecord>> CandidatesAsync(CorrectionQueueRequest r, AttendanceCursorPosition? a, int l, CancellationToken ct, Guid? u = null) => throw new NotSupportedException();
    public Task<bool> ScopeExistsAsync(CorrectionQueueScope s, Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<RawPunchEvent?> PunchAsync(Guid e, Guid id, CancellationToken ct) => throw new NotSupportedException();
    public void Add(ApprovalDecision d, WorkSession? s) => throw new NotSupportedException();
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); files[key] = (purpose, content);
        return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        var item = files[reference.StorageKey];
        if (item.Purpose != purpose) throw new Exception("Private purpose mismatch");
        if (TaxonomyAfterPrivateRead is not null) { Taxonomy = TaxonomyAfterPrivateRead; TaxonomyAfterPrivateRead = null; }
        return Task.FromResult(item.Content);
    }
    public Task<CorrectionAlternativeEvidence> CaptureAsync(Guid employee, DateOnly date, CorrectionAlternativeKind kind,
        Guid policyId, IReadOnlyList<Guid>? ids, DateTimeOffset now, CancellationToken ct) => throw new Exception("Calculation must not recapture Alternative evidence");
    public Task RevalidateAsync(Guid employee, DateOnly date, CorrectionAlternativeEvidence evidence, DateTimeOffset now, CancellationToken ct)
    {
        AlternativeRevalidations++; Now += AlternativeReadAdvance;
        if (!AlternativeAvailable || employee != Employee || date != Date || now > Now ||
            JsonSerializer.Serialize(evidence) != JsonSerializer.Serialize(AlternativeEvidence))
            throw AttendanceOperation.Conflict("alternative_evidence_version_conflict");
        return Task.CompletedTask;
    }
    public Task<AttendanceAlternativeCorrectionApprovalProof?> ReadAsync(Guid employee, Guid correction, Guid decision, CancellationToken ct)
    {
        ApprovalReads++; Now += ApprovalReadAdvance;
        if (employee != Employee || correction != CorrectionRow?.Correction.Id || decision != CorrectionDecision?.Id)
            throw new Exception("Approval hook must receive exact identity");
        return Task.FromResult(ApprovalProof is { IsAuthorizedStagedApplication: true } proof ? proof with { AuthorityAtUtc = Now } : ApprovalProof);
    }
    public void ReplaceCorrectionEvidence(Func<CorrectionEvidenceSnapshot, CorrectionEvidenceSnapshot> change)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(CorrectionRow!.Correction.PrivateEvidenceReference)!;
        var item = files[reference.StorageKey]; LastCorrectionEvidence = change(LastCorrectionEvidence!);
        files[reference.StorageKey] = (item.Purpose, JsonSerializer.SerializeToUtf8Bytes(LastCorrectionEvidence));
    }
    public async Task CommitAlternative(CorrectionAlternativeKind? auditKind = null, bool nullAfter = false)
    {
        var row = CorrectionRow!; var decision = CorrectionDecision!; var c = row.Correction;
        var response = new CorrectionResponse(c.Id, Employee, Date, "ManualSession", c.RawPunchEventId, c.OriginalSummaryId,
            c.OriginalTimeUtc, c.RequestedTimeUtc, c.RequestedEndUtc, null, c.Reason, c.RequestedAtUtc,
            "Approved", "admin_alternative", Convert.ToBase64String(new byte[8]), true, Session.Id,
            AlternativeKind: auditKind ?? LastCorrectionEvidence!.Proposal.AlternativeKind);
        var purpose = $"attendance:operation:{decision.ActorUserId.Replace("-", "")}:attendance.correction.decide:audit";
        var reference = AttendanceOperation.Reference(await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new {
            operation = "attendance.correction.decide", ResourceId = c.Id,
            After = nullAfter ? null : new CorrectionDecisionResponse(response, decision.Id, Session.Id, null)
        }), purpose, default));
        var audit = new AuditEvent("attendance.correction.decide", decision.ActorUserId, "ADMIN", "Attendance", c.Id.ToString(),
            decision.DecidedAt, "synthetic-committed-alternative", Employee, reference);
        ApprovalProof = ApprovalProof! with { IsAuthorizedStagedApplication = false, AuthorityAtUtc = decision.DecidedAt, Audit = audit };
    }
    public void UseActualElapsed()
    {
        var rules = Rules with { PaidTime = Rules.PaidTime! with { Basis = AttendancePaidTimeBasis.ActualElapsedDuty } };
        Policy = Policy with { Rules = rules, CanonicalJson = AttendancePolicyRulesCodec.Canonicalize(rules) };
    }
    public void UseOffice()
    {
        var branch = Guid.NewGuid(); var officeRoster = Roster.AssignmentId!.Value; var member = Roster.Explanation!.MembershipRecordId!.Value;
        Roster = Roster with { SiteId = null, BranchId = branch,
            Explanation = new("office", null, member, null, null, "synthetic") { OfficeRosterAssignmentId = officeRoster } };
        OfficeBoundary = new(Guid.NewGuid(), branch, 1, Date.AddDays(-1), 24m, 67m, 100m, 20m,
            "PK-PB", "synthetic-approved-boundary", "synthetic", Start.AddDays(-1));
        OfficeSource = OfficeAttendanceSource.Capture(Employee, Date, Roster, OfficeBoundary);
        RawPunchEvent Punch(PunchAction action, DateTimeOffset at) => new(Employee, action, "synthetic", Guid.NewGuid().ToString("N"),
            new string('C', 64), at, false, employeeAssignmentId: member, shiftVersionId: Roster.ShiftVersionId,
            businessDate: Date, holidayCalendarId: Roster.Holiday!.CalendarId, branchId: branch,
            officeRosterAssignmentId: officeRoster, officeGeofenceVersionId: OfficeSource!.OfficeGeofenceVersionId);
        var first = Punch(PunchAction.CheckIn, Start); var last = Punch(PunchAction.CheckOut, Start.AddHours(9));
        Session = new(Session.SessionKey, 1, Employee, Date, Start, Start.AddHours(9), Roster.ShiftVersionId!.Value, null,
            Start.AddHours(9), first, last, branchId: branch, officeRosterAssignmentId: officeRoster, employeeAssignmentId: member,
            officeGeofenceVersionId: OfficeSource!.OfficeGeofenceVersionId, holidayCalendarId: OfficeSource.HolidayCalendarId);
        Punches = new[] { first, last }.Select(x => new AttendanceCalculationPunch(x.Id, Employee, x.Action, Date, x.ReceivedAtUtc,
            null, member, x.ShiftVersionId, null, null, x.HolidayCalendarId,
            x.Action == PunchAction.CheckIn ? AssessmentId : Guid.Parse("22222222-2222-2222-2222-222222222222"),
            1, PunchAssessmentOutcome.Accepted, 1, branch, officeRoster, x.OfficeGeofenceVersionId)).ToList();
        Facts = AttendanceFacts.Calculate(Employee, Date, [Session]);
    }
    public async Task Manual(int? schema = null, bool wrongOffice = false, CorrectionAlternativeKind? alternativeKind = null)
    {
        var failed = new RawPunchEvent(Employee, PunchAction.CheckIn, "synthetic", "failed-punch", new string('E', 64), Start, false,
            siteId: Roster.SiteId, employeeAssignmentId: Roster.Explanation!.MembershipRecordId, shiftVersionId: Roster.ShiftVersionId,
            businessDate: Date, siteRosterAssignmentId: OfficeSource is null ? Roster.AssignmentId : null, holidayCalendarId: Roster.Holiday!.CalendarId,
            branchId: Roster.BranchId, officeRosterAssignmentId: OfficeSource?.OfficeRosterAssignmentId,
            officeGeofenceVersionId: OfficeSource?.OfficeGeofenceVersionId);
        var assessment = new PunchAssessment(failed, 1, PunchAssessmentOutcome.Rejected, "FACE_FAILED", null, "synthetic", Start);
        var captureType = typeof(AttendanceCorrectionService).Assembly.GetType("Cosmix.Application.Attendance.Corrections.CorrectionSourceEvidence")!;
        var captured = ((CorrectionSourceResponse Source, IReadOnlyList<AttendanceResourceScope> Scopes))captureType.GetMethod("Capture")!
            .Invoke(null, [Employee, Date, new CorrectionSourceState(failed, assessment, null, false, [], []), Roster,
                new AttendanceResourceScope[] { new([new(EmployeeId: Employee, SiteId: Roster.SiteId)]) }, OfficeSource])!;
        var requester = Guid.NewGuid(); var submission = Guid.NewGuid(); var now = Start.AddHours(10);
        if (alternativeKind.HasValue)
        {
            var policy = new CorrectionAlternativePolicy(Guid.NewGuid(), alternativeKind.Value, 1, Date.AddDays(-1), true,
                [EmployeeDocumentType.Other], "synthetic-management-policy", Guid.NewGuid().ToString(), Start.AddDays(-1), "");
            policy = policy with { Fingerprint = CorrectionAlternativePolicyService.PolicyFingerprint(policy) };
            AlternativeEvidence = new(policy, [new(Guid.NewGuid(), Employee, EmployeeDocumentType.Other, new('A', 64),
                Convert.ToBase64String(new byte[8]), "synthetic-scanner", Start.AddDays(-1), Start.AddHours(-1), "synthetic-verifier")]);
        }
        var proposal = new CreateCorrectionRequest(Date, AttendanceCorrectionKind.ManualSession, failed.Id, assessment.Id, null,
            Start, Start.AddHours(9), null, "Verified manual duty", "Synthetic supervisor evidence", captured.Source.SourceFingerprint,
            AlternativeKind: alternativeKind, ExpectedAlternativePolicyId: AlternativeEvidence?.Policy.Id,
            AlternativeEvidenceDocumentIds: AlternativeEvidence?.Documents.Select(x => x.Id).ToArray());
        var protectedSource = wrongOffice ? captured.Source with { OfficeSource = OfficeSource! with { BranchId = Guid.NewGuid() } } : captured.Source;
        var snapshot = new CorrectionEvidenceSnapshot(schema ?? (alternativeKind.HasValue ? 5 : OfficeSource is null ? 3 : 4),
            submission, Employee, requester, proposal, protectedSource, captured.Scopes, AlternativeEvidence);
        LastCorrectionEvidence = snapshot;
        var reference = AttendanceOperation.Reference(await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), $"attendance:correction:{Employee:N}:proposal", default));
        var approval = new ApprovalRequest("attendance.correction", submission, requester.ToString(), reference, 1, now.AddMinutes(-1));
        var correction = new AttendanceCorrection(Employee, Date, AttendanceCorrectionKind.ManualSession, approval.Id, requester.ToString(), approval.RequestedAt,
            proposal.Reason, reference, originalTimeUtc: failed.ReceivedAtUtc, requestedTimeUtc: Start, requestedEndUtc: Start.AddHours(9), rawPunchId: failed.Id);
        CorrectionDecision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), alternativeKind.HasValue ? "ADMIN" : "Configured-HR", "Confirmed", now, reference);
        CorrectionRow = new(correction, approval);
        if (alternativeKind.HasValue)
        {
            var roleAssignment = Guid.NewGuid();
            var actor = new AuthenticatedAccount(Guid.Parse(CorrectionDecision.ActorUserId), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            PermissionGrant grant = new(CorrectionPermissions.AlternativesDecide, true, roleAssignment, AccessScopeKind.Employee, Employee)
                { AuthorityTimingKnown = true, ValidFromUtc = Start.AddDays(-1) };
            ApprovalProof = new(CorrectionDecision, actor, SystemRoleKind.Admin, roleAssignment, [grant], captured.Scopes, Now, true, null);
        }
        Session = new(Guid.NewGuid(), 1, Employee, Date, Start, Start.AddHours(9), Roster.ShiftVersionId!.Value, null, now, null,
            correctionId: correction.Id, siteId: Roster.SiteId, branchId: Roster.BranchId,
            officeRosterAssignmentId: OfficeSource?.OfficeRosterAssignmentId, employeeAssignmentId: OfficeSource?.EmployeeAssignmentId,
            officeGeofenceVersionId: OfficeSource?.OfficeGeofenceVersionId, holidayCalendarId: OfficeSource?.HolidayCalendarId);
        Facts = AttendanceFacts.Calculate(Employee, Date, [Session]); Punches.Clear();
    }
    public async Task Overtime(bool approve)
    {
        var sessions = await SessionSourcesAsync(Employee, Date, default);
        var employment = (await EmploymentAsync(Employee, Date, default))!;
        var duration = await new ConfirmedOvertimeDurationPolicy().EvaluateAsync(Employee, Date, sessions, Roster, default);
        var source = new OvertimeSourceSnapshot(new(Employee, Date, "SITE", Roster.SiteId, Roster, sessions, null, null, false,
            new("configured", Guid.NewGuid(), true), duration, ""), Guid.NewGuid(), employment, [new([new(EmployeeId: Employee, SiteId: Roster.SiteId)])]);
        source = source with { Source = source.Source with { SourceFingerprint = OvertimeRules.Fingerprint(source) } };
        var proposal = new SubmitOvertimeRequest(Date, Session.Id, Start.AddHours(9), Start.AddHours(11), OvertimeActivity.Work,
            source.Source.SourceFingerprint, "Approved work", "Synthetic written evidence");
        var requester = Guid.NewGuid(); var submission = Guid.NewGuid(); var now = Start.AddHours(12);
        var snapshot = new OvertimeSubmissionSnapshot(1, submission, Employee, requester, proposal, source);
        var reference = AttendanceOperation.Reference(await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), $"attendance:overtime:{Employee:N}:proposal", default));
        var approval = new ApprovalRequest(OvertimeService.RequestType, submission, requester.ToString(), reference, 1, now);
        var request = new OvertimeRequest(Employee, Session.Id, proposal.StartsAtUtc, proposal.EndsAtUtc, 120, duration.PolicyVersionId,
            duration.RateRuleCode, proposal.Reason, reference, requester.ToString(), now, approval.Id, Roster.SiteId);
        ApprovalDecision? decision = null;
        if (approve)
        {
            var ticks = TimeSpan.FromHours(2).Ticks;
            var evidence = new OvertimeDecisionSnapshot(1, request.Id, Employee, source.Source.SourceFingerprint, ApprovalState.Approved,
                120, duration.PolicyVersionId, duration.CalculationVersion, source.Source.Eligibility.VersionId, source.CompensationRecordId,
                duration.RateRuleCode, "calculated_pending_payroll", ticks,
                new(OvertimeFormula.Version, source.CompensationRecordId!.Value, Date, 100000, DateTime.DaysInMonth(Date.Year, Date.Month), ticks,
                    OvertimeFormula.WholeRupees(100000, Date, ticks), "PKR"));
            var decisionRef = AttendanceOperation.Reference(await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(evidence), $"attendance:overtime:{Employee:N}:{request.Id:N}:decision", default));
            decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Configured-HR", "Confirmed", now.AddMinutes(1), decisionRef);
        }
        OvertimeRows.Add(new() { Request = request, Approval = approval, Decision = decision, BusinessDate = Date });
    }
    public void CorruptOvertimeAmount()
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(OvertimeRows.Single().Decision!.ProtectedChangeReference!)!;
        var original = files[reference.StorageKey];
        var value = JsonSerializer.Deserialize<OvertimeDecisionSnapshot>(original.Content)!;
        files[reference.StorageKey] = (original.Purpose, JsonSerializer.SerializeToUtf8Bytes(value with { FinancialEvidence = value.FinancialEvidence! with { Amount = 0 } }));
    }
}
sealed class FixtureClock(Fixture fixture) : TimeProvider { public override DateTimeOffset GetUtcNow() => fixture.Now; }
