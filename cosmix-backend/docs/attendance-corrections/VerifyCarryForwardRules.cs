#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCarryForwardRules.packages.lock.json
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Authentication;

// Specification-first harness. Awaiting feature implementation and coordinated execution.
// No account, provider, file-store or SQL activity is permitted here.
var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Conflict(Action action, string code)
{
    checks++;
    try { action(); } catch (AuthenticationFailure error) when (error.Code == code) { return; }
    throw new Exception(code);
}
LockedPeriodRange Lock(int fromMonth, int fromDay, int toMonth, int toDay) =>
    new(Guid.NewGuid(), new(2026, fromMonth, fromDay), new(2026, toMonth, toDay));
var source = new DateOnly(2026, 10, 31);
var empty = CarryForwardRules.FirstAvailableMonth(source, []);
Check(empty.Start == new DateOnly(2026, 11, 1) && empty.EndExclusive == new DateOnly(2026, 12, 1), "no lock creates no phantom period and picks following calendar month");
Check(CarryForwardRules.FirstAvailableMonth(source, [Lock(11, 15, 11, 16)]).Start == new DateOnly(2026, 12, 1),
    "one locked day excludes the entire candidate month");
Check(CarryForwardRules.FirstAvailableMonth(source, [Lock(10, 1, 11, 1)]).Start == new DateOnly(2026, 11, 1),
    "exclusive source-period end does not block the next month");
Check(CarryForwardRules.FirstAvailableMonth(source, [Lock(10, 30, 11, 2)]).Start == new DateOnly(2026, 12, 1),
    "multi-month source period also excludes its overlapping target month");
Check(CarryForwardRules.FirstAvailableMonth(source, [Lock(12, 1, 12, 31)]).Start == new DateOnly(2026, 11, 1),
    "a later lock cannot skip an earlier eligible month");
Check(CarryForwardRules.FirstAvailableMonth(new(2026, 12, 15), []).Start == new DateOnly(2027, 1, 1), "calendar year boundary");
Check(CarryForwardRules.FirstAvailableMonth(new(2028, 1, 31), []).EndExclusive == new DateOnly(2028, 3, 1), "leap February uses a calendar month");
var overlapping = new LockedPeriodRange[]
{
    new(Guid.NewGuid(), new(2026, 11, 20), new(2027, 1, 4)),
    new(Guid.NewGuid(), new(2026, 11, 5), new(2026, 12, 2))
};
Check(CarryForwardRules.FirstAvailableMonth(source, overlapping).Start == new DateOnly(2027, 2, 1), "unsorted overlapping ranges skip each affected whole month");
Conflict(() => CarryForwardRules.FirstAvailableMonth(new(9999, 12, 1), []), "carry_forward_target_unavailable");
var second = TimeSpan.TicksPerSecond;
Check(CarryForwardRules.MinuteDelta(599 * second / 10, 591 * second / 10) == 0, "negative sub-minute difference is not floored to a whole lost minute");
Check(CarryForwardRules.MinuteDelta(601 * second / 10, 599 * second / 10) == -1, "crossing a minute boundary produces the correct negative difference");
Check(CarryForwardRules.MinuteDelta(599 * second / 10, 601 * second / 10) == 1, "crossing a minute boundary produces the correct positive difference");
Check(CarryForwardRules.MinuteDelta(8 * TimeSpan.TicksPerHour, 10 * TimeSpan.TicksPerHour) +
    CarryForwardRules.MinuteDelta(10 * TimeSpan.TicksPerHour, 9 * TimeSpan.TicksPerHour) == 60,
    "successive corrections compare with last interpretation, avoiding baseline double-counting");
Console.WriteLine($"Carry-forward rule checks passed: {checks}. Pure calendar/factual arithmetic only.");
