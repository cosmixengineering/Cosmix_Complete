using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Authentication;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions", "auth"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(150).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles", "auth", t => t.HasCheckConstraint("CK_Roles_SystemRole", "[SystemRole] IN (0, 1, 2)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.SystemRole).HasConversion<int>().HasDefaultValue(SystemRoleKind.None);
        b.HasIndex(x => x.SystemRole).IsUnique().HasFilter("[SystemRole] <> 0");
        b.Ignore(x => x.IsSystemRole);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions", "auth", t => t.HasCheckConstraint("CK_RolePermissions_Revocation",
            "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedAt] >= [GrantedAt] AND [RevokedByUserId] IS NOT NULL)"));
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasFilter("[RevokedAt] IS NULL");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> b)
    {
        b.ToTable("UserRoleAssignments", "auth", t =>
        {
            t.HasCheckConstraint("CK_UserRoleAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            t.HasCheckConstraint("CK_UserRoleAssignments_Revocation", "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL AND [RevocationReason] IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserAccountId, x.Id });
        b.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => new { x.EmployeeId, x.UserAccountId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.RevocationReason).HasMaxLength(1000); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.UserAccountId, x.RoleId, x.EffectiveFrom }).IsUnique();
        b.HasIndex(x => new { x.UserAccountId, x.EffectiveFrom, x.EffectiveTo });
    }
}

public sealed class UserAccessScopeConfiguration : IEntityTypeConfiguration<UserAccessScope>
{
    public void Configure(EntityTypeBuilder<UserAccessScope> b)
    {
        b.ToTable("UserAccessScopes", "auth", t => t.HasCheckConstraint("CK_UserAccessScopes_Target",
            "([Kind] IN (1,2) AND [EmployeeId] IS NOT NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR " +
            "([Kind] = 3 AND [EmployeeId] IS NULL AND [DepartmentId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR " +
            "([Kind] = 4 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR " +
            "([Kind] = 5 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [ProjectId] IS NULL) OR " +
            "([Kind] = 6 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NOT NULL) OR " +
            "([Kind] = 7 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL)"));
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.UserRoleAssignmentId, x.Id });
        b.HasOne(x => x.UserRoleAssignment).WithMany().HasForeignKey(x => x.UserRoleAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.Kind, x.EmployeeId }).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.DepartmentId }).IsUnique().HasFilter("[DepartmentId] IS NOT NULL");
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.TeamId }).IsUnique().HasFilter("[TeamId] IS NOT NULL");
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.SiteId }).IsUnique().HasFilter("[SiteId] IS NOT NULL");
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.ProjectId }).IsUnique().HasFilter("[ProjectId] IS NOT NULL");
        b.HasIndex(x => new { x.UserRoleAssignmentId, x.Kind }).IsUnique().HasFilter("[Kind] = 7");
    }
}
