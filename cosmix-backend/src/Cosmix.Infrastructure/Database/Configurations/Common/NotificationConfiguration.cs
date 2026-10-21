using Cosmix.Domain.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications", "communication", t =>
        {
            t.HasCheckConstraint("CK_Notifications_Channel", "[Channel] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Notifications_Times", "([DeliveredAt] IS NULL OR [DeliveredAt] >= [CreatedAt]) AND ([ReadAt] IS NULL OR [ReadAt] >= [CreatedAt])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RecipientUserId).HasMaxLength(200);
        b.Property(x => x.TemplateCode).HasMaxLength(100); b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.ProtectedPayloadReference).HasMaxLength(500); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.RecipientUserId, x.ReadAt, x.CreatedAt });
    }
}
