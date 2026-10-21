using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class SiteReportConfiguration : IEntityTypeConfiguration<SiteReport>
{
    public void Configure(EntityTypeBuilder<SiteReport> b)
    {
        AttendanceMapping.Record(b, "SiteReports", ("Kind", "[Kind] BETWEEN 1 AND 5"), ("Coordinates", AttendanceMapping.CoordinatesCheck));
        AttendanceMapping.Strings(b, ("TaskReference", 200), ("Notes", 4000), ("IdempotencyKey", 100), ("PrivateEvidenceReference", 500));
        AttendanceMapping.Coordinates(b); b.Property(x => x.IdempotencyKey).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique(); b.HasIndex(x => new { x.SiteId, x.ReceivedAtUtc });
        b.HasOne<EmployeeAssignment>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.EmployeeAssignmentId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.UserAccountId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegisteredDevice>().WithMany().HasForeignKey(x => new { x.UserAccountId, x.RegisteredDeviceId }).HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Reviews).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class SiteReportReviewConfiguration : IEntityTypeConfiguration<SiteReportReview>
{
    public void Configure(EntityTypeBuilder<SiteReportReview> b)
    {
        AttendanceMapping.Record(b, "SiteReportReviews", ("Sequence", "[Sequence] > 0"), ("Outcome", "[Outcome] BETWEEN 1 AND 4"));
        AttendanceMapping.Strings(b, ("Reviewer", 200), ("ReviewerRole", 200), ("Reason", 2000));
        b.HasIndex(x => new { x.SiteReportId, x.Sequence }).IsUnique();
        b.HasOne(x => x.Report).WithMany(x => x.Reviews).HasForeignKey(x => x.SiteReportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
