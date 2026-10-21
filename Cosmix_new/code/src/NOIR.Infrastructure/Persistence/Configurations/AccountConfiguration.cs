namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Account entity.
/// </summary>
public class AccountConfiguration : TenantEntityConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);

        builder.ToTable("Accounts");

        // Identity
        builder.Property(e => e.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Type stored as string for readability (matches existing enum conversion pattern)
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Multi-tenant unique constraint (CLAUDE.md Rule 18)
        builder.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasDatabaseName("IX_Accounts_TenantId_Code");

        // Hierarchy (self-referencing, restrict delete to protect children)
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ParentId)
            .HasDatabaseName("IX_Accounts_ParentId");

        // State
        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.Property(e => e.IsSystem)
            .HasDefaultValue(false);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);
    }
}
