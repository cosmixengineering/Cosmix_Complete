using Cosmix.Domain.Authentication.Scopes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Authentication;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("Teams", "auth"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.ToTable("TeamMembers", "auth", t =>
        {
            t.HasCheckConstraint("CK_TeamMembers_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            t.HasCheckConstraint("CK_TeamMembers_Termination", MembershipConstraints.Termination);
        });
        b.HasKey(x => x.Id); b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TeamId, x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom, x.EffectiveTo }); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.TerminationReason).HasMaxLength(1000);
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects", "auth"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> b)
    {
        b.ToTable("ProjectMembers", "auth", t =>
        {
            t.HasCheckConstraint("CK_ProjectMembers_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            t.HasCheckConstraint("CK_ProjectMembers_Termination", MembershipConstraints.Termination);
        });
        b.HasKey(x => x.Id); b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom, x.EffectiveTo }); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.TerminationReason).HasMaxLength(1000);
    }
}

internal static class MembershipConstraints
{
    internal const string Termination = "([TerminatedEffectiveTo] IS NULL AND [TerminationReason] IS NULL " +
        "AND [TerminatedByUserId] IS NULL AND [TerminatedAt] IS NULL AND [TerminationBusinessDate] IS NULL) OR " +
        "([TerminatedEffectiveTo] IS NOT NULL AND [TerminationReason] IS NOT NULL " +
        "AND [TerminatedByUserId] IS NOT NULL AND [TerminatedAt] IS NOT NULL AND [TerminationBusinessDate] IS NOT NULL " +
        "AND LEN(LTRIM(RTRIM([TerminationReason]))) > 0 " +
        "AND [TerminatedByUserId] <> '00000000-0000-0000-0000-000000000000' " +
        "AND [TerminatedEffectiveTo] > [EffectiveFrom] AND [TerminatedEffectiveTo] >= [TerminationBusinessDate] " +
        "AND ([EffectiveTo] IS NULL OR ([TerminatedEffectiveTo] <= [EffectiveTo] AND [EffectiveTo] > [TerminationBusinessDate])))";
}
