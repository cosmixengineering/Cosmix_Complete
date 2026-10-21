#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.Common.Approvals;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Denied(Action action) { try { action(); } catch (AuthenticationFailure x) when (x.StatusCode == 403) { checks++; return; } throw new Exception("Self review accepted"); }
var start = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
WaitingInterval I(double from, double to) => new(start.AddMinutes(from), start.AddMinutes(to));
var rules = new WaitingRules(3600m, true, true, WaitingEffect.FactualOnly, null, null);
var a = WaitingAttribution.Calculate(I(0,45), [I(0,90)], [I(0,45)], [I(20,45)], [], [], [], rules);
Check(a.ProvenTicks == 20 * TimeSpan.TicksPerMinute, "10:00-10:20 wait, productive until 10:45 proves only 20 minutes");
var c = WaitingAttribution.Calculate(I(10,30), [I(0,90)], [I(0,45)], [], [], a.ProvenIntervals, a.ProvenIntervals, rules);
Check(c.CurrentLossTicks == 30 * TimeSpan.TicksPerMinute && c.DeltaTicks == 10 * TimeSpan.TicksPerMinute, "A/C union does not multiply common loss");
var capped = WaitingAttribution.Calculate(I(10,40), [I(0,90)], [I(0,45)], [], [], a.ProvenIntervals, a.ProvenIntervals, rules with { MaximumAttributedSeconds=1500m });
Check(capped.CurrentLossTicks == 25 * TimeSpan.TicksPerMinute && capped.DeltaTicks == 5 * TimeSpan.TicksPerMinute, "One configured group cap");
var changed = WaitingAttribution.Calculate(I(0,45), [I(0,90)], [I(15,45)], [I(20,45)], [], a.ProvenIntervals, [], rules);
Check(changed.ProvenTicks == 5 * TimeSpan.TicksPerMinute && changed.DeltaTicks == -15 * TimeSpan.TicksPerMinute, "Changed approved start is a signed retained revision");
var excused = WaitingAttribution.Calculate(I(0,45), [I(0,90)], [I(0,45)], [I(20,45)], [I(0,10)], [], [], rules);
Check(excused.ProvenTicks == 10 * TimeSpan.TicksPerMinute, "Approved excused interval is subtracted");
var exact = WaitingAttribution.Calculate(new(start, start.AddTicks(1)), [I(0,1)], [I(0,1)], [], [], [], [], rules);
Check(exact.ProvenTicks == 1 && WaitingAttribution.DisplayMinutes(exact.ProvenTicks) == 0, "Private ticks survive zero display minutes");
Check(WaitingAttribution.Ticks([I(0,20), I(20,30), I(5,10)]) == 30 * TimeSpan.TicksPerMinute, "Adjacent and duplicate evidence union");
Check(WaitingAttribution.Subtract([I(0,30)], [I(5,10),I(15,20)]).Count == 3, "Alternate work retains disjoint fragments");
var requester = Guid.NewGuid(); var beneficiary = Guid.NewGuid(); var causer = Guid.NewGuid();
Denied(() => WaitingAttribution.Independent(requester, Guid.NewGuid(), requester, [beneficiary], causer));
Denied(() => WaitingAttribution.Independent(Guid.NewGuid(), beneficiary, requester, [beneficiary], causer));
Denied(() => WaitingAttribution.Independent(Guid.NewGuid(), causer, requester, [beneficiary], causer));
WaitingAttribution.Independent(Guid.NewGuid(), Guid.NewGuid(), requester, [beneficiary], causer); checks++;
Check(WaitingAttribution.ExactPoints(TimeSpan.TicksPerHour/2, rules with { Effect=WaitingEffect.PerformanceProposal, PerformancePointsPerHour=0.8m }) == 0.4m, "Configured performance keeps exact points");
var thirds=rules with { Effect=WaitingEffect.PerformanceProposal, PerformancePointsPerHour=3m };
Check(WaitingAttribution.ExactPoints(20*TimeSpan.TicksPerMinute, thirds)==1m, "Multiply before dividing preserves representable exact points for one-third hour");
Check(WaitingAttribution.ExactPoints(-5*TimeSpan.TicksPerMinute, thirds)==-.25m, "Signed correction preserves exact configured points");
var root=Guid.NewGuid();
Check(!WaitingAttribution.HasUnresolvedReturnedRevision([(root,1,ApprovalState.Returned),(root,2,ApprovalState.Approved)]), "Approved amendment resolves old Return without rewriting it");
Check(WaitingAttribution.HasUnresolvedReturnedRevision([(root,1,ApprovalState.Approved),(root,2,ApprovalState.Returned)]), "Newest Return remains unverified for consumers");
Console.WriteLine($"C05 attribution/authority checks passed: {checks}. No SQL, host, provider or fixtures.");
