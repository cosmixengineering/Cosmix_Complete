using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollPostingProfileConfiguration : IEntityTypeConfiguration<PayrollPostingProfile>
{
    public void Configure(EntityTypeBuilder<PayrollPostingProfile> builder)
    {
        builder.ToTable("PayrollPostingProfiles", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.EffectiveFrom).IsRequired();
        builder.Property(x => x.EffectiveToExclusive);
        builder.Property(x => x.RulesJson).HasMaxLength(30000).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(200);
        builder.Property(x => x.ApprovedAt);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.Currency, x.EffectiveFrom });
        builder.HasIndex(x => x.State);
    }
}
