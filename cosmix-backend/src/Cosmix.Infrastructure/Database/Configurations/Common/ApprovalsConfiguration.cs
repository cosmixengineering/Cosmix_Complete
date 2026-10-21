using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Domain.Attendance.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Common;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.ToTable("ApprovalRequests", "workflow", t =>
        {
            t.HasCheckConstraint("CK_ApprovalRequests_State", "[State] BETWEEN 0 AND 4 AND [Stage] > 0");
            t.HasCheckConstraint("CK_ApprovalRequests_Closed", "([State] = 0 AND [ClosedAt] IS NULL) OR ([State] <> 0 AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [RequestedAt])");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestType).HasMaxLength(100);
        b.Property(x => x.RequesterUserId).HasMaxLength(200);
        b.Property(x => x.SnapshotPrivateReference).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.RouteRule).WithMany().HasForeignKey(x => x.RouteRuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.State, x.RequestType, x.RequestedAt });
        b.HasIndex(x => new { x.RequestType, x.SubjectId, x.Stage });
    }
}

public sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> b)
    {
        b.ToTable("ApprovalDecisions", "workflow", t =>
        {
            t.HasCheckConstraint("CK_ApprovalDecisions_Decision", "[Decision] IN (1,2,3)");
            t.HasCheckConstraint("CK_ApprovalDecisions_Reason", "[Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ApprovalRequestId).IsUnique();
        b.Property(x => x.ActorUserId).HasMaxLength(200); b.Property(x => x.ActorRoleCode).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.ProtectedChangeReference).HasMaxLength(500);
    }
}

public sealed class ApprovalRouteRuleConfiguration : IEntityTypeConfiguration<ApprovalRouteRule>
{
    public void Configure(EntityTypeBuilder<ApprovalRouteRule> b)
    {
        b.ToTable("ApprovalRouteRules", "workflow", t =>
        {
            t.HasCheckConstraint("CK_ApprovalRouteRules_Version", "[Version] > 0 AND [Stage] > 0");
            t.HasCheckConstraint("CK_ApprovalRouteRules_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            t.HasCheckConstraint("CK_ApprovalRouteRules_Amount", "([MinimumAmount] IS NULL OR [MinimumAmount] >= 0) AND ([MaximumAmount] IS NULL OR [MaximumAmount] >= 0) AND ([MinimumAmount] IS NULL OR [MaximumAmount] IS NULL OR [MaximumAmount] >= [MinimumAmount]) AND (([MinimumAmount] IS NULL AND [MaximumAmount] IS NULL) OR ([Currency] IS NOT NULL AND LEN([Currency]) = 3))");
            t.HasCheckConstraint("CK_ApprovalRouteRules_Duration", "([MinimumDurationMinutes] IS NULL OR [MinimumDurationMinutes] >= 0) AND ([MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= 0) AND ([MinimumDurationMinutes] IS NULL OR [MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= [MinimumDurationMinutes])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(30);
        b.HasIndex(x => new { x.Code, x.Version }).IsUnique();
        b.Property(x => x.RequestType).HasMaxLength(100); b.Property(x => x.ApproverPermission).HasMaxLength(150);
        b.Property(x => x.ApprovedByUserId).HasMaxLength(200); b.Property(x => x.RiskCode).HasMaxLength(50);
        b.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
        b.Property(x => x.MinimumAmount).HasPrecision(18, 2); b.Property(x => x.MaximumAmount).HasPrecision(18, 2);
        b.HasOne(x => x.StaffCategory).WithMany().HasForeignKey(x => x.StaffCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequestType, x.EffectiveFrom, x.EffectiveTo });
    }
}

public sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> b)
    {
        b.ToTable("ApprovalDelegations", "workflow", t =>
        {
            t.HasCheckConstraint("CK_ApprovalDelegations_Users", "[DelegatorUserId] <> [DelegateUserId]");
            t.HasCheckConstraint("CK_ApprovalDelegations_Dates", "[EndsAt] > [StartsAt]");
            t.HasCheckConstraint("CK_ApprovalDelegations_Revocation", "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)");
            t.HasCheckConstraint("CK_ApprovalDelegations_Scope", "([Permission] IS NULL AND [ScopeKind] IS NULL AND [TargetId] IS NULL) OR ([Permission] IS NOT NULL AND [ScopeKind] IS NOT NULL AND (([ScopeKind] = 7 AND [TargetId] IS NULL) OR ([ScopeKind] BETWEEN 2 AND 6 AND [TargetId] IS NOT NULL)))");
            t.HasCheckConstraint("CK_ApprovalDelegations_RevokeReason", "[Permission] IS NULL OR [RevokedAt] IS NULL OR ([RevocationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RevocationReason]))) > 0)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.DelegatorUserId).HasMaxLength(200); b.Property(x => x.DelegateUserId).HasMaxLength(200);
        b.Property(x => x.AuthorizedByUserId).HasMaxLength(200); b.Property(x => x.RevokedByUserId).HasMaxLength(200);
        b.Property(x => x.RequestType).HasMaxLength(100); b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Permission).HasMaxLength(150); b.Property(x => x.RevocationReason).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.DelegateUserId, x.RequestType, x.StartsAt });
    }
}
