using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Domain.Attendance.Calculation;

namespace Cosmix.Application.Attendance.Overtime;

internal sealed record OvertimeAttendanceEvidence(OvertimeRecord Row, OvertimeSourceSnapshot Source,
    long? ApprovedTicks, IReadOnlyList<AttendanceTimeInterval> ApprovedIntervals, string Digest);

public sealed partial class OvertimeService
{
    /// <summary>Caller already owns authorization. Uses the same retained proposal/decision/formula
    /// verification as the protected OT detail endpoint; no public monetary projection is added.</summary>
    internal async Task<OvertimeAttendanceEvidence> ReadAttendanceEvidenceAsync(Guid employeeId, Guid requestId, CancellationToken ct)
    {
        var value = await Evidence(employeeId, requestId, ct);
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { value.Snapshot, value.Decision, value.Row.Approval.State, value.Row.Approval.ClosedAt,
                DecisionId = value.Row.Decision?.Id, value.Row.Approval.RowVersion })));
        var proposal = value.Snapshot.Proposal;
        var intervals = value.Decision?.ApprovedTicks is null ? [] : value.Snapshot.Inputs.Source.DurationPolicy.EligibleIntervals
            .Select(x => new AttendanceTimeInterval(x.StartsAtUtc > proposal.StartsAtUtc ? x.StartsAtUtc : proposal.StartsAtUtc,
                x.EndsAtUtc < proposal.EndsAtUtc ? x.EndsAtUtc : proposal.EndsAtUtc))
            .Where(x => x.EndsAtUtc > x.StartsAtUtc).OrderBy(x => x.StartsAtUtc).ToArray();
        return new(value.Row, value.Snapshot.Inputs, value.Decision?.ApprovedTicks, intervals, digest);
    }
}
