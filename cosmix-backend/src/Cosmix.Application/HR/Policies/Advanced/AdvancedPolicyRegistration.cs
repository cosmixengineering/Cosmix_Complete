using Cosmix.Domain.HR.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Application.HR.Policies.Advanced;

// C01 extension keeps the parent-owned frozen contracts and ordinary membership DTO unchanged.
public sealed record AdvancedPolicyStaffCategorySource(Guid MembershipId, Guid StaffCategoryId, string Code);
public sealed record AdvancedPolicyDatedSource(Policy? Policy, AttendancePolicySource Source,
    IReadOnlyList<AdvancedPolicyStaffCategorySource> StaffCategories);

public interface IAdvancedPolicyPublicationRepository : IAdvancedAttendancePolicyRepository
{
    Task<AdvancedPolicyDatedSource> DatedSourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct);
    Task<bool> StaffCategoryCodesExistAsync(IReadOnlyList<string> codes, CancellationToken ct);
    Task<bool> HasTargetOverlapAsync(AdvancedAttendanceFamily family, AttendancePolicyTarget target, Guid targetId,
        DateOnly from, DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct);
    void Add(PolicyAssignmentEnd end);
}

public static class AdvancedPolicyRegistration
{
    public static IServiceCollection AddAdvancedAttendancePoliciesApplication(this IServiceCollection services)
    {
        services.TryAddScoped<AdvancedPolicyReader>();
        services.TryAddScoped<IAdvancedAttendancePolicyReader>(sp => sp.GetRequiredService<AdvancedPolicyReader>());
        services.TryAddScoped<AdvancedPolicyService>();
        return services;
    }
}
