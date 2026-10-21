using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class OvertimeRequestConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> b)
    {
        AttendanceMapping.Record(b, "OvertimeRequests", ("Window", "[EndsAtUtc] > [StartsAtUtc]"),
            ("Minutes", "[RequestedMinutes] > 0 AND CAST([RequestedMinutes] AS bigint) * 600000000 <= DATEDIFF_BIG(microsecond, [StartsAtUtc], [EndsAtUtc]) * 10"));
        AttendanceMapping.Strings(b, ("RateRuleCode", 100), ("Reason", 2000), ("PrivateEvidenceReference", 500), ("RequestedBy", 200), ("JobReference", 200));
        b.HasIndex(x => new { x.EmployeeId, x.StartsAtUtc }); b.HasIndex(x => x.BulkSubmissionId);
        b.HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.WorkSessionId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
