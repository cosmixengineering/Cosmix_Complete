using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("AuditEvents", "audit"); b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(100); b.Property(x => x.ActorUserId).HasMaxLength(200);
        b.Property(x => x.ActorRoleCode).HasMaxLength(100); b.Property(x => x.TargetType).HasMaxLength(100);
        b.Property(x => x.TargetId).HasMaxLength(200); b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.ProtectedChangesReference).HasMaxLength(500); b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.DeviceReference).HasMaxLength(200);
        b.HasOne(x => x.EmployeeScope).WithMany().HasForeignKey(x => x.EmployeeScopeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt }); b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
    }
}
