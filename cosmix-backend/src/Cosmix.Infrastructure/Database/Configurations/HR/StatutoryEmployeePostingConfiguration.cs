using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class StatutoryEmployeePostingConfiguration : IEntityTypeConfiguration<StatutoryEmployeePosting>
{
    public void Configure(EntityTypeBuilder<StatutoryEmployeePosting> builder)
    {
        builder.ToTable("StatutoryEmployeePostings", "hr");
        builder.HasKey(x => x.EntryId);
        builder.Property(x => x.SourceFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PostedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayableAccountCode).HasMaxLength(50).IsRequired();
        builder.HasOne<StatutoryContributionEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StatutoryContribution>().WithMany(x => x.EmployeePostings).HasForeignKey(x => x.ContributionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollJournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
