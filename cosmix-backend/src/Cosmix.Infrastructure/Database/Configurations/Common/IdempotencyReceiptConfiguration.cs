using Cosmix.Domain.Common.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class IdempotencyReceiptConfiguration : IEntityTypeConfiguration<IdempotencyReceipt>
{
    public void Configure(EntityTypeBuilder<IdempotencyReceipt> b)
    {
        b.ToTable("IdempotencyReceipts", "workflow", t =>
        {
            t.HasCheckConstraint("CK_IdempotencyReceipts_Expiry", "[ExpiresAt] > [CreatedAt]");
            t.HasCheckConstraint("CK_IdempotencyReceipts_Hash", "LEN([RequestSha256]) = 64 AND [RequestSha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            t.HasCheckConstraint("CK_IdempotencyReceipts_Result", "([CompletedAt] IS NULL AND [ResultCode] IS NULL AND [ProtectedResultReference] IS NULL) OR ([CompletedAt] IS NOT NULL AND [ResultCode] IS NOT NULL AND [ProtectedResultReference] IS NOT NULL AND [CompletedAt] >= [CreatedAt] AND [CompletedAt] < [ExpiresAt])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.ActorUserId).HasMaxLength(200);
        b.Property(x => x.Operation).HasMaxLength(100); b.Property(x => x.Key).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.RequestSha256).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.ResultCode).HasMaxLength(100); b.Property(x => x.ProtectedResultReference).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => new { x.ActorUserId, x.Operation, x.Key }).IsUnique(); b.HasIndex(x => x.ExpiresAt);
    }
}
