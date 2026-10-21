using Cosmix.Domain.HR.StaffCategories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class StaffCategoryConfiguration : IEntityTypeConfiguration<StaffCategory>
{
    public void Configure(EntityTypeBuilder<StaffCategory> builder)
    {
        builder.ToTable("StaffCategories", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

