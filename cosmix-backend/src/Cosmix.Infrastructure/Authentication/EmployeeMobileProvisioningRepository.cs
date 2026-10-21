using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication;

public sealed class EmployeeMobileProvisioningRepository(CosmixDbContext db) : IEmployeeMobileProvisioningRepository
{
    public async Task<ApprovedEmployeeIdentity?> GetApprovedEmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId && x.OnboardingStatus == OnboardingStatus.Approved, ct);
        if (employee?.ApprovedOnboardingDecisionId is null) return null;
        var approval = await db.EmployeeOnboardingDecisions.Include(x => x.Submission).SingleOrDefaultAsync(x =>
            x.Id == employee.ApprovedOnboardingDecisionId && x.EmployeeId == employeeId && x.Decision == OnboardingDecision.Approve, ct);
        return approval is null ? null : new(employee, approval);
    }
    public Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken ct) => db.UserAccounts
        .Include(x => x.Employee).Include(x => x.ApprovalDecision).ThenInclude(x => x.Submission)
        .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
    public async Task<IReadOnlyList<LoginPhoneBinding>> GetBindingsAsync(Guid accountId, CancellationToken ct) =>
        await db.Set<LoginPhoneBinding>().Include(x => x.LoginPhoneChange).Where(x => x.UserAccountId == accountId).ToListAsync(ct);
    public Task<bool> IsPhoneBoundAsync(string phone, CancellationToken ct) =>
        db.Set<LoginPhoneBinding>().AnyAsync(x => x.PhoneNumber == phone && x.RevokedAt == null, ct);
    public void AddAccount(UserAccount account) => db.Add(account);
    public void AddBinding(LoginPhoneBinding binding) => db.Add(binding);
}
