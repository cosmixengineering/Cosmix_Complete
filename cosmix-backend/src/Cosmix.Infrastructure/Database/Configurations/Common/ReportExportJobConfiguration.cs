using Cosmix.Domain.Common.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> b)
    {
        b.ToTable("ReportExportJobs", "reports", t =>
        {
            t.HasCheckConstraint("CK_ReportExportJobs_State", "[State] BETWEEN 0 AND 3 AND [Format] BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_ReportExportJobs_Period", "([PeriodFrom] IS NULL AND [PeriodTo] IS NULL) OR ([PeriodFrom] IS NOT NULL AND [PeriodTo] IS NOT NULL AND [PeriodTo] >= [PeriodFrom])");
            t.HasCheckConstraint("CK_ReportExportJobs_Completed", "[State] <> 2 OR ([StoredFileId] IS NOT NULL AND [GeneratedAt] IS NOT NULL AND [DownloadExpiresAt] IS NOT NULL AND [GeneratedAt] >= [RequestedAt] AND [DownloadExpiresAt] > [GeneratedAt])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RequestedByUserId).HasMaxLength(200);
        b.Property(x => x.ReportType).HasMaxLength(100); b.Property(x => x.ProtectedFilterScopeReference).HasMaxLength(500);
        b.Property(x => x.SourceLockReference).HasMaxLength(500); b.Property(x => x.FailureCode).HasMaxLength(100);
        b.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => x.StoredFileId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.RequestedByUserId, x.RequestedAt }); b.HasIndex(x => x.State);
    }
}
