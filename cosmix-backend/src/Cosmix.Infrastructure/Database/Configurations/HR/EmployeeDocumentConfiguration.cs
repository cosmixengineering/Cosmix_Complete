using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocuments", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeDocuments_Type", "[DocumentType] BETWEEN 1 AND 10");
            table.HasCheckConstraint("CK_EmployeeDocuments_Privacy", "[Privacy] IN (0, 1)");
            table.HasCheckConstraint("CK_EmployeeDocuments_Size", "[SizeBytes] > 0");
            table.HasCheckConstraint("CK_EmployeeDocuments_ScanStatus", "[ScanStatus] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_EmployeeDocuments_VerificationStatus", "[VerificationStatus] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_EmployeeDocuments_ScanEvidence", "([ScanStatus] = 0 AND [ScannedAt] IS NULL) OR ([ScanStatus] <> 0 AND [ScannedAt] IS NOT NULL AND [ScannedAt] >= [UploadedAt] AND [ScannerVersion] IS NOT NULL AND ([ScanStatus] <> 1 OR [SignatureVerified] = 1))");
            table.HasCheckConstraint("CK_EmployeeDocuments_VerificationEvidence", "([VerificationStatus] = 0 AND [VerifiedAt] IS NULL AND [VerifiedByUserId] IS NULL) OR ([VerificationStatus] <> 0 AND [VerifiedAt] IS NOT NULL AND [VerifiedAt] >= [UploadedAt] AND [VerifiedByUserId] IS NOT NULL AND ([VerificationStatus] <> 2 OR [VerificationReason] IS NOT NULL))");
            table.HasCheckConstraint("CK_EmployeeDocuments_ScanConflict", "[HasScanConflict] = 0 OR [ScanStatus] <> 0");
            table.HasCheckConstraint("CK_EmployeeDocuments_VerifiedScan", "[VerifiedScanAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ScannedAt] IS NOT NULL AND [VerifiedScanAt] >= [UploadedAt] AND [VerifiedScanAt] <= [VerifiedAt] AND [VerifiedScanAt] <= [ScannedAt])");
            table.HasCheckConstraint("CK_EmployeeDocuments_Dates",
                "[IssuedOn] IS NULL OR [ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]");
            table.HasCheckConstraint("CK_EmployeeDocuments_Lifecycle", "([LifecycleStatus] = 0 AND [LifecycleChangedAt] IS NULL AND [LifecycleChangedByUserId] IS NULL AND [LifecycleReason] IS NULL) OR ([LifecycleStatus] BETWEEN 1 AND 3 AND [LifecycleChangedAt] IS NOT NULL AND [LifecycleChangedAt] >= [UploadedAt] AND [LifecycleChangedByUserId] IS NOT NULL AND [LifecycleReason] IS NOT NULL)");
            table.HasCheckConstraint("CK_EmployeeDocuments_ScanAttempt", "([ActiveScanAttemptId] IS NULL AND [ScanStartedAt] IS NULL AND [IsScanInProgress] = 0) OR ([ActiveScanAttemptId] IS NOT NULL AND [ScanStartedAt] IS NOT NULL AND [ScanStartedAt] >= [UploadedAt])");
            table.HasCheckConstraint("CK_EmployeeDocuments_HistoryRevision", "[HistoryRevision] >= 0");
            table.HasCheckConstraint("CK_EmployeeDocuments_Sha256",
                "LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.EmployeeId });
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.StorageKey).HasMaxLength(400).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ScannerVersion).HasMaxLength(150);
        builder.Property(x => x.HasScanConflict).HasDefaultValue(false);
        builder.Property(x => x.VerifiedByUserId).HasMaxLength(200);
        builder.Property(x => x.VerificationReason).HasMaxLength(1000);
        builder.Property(x => x.LifecycleStatus).HasDefaultValue(EmployeeDocumentLifecycleStatus.Current);
        builder.Property(x => x.LifecycleChangedByUserId).HasMaxLength(200);
        builder.Property(x => x.LifecycleReason).HasMaxLength(1000);
        builder.Property(x => x.IsScanInProgress).HasDefaultValue(false);
        builder.Property(x => x.HistoryRevision).HasDefaultValue(0);
        builder.Ignore(x => x.IsAvailable);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.EmployeeId, x.DocumentType });
        builder.HasIndex(x => x.ExpiresOn);
    }
}
