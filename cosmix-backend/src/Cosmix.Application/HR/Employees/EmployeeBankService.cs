using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeBankService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeBankRepository repository, IEmployeeUpdateRepository updates, EmployeeHiringService forms, IPrivateFileStore files)
{
    private static readonly string[] ReadPermissions = [EmployeePermissions.Read, EmployeeUpdatePermissions.BankRead];
    private static readonly string[] VerifyPermissions = [EmployeePermissions.Read, EmployeeUpdatePermissions.BankRead, EmployeeBankPermissions.Verify];

    public Task<EmployeeBankStatus> CurrentAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<EmployeeBankStatus>(ReadPermissions, "hr.employee.bank.verification.read", employeeId, context, async (_, token) =>
        {
            var employee = await EmployeeAsync(employeeId, token);
            var today = forms.Today();
            var bank = await repository.CurrentBankAsync(employeeId, today, token) ?? throw EmployeeOperation.Missing();
            if (bank.EffectiveTo <= today) throw EmployeeOperation.Missing();
            await ConsistentAsync(employeeId, bank.Id, token);
            var latest = await repository.LatestAsync(employeeId, bank.Id, token);
            return new(employeeId, bank.Id, bank.Revision, bank.MaskedAccountIdentifier, latest?.IsVerified ?? bank.IsVerified,
                latest?.Version ?? 0, latest?.RecordedByUserId ?? bank.VerifiedByUserId, latest?.RecordedAt ?? bank.VerifiedAt,
                EmployeeHiringService.Version(employee));
        }, ct);

    public Task<EmployeeWriteResult> VerifyAsync(Guid employeeId, Guid bankDetailsId, EmployeeBankTransitionRequest input,
        string key, AuthRequestContext context, CancellationToken ct) => TransitionAsync(employeeId, bankDetailsId, true, input, key, context, ct);
    public Task<EmployeeWriteResult> RevokeAsync(Guid employeeId, Guid bankDetailsId, EmployeeBankTransitionRequest input,
        string key, AuthRequestContext context, CancellationToken ct) => TransitionAsync(employeeId, bankDetailsId, false, input, key, context, ct);

    private Task<EmployeeWriteResult> TransitionAsync(Guid employeeId, Guid bankDetailsId, bool verify, EmployeeBankTransitionRequest input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(VerifyPermissions, verify ? "hr.employee.bank.verified" : "hr.employee.bank.verification.revoked",
            employeeId, key, new { bankDetailsId, input }, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                EmployeeUpdateValidation.Reason(input.Reason);
                if (string.IsNullOrWhiteSpace(input.Proof) || input.Proof.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
                    throw EmployeeOperation.Invalid("invalid_employee_bank_proof");
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var bank = await BankAsync(employeeId, bankDetailsId, token);
                var today = forms.Today();
                var current = await repository.CurrentBankAsync(employeeId, today, token);
                if (current?.Id != bank.Id || bank.EffectiveTo <= today) throw EmployeeOperation.Conflict("employee_bank_revision_not_current");
                await ConsistentAsync(employeeId, bank.Id, token);
                var previous = await repository.LatestAsync(employeeId, bank.Id, token);
                if (input.ExpectedVerificationVersion != (previous?.Version ?? 0)) throw EmployeeOperation.Conflict("employee_bank_verification_version_conflict");
                if (verify == (previous?.IsVerified ?? bank.IsVerified)) throw EmployeeOperation.Conflict("employee_bank_verification_has_no_change");
                if (actor.Now < bank.RecordedAt || (previous is not null && actor.Now <= previous.RecordedAt))
                    throw EmployeeOperation.Conflict("employee_bank_verification_time_conflict");
                var version = checked((previous?.Version ?? 0) + 1);
                var evidence = new BankEvidence(1, employeeId, bank.Id, version, verify, actor.Identity.AccountId.ToString(),
                    actor.Now, input.Reason.Trim(), input.Proof.Trim());
                var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
                PrivateFileReference reference;
                try { reference = await files.WriteAsync(bytes, Purpose(employeeId, bank.Id), token); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
                var encoded = JsonSerializer.Serialize(reference);
                if (encoded.Length > 500) throw EmployeeOperation.Conflict("employee_bank_evidence_reference_invalid");
                var record = new EmployeeBankVerification(bank, previous, verify, actor.Identity.AccountId.ToString(), actor.Now, encoded);
                repository.AddVerification(record);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { BankVerificationId = record.Id };
            }, ct, authorizeResource: async (actor, token) =>
            {
                await EmployeeAsync(employeeId, token);
                await BankAsync(employeeId, bankDetailsId, token);
                if (verify && !await operation.IsActiveEmploymentAsync(employeeId, actor.Now, token))
                    throw EmployeeOperation.Conflict("employee_bank_verification_requires_active_employment");
            });

    public Task<PagedResult<EmployeeBankVerificationSummary>> HistoryAsync(Guid employeeId, Guid bankDetailsId, EmployeeBankHistoryQuery input,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(ReadPermissions, "hr.employee.bank.verification.history",
            employeeId, context, async (_, token) =>
            {
                OrganizationOperation.Validate(input);
                await EmployeeAsync(employeeId, token); await BankAsync(employeeId, bankDetailsId, token);
                await ConsistentAsync(employeeId, bankDetailsId, token);
                return await repository.HistoryAsync(employeeId, bankDetailsId, input, token);
            }, ct, auditTarget: _ => $"bank:{bankDetailsId:N}");

    public Task<EmployeeBankVerificationEvidence> EvidenceAsync(Guid employeeId, Guid bankDetailsId, Guid verificationId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync<EmployeeBankVerificationEvidence>(VerifyPermissions, "hr.employee.bank.verification.evidence",
            employeeId, context, async (_, token) =>
            {
                await EmployeeAsync(employeeId, token); await BankAsync(employeeId, bankDetailsId, token);
                var record = await repository.FindVerificationAsync(employeeId, bankDetailsId, verificationId, token) ?? throw EmployeeOperation.Missing();
                BankEvidence evidence;
                try
                {
                    var reference = JsonSerializer.Deserialize<PrivateFileReference>(record.PrivateEvidenceReference)
                        ?? throw EmployeeOperation.Conflict("employee_bank_evidence_unavailable");
                    var bytes = await files.ReadAsync(reference, Purpose(employeeId, bankDetailsId), token);
                    try { evidence = JsonSerializer.Deserialize<BankEvidence>(bytes) ?? throw EmployeeOperation.Conflict("employee_bank_evidence_unavailable"); }
                    finally { CryptographicOperations.ZeroMemory(bytes); }
                }
                catch (JsonException) { throw EmployeeOperation.Conflict("employee_bank_evidence_unavailable"); }
                if (evidence.SchemaVersion != 1 || evidence.EmployeeId != employeeId || evidence.BankDetailsId != bankDetailsId ||
                    evidence.Version != record.Version || evidence.IsVerified != record.IsVerified || evidence.Actor != record.RecordedByUserId ||
                    evidence.RecordedAt != record.RecordedAt)
                    throw EmployeeOperation.Conflict("employee_bank_evidence_unavailable");
                return new(Map(record), evidence.Reason, evidence.Proof);
            }, ct, auditTarget: _ => $"bank:{bankDetailsId:N}/verification:{verificationId:N}");

    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
    {
        var employee = await hiring.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
        if (employee.OnboardingStatus != OnboardingStatus.Approved || !employee.ApprovedOnboardingDecisionId.HasValue)
            throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }
    private async Task<EmployeeBankDetails> BankAsync(Guid employeeId, Guid bankId, CancellationToken ct) =>
        await repository.FindBankAsync(employeeId, bankId, ct) ?? throw EmployeeOperation.Missing();
    private async Task ConsistentAsync(Guid employeeId, Guid bankId, CancellationToken ct)
    {
        if (await updates.HasConflictingHistoryAsync(employeeId, EmployeeChangeSection.Bank, ct) ||
            await repository.HasConflictingHistoryAsync(employeeId, bankId, ct))
            throw EmployeeOperation.Conflict("employee_bank_history_conflict");
    }
    private static string Purpose(Guid employeeId, Guid bankId) => EmployeeHiringService.Purpose(employeeId, $"bank-verification/{bankId:N}");
    private static EmployeeBankVerificationSummary Map(EmployeeBankVerification x) => new(x.Id, x.EmployeeId, x.EmployeeBankDetailsId,
        x.Version, x.IsVerified, x.RecordedByUserId, x.RecordedAt);
    private sealed record BankEvidence(int SchemaVersion, Guid EmployeeId, Guid BankDetailsId, int Version, bool IsVerified,
        string Actor, DateTimeOffset RecordedAt, string Reason, string Proof);
}
