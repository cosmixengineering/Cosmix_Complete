using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class StatutoryContributionConfiguration : IEntityTypeConfiguration<StatutoryContribution>
{
    public void Configure(EntityTypeBuilder<StatutoryContribution> builder)
    {
        builder.ToTable("StatutoryContributions", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scheme).IsRequired();
        builder.Property(x => x.ContributionMonth).IsRequired();
        builder.Property(x => x.EmployeeContributionAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.EmployerContributionAmount).HasPrecision(18, 2).IsRequired();
        builder.Ignore(x => x.TotalContributionAmount);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.PayableAccountCode).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.EmployerRegistrationNumber).HasMaxLength(100);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(200);
        builder.Property(x => x.PostedByUserId).HasMaxLength(200);
        builder.Property(x => x.JournalEntryId);
        builder.Property(x => x.EmployeeItemsJson);
        builder.Property(x => x.HasEmployeeEntries).IsRequired();
        builder.Property(x => x.ObligationId);
        builder.HasMany(x => x.Entries).WithOne(x => x.Contribution).HasForeignKey(x => x.ContributionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.Scheme, x.ContributionMonth }).IsUnique();
        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.JournalEntryId);
        builder.HasIndex(x => x.ObligationId);
    }
}
