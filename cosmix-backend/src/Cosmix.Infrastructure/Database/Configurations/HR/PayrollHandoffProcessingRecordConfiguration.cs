using Cosmix.Domain.HR.PayrollHandoff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollHandoffProcessingRecordConfiguration : IEntityTypeConfiguration<PayrollHandoffProcessingRecord>
{
    public void Configure(EntityTypeBuilder<PayrollHandoffProcessingRecord> builder)
    {
        builder.ToTable("PayrollHandoffProcessingRecords", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProcessingReference).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ProtectedResultReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ProcessedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.PayrollHandoffBatch).WithMany().HasForeignKey(x => x.PayrollHandoffBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PayrollHandoffBatchId, x.ExternalProcessingReference }).IsUnique();
    }
}
