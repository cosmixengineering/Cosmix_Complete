using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.HR.Leaves;

var employee = Guid.NewGuid(); var type = Guid.NewGuid(); var policy = Guid.NewGuid();
var from = new DateOnly(2026, 7, 1); var to = from.AddYears(1);
var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
var checks = 0; var errors = 0;
void Check(bool ok, string name) { checks++; if (!ok) { errors++; Console.WriteLine("FAIL: " + name); } }
void Reject(string name, Action action) {
    try { action(); Check(false, name); }
    catch (AuthenticationFailure e) { Check(e.Code == "leave_benefit_usage_conflict" && e.StatusCode == 409, name); }
}
LeaveBenefitUsageEntry Debit(LeaveLedgerEntryType kind) => new(Guid.NewGuid(), employee, type, policy, from, to,
    kind, -120, kind == LeaveLedgerEntryType.Utilization ? Guid.NewGuid() : null, null, now, 480);
foreach (var kind in new[] { LeaveLedgerEntryType.Utilization, LeaveLedgerEntryType.Reservation, LeaveLedgerEntryType.Encashment }) {
    var debit = Debit(kind);
    Check(LeaveBenefitUsage.Active(employee, from, to, [debit]).Count == 1, "Active debit " + kind);
    foreach (var malformed in new[] {
        debit with { Id = Guid.Empty }, debit with { EmployeeId = Guid.NewGuid() }, debit with { YearFrom = from.AddDays(1) },
        debit with { YearTo = to.AddDays(1) }, debit with { LeaveTypeId = Guid.Empty }, debit with { PolicyVersionId = Guid.Empty },
        debit with { StandardDayMinutes = 0 }, debit with { StandardDayMinutes = 1441 }, debit with { Minutes = 0 },
        debit with { Minutes = 120 }, debit with { Minutes = int.MinValue }, debit with { ReversesId = Guid.NewGuid() },
        debit with { Type = LeaveLedgerEntryType.Adjustment }, debit with { Type = LeaveLedgerEntryType.Entitlement },
        debit with { RequestId = Guid.Empty }
    }) Reject("Malformed debit " + kind, () => LeaveBenefitUsage.Active(employee, from, to, [malformed]));
    Reject("Duplicate debit " + kind, () => LeaveBenefitUsage.Active(employee, from, to, [debit, debit]));
    var release = debit with { Id = Guid.NewGuid(), Type = LeaveLedgerEntryType.Release, Minutes = 120, ReversesId = debit.Id, RecordedAt = now.AddMinutes(1) };
    if (kind == LeaveLedgerEntryType.Encashment) {
        Reject("No invented encashment reversal", () => LeaveBenefitUsage.Active(employee, from, to, [debit, release]));
        continue;
    }
    Check(LeaveBenefitUsage.Active(employee, from, to, [debit, release]).Count == 0, "Exact release " + kind);
    Check(LeaveBenefitUsage.Fingerprint([debit, release]) == LeaveBenefitUsage.Fingerprint([release, debit]), "Order-independent usage fingerprint");
    Check(LeaveBenefitUsage.Fingerprint([debit]) != LeaveBenefitUsage.Fingerprint([debit, release]), "Release changes fingerprint");
    foreach (var malformed in new[] {
        release with { ReversesId = null }, release with { ReversesId = Guid.NewGuid() }, release with { Minutes = 119 },
        release with { Minutes = 121 }, release with { Minutes = -120 }, release with { LeaveTypeId = Guid.NewGuid() },
        release with { PolicyVersionId = Guid.NewGuid() }, release with { StandardDayMinutes = 420 },
        release with { RequestId = Guid.NewGuid() }, release with { RecordedAt = now.AddTicks(-1) }
    }) Reject("Malformed release " + kind, () => LeaveBenefitUsage.Active(employee, from, to, [debit, malformed]));
    Reject("Repeated release " + kind, () => LeaveBenefitUsage.Active(employee, from, to, [debit, release, release with { Id = Guid.NewGuid() }]));
    Reject("Orphan release " + kind, () => LeaveBenefitUsage.Active(employee, from, to, [release]));
    var second = Debit(kind);
    Check(LeaveBenefitUsage.Active(employee, from, to, [debit, release, second]).Keys.Single() == second.Id,
        "One release does not release a different debit");
}
Console.WriteLine($"{(errors == 0 ? "PASS" : "FAIL")}: {checks - errors}/{checks} benefit usage validation checks; no SQL/host/storage.");
return errors == 0 ? 0 : 1;
