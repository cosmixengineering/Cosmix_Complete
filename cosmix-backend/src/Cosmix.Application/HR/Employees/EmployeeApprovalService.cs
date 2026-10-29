using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeDecisionRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    OnboardingDecision Decision, [StringLength(2000)] string? Reason = null, Guid? InitialSiteId = null);

public sealed record EmployeeDecisionHistory(Guid Id, Guid SubmissionId, OnboardingDecision Decision,
    Guid? ActorAccountId, Guid? ActorRoleId, string? Reason, DateTimeOffset DecidedAt);

public sealed record EmployeeAccessSetup(Guid EmployeeId, AccessAccountResponse? Account,
    bool MobileAccessRequested, bool InitialAccessAssigned, bool MobilePhoneEnrolled);

/// <summary>One transaction decides the frozen submission, materializes employment and creates a dormant identity.</summary>
public sealed class EmployeeApprovalService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeApprovalRepository approvals, EmployeeHiringService forms, EmployeeApprovalMaterializer materializer,
    IEmployeeMobileProvisioningRepository identities)
{
    public Task<EmployeeWriteResult> DecideAsync(Guid employeeId, Guid submissionId, EmployeeDecisionRequest input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Review, EmployeePermissions.Read, EmployeePermissions.DocumentsRead],
            "hr.employee.submission.decided", employeeId, key, new { submissionId, input }, context,
            async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                if (!Enum.IsDefined(input.Decision) || input.InitialSiteId == Guid.Empty ||
                    input.Reason?.Any(char.IsControl) == true ||
                    (input.Decision != OnboardingDecision.Approve && (string.IsNullOrWhiteSpace(input.Reason) || input.InitialSiteId.HasValue)))
                    throw EmployeeOperation.Invalid("invalid_employee_decision");
                var employee = await hiring.FindAsync(employeeId, token) ?? throw EmployeeOperation.Missing();
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                var submission = await hiring.FindSubmissionAsync(employeeId, submissionId, token) ?? throw EmployeeOperation.Missing();
                if (employee.OnboardingStatus != OnboardingStatus.PendingApproval || employee.Status != EmployeeStatus.PendingApproval ||
                    submission.Version != employee.SubmittedVersion || submission.DraftRevision != employee.DraftRevision)
                    throw EmployeeOperation.Conflict("employee_submission_not_current");
                if (submission.SubmittedByUserId == actor.Identity.AccountId || employee.Id == actor.Identity.EmployeeId)
                    throw EmployeeOperation.Denied();

                EmployeeHiringService.HiringSnapshot? snapshot = null;
                if (input.Decision == OnboardingDecision.Approve)
                {
                    snapshot = (await forms.SubmissionAsync(employeeId, submissionId, token)).Snapshot;
                    var documents = await hiring.DocumentsAsync(employeeId, token);
                    foreach (var frozen in snapshot.Documents)
                    {
                        var current = documents.SingleOrDefault(d => d.Id == frozen.Id);
                        if (current is null || Convert.ToBase64String(current.RowVersion) != frozen.Version)
                            throw EmployeeOperation.Conflict("employee_documents_changed_review_required");
                    }
                    snapshot = snapshot with { Documents = documents.Where(d => snapshot.Documents.Any(f => f.Id == d.Id))
                        .Select(d => EmployeeHiringService.MapDocument(d, forms.Today())).ToArray() };
                    var validation = await forms.ValidateSubmissionCoreAsync(employeeId, snapshot, token, revalidateTaxonomy: true);
                    if (!validation.CanSubmit) throw EmployeeOperation.Conflict("employee_form_incomplete_or_documents_unavailable");
                    await hiring.ValidateUniqueAsync(employee, token);
                    await materializer.ValidateAsync(employee, snapshot.Profile, snapshot.Financial, snapshot.Medical, input.InitialSiteId, token);
                    if (await identities.FindAccountAsync(employeeId, token) is not null)
                        throw EmployeeOperation.Conflict("employee_account_already_exists");
                }

                var decision = employee.DecideSubmission(submission, input.Decision, actor.Identity.AccountId,
                    actor.RoleId, input.Reason, actor.Now);
                approvals.AddDecision(decision);
                // The profile guard freezes Pending submissions. This save ends that state, but the outer
                // EmployeeOperation transaction still rolls everything back if any subsequent step fails.
                await hiring.SaveChangesAsync(token);
                UserAccount? account = null;
                if (snapshot is not null)
                {
                    await materializer.MaterializeAsync(employee, snapshot.Profile, snapshot.Financial, snapshot.Medical,
                        input.InitialSiteId, actor.Identity.AccountId.ToString(), actor.Now, token);
                    account = new UserAccount(employee, decision, "employee:" + employeeId.ToString("N"), actor.Now);
                    identities.AddAccount(account);
                    await hiring.SaveChangesAsync(token);
                }
                return EmployeeHiringService.Result(employee) with
                { SubmissionId = submissionId, DecisionId = decision.Id, AccountId = account?.Id };
            }, ct, adminReview: true, authorizeResource: async (actor, token) =>
            {
                if (input.Decision != OnboardingDecision.Approve) return;
                var (submission, snapshot) = await forms.SubmissionAsync(employeeId, submissionId, token);
                if (submission.Id != submissionId || submission.EmployeeId != employeeId) throw EmployeeOperation.Missing();
                if (snapshot.Financial is not null)
                    await operation.RequirePermissionsAsync(actor, [EmployeePermissions.FinancialRead, EmployeePermissions.FinancialEdit], token);
                if (snapshot.Financial?.Bank is not null)
                    await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.BankRead, EmployeeUpdatePermissions.BankRequest], token);
                if (snapshot.Medical is not null)
                    await operation.RequirePermissionsAsync(actor, [EmployeePermissions.MedicalRead, EmployeePermissions.MedicalEdit], token);
            });

    public Task<IReadOnlyList<EmployeeDecisionHistory>> HistoryAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<EmployeeDecisionHistory>>([EmployeePermissions.Read], "hr.employee.decisions.read", employeeId,
            context, async (_, token) =>
            {
                if (await hiring.FindAsync(employeeId, token) is null) throw EmployeeOperation.Missing();
                return (await approvals.DecisionsAsync(employeeId, token)).Select(d => new EmployeeDecisionHistory(
                    d.Id, d.SubmissionId, d.Decision, d.ActorUserId, d.ActorRoleId, d.Reason, d.DecidedAt)).ToArray();
            }, ct);

    public Task<EmployeeAccessSetup> AccessSetupAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Review, AccessPermissionCatalog.AccountsRead], "hr.employee.access_setup.read", employeeId,
            context, async (_, token) =>
            {
                var employee = await hiring.FindAsync(employeeId, token) ?? throw EmployeeOperation.Missing();
                var account = await identities.FindAccountAsync(employeeId, token);
                var phoneEnrolled = account is not null && (await identities.GetBindingsAsync(account.Id, token)).Any(b => b.RevokedAt is null);
                return new EmployeeAccessSetup(employeeId, account is null ? null : new(account.Id, employeeId, account.Status,
                    account.MobileAccessEnabled, account.WebAccessEnabled, account.MfaRequired, Convert.ToBase64String(account.RowVersion), account.AuthenticatorRequired),
                    employee.MobileAccessRequested, account?.InitialRoleAssignmentId is not null, phoneEnrolled);
            }, ct, adminReview: true);
}
