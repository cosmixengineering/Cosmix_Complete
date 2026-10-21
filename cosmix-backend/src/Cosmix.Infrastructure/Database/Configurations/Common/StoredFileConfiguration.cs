using Cosmix.Domain.Common.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("StoredFiles", "files", t =>
        {
            t.HasCheckConstraint("CK_StoredFiles_Size", "[SizeBytes] > 0");
            t.HasCheckConstraint("CK_StoredFiles_Scan", "[ScanState] BETWEEN 0 AND 3 AND ([ScanState] <> 1 OR [SignatureVerified] = 1)");
            t.HasCheckConstraint("CK_StoredFiles_Retention", "[RetainUntil] IS NULL OR [RetainUntil] > [UploadedAt]");
            t.HasCheckConstraint("CK_StoredFiles_Hash", "LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            t.HasCheckConstraint("CK_StoredFiles_Deleted", "([DeletedAt] IS NULL AND [DeletedByUserId] IS NULL) OR ([DeletedAt] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [DeletedAt] >= [UploadedAt])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.StorageKey).HasMaxLength(400); b.HasIndex(x => x.StorageKey).IsUnique();
        b.Property(x => x.OriginalName).HasMaxLength(255); b.Property(x => x.Extension).HasMaxLength(20);
        b.Property(x => x.ContentType).HasMaxLength(100); b.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.UploadedByUserId).HasMaxLength(200); b.Property(x => x.EncryptionKeyReference).HasMaxLength(300);
        b.Property(x => x.ScannerVersion).HasMaxLength(150); b.Property(x => x.DeletedByUserId).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.ScanState, x.UploadedAt });
    }
}
