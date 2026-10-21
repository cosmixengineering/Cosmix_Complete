using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Leaves;

/// <summary>Immutable explicit requested interval, never an equal split of an aggregate duration.</summary>
public sealed class LeaveRequestDay : IAppendOnlyEntity
{
    private LeaveRequestDay() { }
    public LeaveRequestDay(Guid leaveRequestId, DateOnly date, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, Guid? shiftVersionId = null)
    {
        if (endsAtUtc <= startsAtUtc || endsAtUtc - startsAtUtc > TimeSpan.FromHours(24) ||
            startsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0 || endsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Leave intervals must have positive whole minutes and be at most 24 hours.");
        Id = Guid.NewGuid(); LeaveRequestId = ModelGuard.Id(leaveRequestId, nameof(leaveRequestId)); Date = date;
        StartsAtUtc = ModelGuard.Utc(startsAtUtc); EndsAtUtc = ModelGuard.Utc(endsAtUtc);
        Minutes = checked((int)((endsAtUtc - startsAtUtc).Ticks / TimeSpan.TicksPerMinute));
        ShiftVersionId = shiftVersionId.HasValue ? ModelGuard.Id(shiftVersionId.Value, nameof(shiftVersionId)) : null;
    }
    public Guid Id { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public int Minutes { get; private set; }
    public Guid? ShiftVersionId { get; private set; }
    public LeaveRequest LeaveRequest { get; private set; } = null!;
}
