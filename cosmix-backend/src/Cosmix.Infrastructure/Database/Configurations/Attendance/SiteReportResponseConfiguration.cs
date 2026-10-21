using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class SiteReportResponseConfiguration : IEntityTypeConfiguration<SiteReportResponse>
{
    public void Configure(EntityTypeBuilder<SiteReportResponse> b)
    {
        AttendanceMapping.Record(b, "SiteReportResponses", ("Sequence", "[Sequence] > 0"));
        b.Property(x => x.PrivateEvidenceReference).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.SiteReportId, x.Sequence }).IsUnique();
        b.HasIndex(x => x.ReturnedReviewId).IsUnique();
        b.HasOne<SiteReport>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.SiteReportId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SiteReportReview>().WithMany().HasForeignKey(x => new { x.SiteReportId, x.ReturnedReviewId })
            .HasPrincipalKey(x => new { x.SiteReportId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.SubmittedByAccountId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
