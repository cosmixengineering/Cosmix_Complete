using Cosmix.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Finance;

public sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatementSnapshot>
{
    public void Configure(EntityTypeBuilder<BankStatementSnapshot> b)
    {
        b.ToTable("BankStatementSnapshots", "finance", t => t.HasCheckConstraint("CK_BankStatement_MonthRevision", "DAY([Month]) = 1 AND [Revision] > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.AccountCode).HasMaxLength(50); b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.FileSha256).HasMaxLength(64); b.Property(x => x.ProofReference).HasMaxLength(2000); b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.HasIndex(x => new { x.AccountCode, x.Currency, x.Month, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.AccountCode, x.Currency, x.Month, x.FileSha256 });
    }
}
public sealed class BankProposalConfiguration : IEntityTypeConfiguration<BankReconciliationProposal>
{
    public void Configure(EntityTypeBuilder<BankReconciliationProposal> b)
    {
        b.ToTable("BankReconciliationProposals", "finance"); b.HasKey(x => x.Id);
        b.Property(x => x.ProofReference).HasMaxLength(2000); b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.HasOne<BankStatementSnapshot>().WithMany().HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.StatementId, x.Revision }).IsUnique();
    }
}
public sealed class BankCloseConfiguration : IEntityTypeConfiguration<BankReconciliationClose>
{
    public void Configure(EntityTypeBuilder<BankReconciliationClose> b)
    {
        b.ToTable("BankReconciliationCloses", "finance"); b.HasKey(x => x.Id);
        b.Property(x => x.AccountCode).HasMaxLength(50); b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ProofReference).HasMaxLength(2000); b.Property(x => x.ClosedBy).HasMaxLength(200);
        b.HasOne<BankStatementSnapshot>().WithMany().HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BankReconciliationProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AccountCode, x.Currency, x.Month }).IsUnique();
        b.HasIndex(x => x.StatementId).IsUnique(); b.HasIndex(x => x.ProposalId).IsUnique();
    }
}
