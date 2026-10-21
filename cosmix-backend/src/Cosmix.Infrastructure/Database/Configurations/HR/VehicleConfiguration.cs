using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RegistrationNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.VehicleType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.MakeModel).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AssetReference).HasMaxLength(100);
        builder.HasIndex(x => x.RegistrationNumber).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
