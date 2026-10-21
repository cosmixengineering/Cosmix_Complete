using Cosmix.Domain.Common.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages", "jobs", t =>
        {
            t.HasCheckConstraint("CK_OutboxMessages_State", "[State] BETWEEN 0 AND 3 AND [Attempts] >= 0");
            t.HasCheckConstraint("CK_OutboxMessages_Lease", "([State] = 1 AND [LeaseToken] IS NOT NULL AND [LeaseExpiresAt] IS NOT NULL) OR ([State] <> 1 AND [LeaseToken] IS NULL AND [LeaseExpiresAt] IS NULL)");
            t.HasCheckConstraint("CK_OutboxMessages_Completed", "([State] = 2 AND [CompletedAt] IS NOT NULL AND [CompletedAt] >= [CreatedAt]) OR ([State] <> 2 AND [CompletedAt] IS NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.MessageType).HasMaxLength(100);
        b.Property(x => x.EncryptedPayloadReference).HasMaxLength(500); b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.DeduplicationKey).HasMaxLength(150).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.LastFailureCode).HasMaxLength(100); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.MessageType, x.DeduplicationKey }).IsUnique();
        b.HasIndex(x => new { x.State, x.AvailableAt, x.LeaseExpiresAt });
    }
}
