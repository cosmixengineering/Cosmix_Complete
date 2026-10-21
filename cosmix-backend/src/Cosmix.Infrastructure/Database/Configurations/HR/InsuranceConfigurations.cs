using Cosmix.Domain.HR.Insurance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class InsurancePolicyConfiguration : IEntityTypeConfiguration<InsurancePolicy>
{
    public void Configure(EntityTypeBuilder<InsurancePolicy> builder)
    {
        builder.ToTable("InsurancePolicies", "hr", table =>
        {
            table.HasCheckConstraint("CK_InsurancePolicies_Owner",
                "([EmployeeId] IS NOT NULL AND [EmployeeId] <> '00000000-0000-0000-0000-000000000000' AND [VehicleId] IS NULL) OR " +
                "([VehicleId] IS NOT NULL AND [VehicleId] <> '00000000-0000-0000-0000-000000000000' AND [EmployeeId] IS NULL)");
            table.HasCheckConstraint("CK_InsurancePolicies_CoverageDates", "[ExpiryDate] >= [CoverageStartDate]");
            table.HasCheckConstraint("CK_InsurancePolicies_Predecessor", "[SupersedesPolicyId] IS NULL OR [SupersedesPolicyId] <> [Id]");
            table.HasCheckConstraint("CK_InsurancePolicies_RequiredText",
                "LEN(LTRIM(RTRIM([ProviderName]))) > 0 AND LEN(LTRIM(RTRIM([PolicyNumber]))) > 0 AND LEN(LTRIM(RTRIM([RecordedByUserId]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PolicyNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CoverageStartDate).HasColumnType("date");
        builder.Property(x => x.ExpiryDate).HasColumnType("date");
        builder.Property(x => x.RecordedAt).HasColumnType("datetimeoffset");
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => x.StoredFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesPolicy).WithMany().HasForeignKey(x => x.SupersedesPolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupersedesPolicyId).IsUnique().HasFilter("[SupersedesPolicyId] IS NOT NULL");
        builder.HasIndex(x => x.ExpiryDate);
        builder.HasIndex(x => new { x.EmployeeId, x.ExpiryDate }).HasFilter("[EmployeeId] IS NOT NULL");
        builder.HasIndex(x => new { x.VehicleId, x.ExpiryDate }).HasFilter("[VehicleId] IS NOT NULL");
    }
}

public sealed class InsuranceExpiryReminderConfiguration : IEntityTypeConfiguration<InsuranceExpiryReminder>
{
    public void Configure(EntityTypeBuilder<InsuranceExpiryReminder> builder)
    {
        builder.ToTable("InsuranceExpiryReminders", "hr", table =>
        {
            table.HasCheckConstraint("CK_InsuranceExpiryReminders_Channel", "[Channel] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_InsuranceExpiryReminders_Times",
                "[RemindAt] >= [CreatedAt] AND ([CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt])");
            table.HasCheckConstraint("CK_InsuranceExpiryReminders_State",
                "([CancelledAt] IS NULL AND [CancelledByUserId] IS NULL AND [CancellationReason] IS NULL) OR " +
                "([CancelledAt] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancellationReason] IS NOT NULL AND " +
                "LEN(LTRIM(RTRIM([CancelledByUserId]))) > 0 AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0 AND [NotificationId] IS NULL)");
            table.HasCheckConstraint("CK_InsuranceExpiryReminders_RequiredText",
                "LEN(LTRIM(RTRIM([RecipientUserId]))) > 0 AND LEN(LTRIM(RTRIM([CreatedByUserId]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecipientUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CancelledByUserId).HasMaxLength(200);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.Property(x => x.RemindAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.CreatedAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.CancelledAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.InsurancePolicy).WithMany().HasForeignKey(x => x.InsurancePolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InsurancePolicyId, x.RecipientUserId, x.Channel, x.RemindAt }).IsUnique();
        builder.HasIndex(x => x.NotificationId).IsUnique().HasFilter("[NotificationId] IS NOT NULL");
        builder.HasIndex(x => x.RemindAt).HasFilter("[NotificationId] IS NULL AND [CancelledAt] IS NULL");
    }
}
