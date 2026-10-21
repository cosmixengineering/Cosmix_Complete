namespace NOIR.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : TenantEntityConfiguration<Ticket>
{
    public override void Configure(EntityTypeBuilder<Ticket> builder)
    {
        base.Configure(builder);
        builder.ToTable("HelpdeskTickets");
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(4000).IsRequired();
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasMany(t => t.Comments)
            .WithOne()
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketCommentConfiguration : TenantEntityConfiguration<TicketComment>
{
    public override void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        base.Configure(builder);
        builder.ToTable("HelpdeskTicketComments");
        builder.Property(c => c.Text).HasMaxLength(4000).IsRequired();
    }
}
