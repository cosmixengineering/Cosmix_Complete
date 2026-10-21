namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for CrmContact entity.
/// </summary>
public class CrmContactConfiguration : IEntityTypeConfiguration<CrmContact>
{
    public void Configure(EntityTypeBuilder<CrmContact> builder)
    {
        builder.ToTable("CrmContacts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Name
        builder.Property(e => e.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasMaxLength(100)
            .IsRequired();

        // Email (unique per tenant)
        builder.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(e => new { e.Email, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_CrmContacts_Email_TenantId");

        // Phone
        builder.Property(e => e.Phone)
            .HasMaxLength(20);

        // JobTitle
        builder.Property(e => e.JobTitle)
            .HasMaxLength(200);

        // Source
        builder.Property(e => e.Source)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Notes
        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Company FK (SetNull on delete)
        builder.HasOne(e => e.Company)
            .WithMany(c => c.Contacts)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Owner FK (Employee, SetNull on delete)
        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Customer FK (SetNull on delete)
        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tenant
        builder.Property(e => e.TenantId)
            .HasMaxLength(DatabaseConstants.TenantIdMaxLength);
        builder.HasIndex(e => e.TenantId);

        // Audit fields
        builder.Property(e => e.CreatedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.ModifiedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.DeletedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        // Soft delete query filter
        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);

        // Ignore computed properties
        builder.Ignore(e => e.FullName);
    }
}
