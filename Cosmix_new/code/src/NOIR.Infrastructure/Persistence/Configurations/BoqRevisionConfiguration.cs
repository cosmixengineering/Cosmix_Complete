namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BoqRevision entity.
/// </summary>
public class BoqRevisionConfiguration : BaseEntityConfiguration<BoqRevision>
{
    public override void Configure(EntityTypeBuilder<BoqRevision> builder)
    {
        base.Configure(builder);

        builder.ToTable("BoqRevisions");

        // Parent BOQ
        builder.Property(e => e.BoqId)
            .IsRequired();

        builder.HasIndex(e => e.BoqId)
            .HasDatabaseName("IX_BoqRevisions_BoqId");

        // Revision data
        builder.Property(e => e.RevisionNumber)
            .IsRequired();

        builder.HasIndex(e => new { e.BoqId, e.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("IX_BoqRevisions_BoqId_RevisionNumber");

        builder.Property(e => e.Reason)
            .HasMaxLength(500);

        builder.Property(e => e.PreviousTotal)
            .HasPrecision(18, 2);

        builder.Property(e => e.NewTotal)
            .HasPrecision(18, 2);

        builder.Property(e => e.CreatedByUserId)
            .HasMaxLength(DatabaseConstants.UserIdMaxLength);

        // Relationship with Boq (configured from the aggregate side as well)
        builder.HasOne<Boq>()
            .WithMany(b => b.Revisions)
            .HasForeignKey(e => e.BoqId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
