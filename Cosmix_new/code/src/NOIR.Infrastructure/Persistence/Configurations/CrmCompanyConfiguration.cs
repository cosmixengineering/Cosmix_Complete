namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for CrmCompany entity.
/// </summary>
public class CrmCompanyConfiguration : IEntityTypeConfiguration<CrmCompany>
{
    public void Configure(EntityTypeBuilder<CrmCompany> builder)
    {
        builder.ToTable("CrmCompanies");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Name (unique per tenant)
        builder.Property(e => e.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.HasIndex(e => new { e.Name, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_CrmCompanies_Name_TenantId");

        // Domain (filtered unique per tenant — only when not null)
        builder.Property(e => e.Domain)
            .HasMaxLength(256);

        builder.HasIndex(e => new { e.Domain, e.TenantId })
            .IsUnique()
            .HasFilter("[Domain] IS NOT NULL")
            .HasDatabaseName("IX_CrmCompanies_Domain_TenantId");

        // Industry
        builder.Property(e => e.Industry)
            .HasMaxLength(200);

        // Address
        builder.Property(e => e.Address)
            .HasMaxLength(500);

        // Phone
        builder.Property(e => e.Phone)
            .HasMaxLength(20);

        // Website
        builder.Property(e => e.Website)
            .HasMaxLength(500);

        // TaxId
        builder.Property(e => e.TaxId)
            .HasMaxLength(50);

        // Notes
        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Owner FK (Employee, SetNull on delete)
        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
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
    }
}
