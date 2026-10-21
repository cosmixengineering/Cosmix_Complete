using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class LeaveLedgerEntryConfiguration : IEntityTypeConfiguration<LeaveLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LeaveLedgerEntry> builder)
    {
        builder.ToTable("LeaveLedgerEntries", "hr", table =>
        {
            table.HasCheckConstraint("CK_LeaveLedgerEntries_1", "[LeaveYearEnd] > [LeaveYearStart]");
            table.HasCheckConstraint("CK_LeaveLedgerEntries_2", "[MinutesDelta] <> 0");
            table.HasCheckConstraint("CK_LeaveLedgerEntries_3", "[EntryType] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_LeaveLedgerEntries_4", "[ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => new { x.LeaveRequestId, x.EmployeeId, x.LeaveTypeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId, x.LeaveTypeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversesEntry).WithMany().HasForeignKey(x => new { x.ReversesEntryId, x.EmployeeId, x.LeaveTypeId, x.LeaveYearStart, x.LeaveYearEnd }).HasPrincipalKey(x => new { x.Id, x.EmployeeId, x.LeaveTypeId, x.LeaveYearStart, x.LeaveYearEnd }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.LeaveYearStart });
    }
}
