using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed class PayrollFinanceSetupService(IPayrollFinanceRepository repository, SensitiveComplianceOperation? operation = null,
    PayrollFinanceOperation? writes = null, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly SensitiveComplianceOperation operation = operation!;
    private readonly PayrollFinanceOperation writes = writes!;
    public const string Configure = "payroll.accounting.configure";
    public const string Approve = "payroll.accounting.configure.approve";
    public Task<PayrollFinanceSetupResponse> GetAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ExecuteAsync("payroll.accounting.accrue", "FinanceSetupRead", "PayrollFinanceSetup", "all", context,
            async (_, token) => new PayrollFinanceSetupResponse((await repository.AccountsAsync(token)).Select(Map).ToArray(),
                (await repository.PeriodsAsync(token)).Select(Map).ToArray(), (await repository.ProfilesAsync(token)).Select(Map).ToArray()), ct);

    public Task<AccountResponse> CreateAccountAsync(AccountRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Configure, "payroll.account.create", "accounts", key, request, request.Reason, context, async (_, token) =>
        {
            var account = new PayrollLedgerAccount(request.Code, request.Name, request.Kind);
            if (await repository.AccountAsync(account.Code, token) is not null) throw Conflict("payroll_account_exists");
            repository.Add(account); await repository.SaveChangesAsync(token); return Map(account);
        }, ct);

    public Task<AccountResponse> SetAccountStatusAsync(Guid id, AccountStatusRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Configure, "payroll.account.status", id.ToString(), key, request, request.Reason, context, async (_, token) =>
        {
            var account = await repository.AccountAsync(id, token) ?? throw Missing();
            ExpectVersion(repository, account, account.RowVersion, request.ExpectedVersion); account.SetActive(request.IsActive);
            await repository.SaveChangesAsync(token); return Map(account);
        }, ct);

    public Task<AccountingPeriodResponse> CreatePeriodAsync(AccountingPeriodRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Configure, "payroll.period.create", "periods", key, request, request.Reason, context, async (actor, token) =>
        {
            var period = new PayrollAccountingPeriod(request.PeriodStart, request.PeriodEndExclusive, request.Currency, actor.AccountId.ToString(), clock.GetUtcNow());
            if (await repository.PeriodOverlapAsync(period.PeriodStart, period.PeriodEndExclusive, period.Currency, token)) throw Conflict("payroll_accounting_period_overlap");
            repository.Add(period); await repository.SaveChangesAsync(token); return Map(period);
        }, ct);

    public Task<AccountingPeriodResponse> DecidePeriodAsync(Guid id, FinancialDecisionRequest request, bool close, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Approve, close ? "payroll.period.close" : "payroll.period.approve", id.ToString(), key, request, request.Reason, context, async (actor, token) =>
        {
            var period = await repository.PeriodAsync(id, token) ?? throw Missing();
            ExpectVersion(repository, period, period.RowVersion, request.ExpectedVersion);
            if (close) period.Close(actor.AccountId.ToString(), clock.GetUtcNow()); else period.Approve(actor.AccountId.ToString(), clock.GetUtcNow());
            await repository.SaveChangesAsync(token); return Map(period);
        }, ct);

    public Task<PostingProfileResponse> CreateProfileAsync(PostingProfileRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Configure, "payroll.profile.create", "profiles", key, request, request.Reason, context, async (actor, token) =>
        {
            var rules = new PayrollPostingRules(request.SalaryExpenseAccountCode, request.SalaryPayableAccountCode, request.Deductions, request.Statutory);
            await ValidateRulesAsync(rules, token);
            var profile = new PayrollPostingProfile(request.Name, request.Currency, request.EffectiveFrom, request.EffectiveToExclusive,
                JsonSerializer.Serialize(rules), actor.AccountId.ToString(), clock.GetUtcNow());
            repository.Add(profile); await repository.SaveChangesAsync(token); return Map(profile);
        }, ct);

    public Task<PostingProfileResponse> ApproveProfileAsync(Guid id, FinancialDecisionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Approve, "payroll.profile.approve", id.ToString(), key, request, request.Reason, context, async (actor, token) =>
        {
            var profile = await repository.ProfileAsync(id, token) ?? throw Missing();
            ExpectVersion(repository, profile, profile.RowVersion, request.ExpectedVersion);
            await ValidateRulesAsync(Rules(profile), token); profile.Approve(actor.AccountId.ToString(), clock.GetUtcNow());
            await repository.SaveChangesAsync(token); return Map(profile);
        }, ct);

    public async Task<(PayrollAccountingPeriod Period, PayrollPostingProfile Profile, PayrollPostingRules Rules)> ResolvePostingAsync(
        Guid? profileId, DateOnly? postingDate, string currency, DateOnly sourceStart, DateOnly sourceEnd, CancellationToken ct)
    {
        if (profileId is null || profileId == Guid.Empty || postingDate is null) throw Conflict("payroll_posting_configuration_required");
        var period = await RequirePeriodAsync(postingDate.Value, currency, ct);
        if (sourceStart < period.PeriodStart || sourceEnd > period.PeriodEndExclusive) throw Conflict("payroll_source_outside_accounting_period");
        var profile = await repository.ProfileAsync(profileId.Value, ct) ?? throw Missing();
        if (profile.State != PayrollFinanceApprovalState.Approved || profile.ApprovedByUserId == profile.CreatedByUserId ||
            profile.Currency != currency || profile.EffectiveFrom > sourceStart ||
            (profile.EffectiveToExclusive.HasValue && profile.EffectiveToExclusive.Value < sourceEnd)) throw Conflict("payroll_posting_profile_not_applicable");
        var rules = Rules(profile); await ValidateRulesAsync(rules, ct);
        return (period, profile, rules);
    }

    public async Task<PayrollAccountingPeriod> RequirePeriodAsync(DateOnly date, string currency, CancellationToken ct)
    {
        var period = await repository.PeriodForDateAsync(date, currency, ct);
        if (period is null || period.State != PayrollAccountingPeriodState.Open || period.ApprovedAt is null ||
            period.ApprovedByUserId == period.CreatedByUserId) throw Conflict("payroll_accounting_period_not_open");
        return period;
    }

    public async Task RequireAccountAsync(string code, CancellationToken ct, params PayrollAccountKind[] kinds)
    {
        var account = await repository.AccountAsync(code, ct);
        if (account is null || !account.IsActive || !kinds.Contains(account.Kind)) throw Conflict("payroll_account_invalid_or_inactive");
    }

    private async Task ValidateRulesAsync(PayrollPostingRules rules, CancellationToken ct)
    {
        if (rules.Deductions is null || rules.Statutory is null || rules.Deductions.Count > 30 || rules.Statutory.Count > 3 ||
            rules.Deductions.Select(x => x.Component).Distinct(StringComparer.Ordinal).Count() != rules.Deductions.Count ||
            rules.Statutory.Select(x => x.Scheme).Distinct().Count() != rules.Statutory.Count) throw Conflict("payroll_mapping_invalid");
        await RequireAccountAsync(rules.SalaryExpenseAccountCode, ct, PayrollAccountKind.Expense);
        await RequireAccountAsync(rules.SalaryPayableAccountCode, ct, PayrollAccountKind.Liability);
        foreach (var row in rules.Deductions)
        {
            if (!Enum.TryParse<PayrollComponentKind>(row.Component, out var component) || !Enum.IsDefined(component) || row.Component == "StatutoryEmployeeDeduction")
                throw Conflict("payroll_deduction_mapping_invalid");
            await RequireAccountAsync(row.CreditAccountCode, ct, PayrollAccountKind.Liability, PayrollAccountKind.Asset);
            if (row.CreditAccountCode == rules.SalaryPayableAccountCode) throw Conflict("payroll_deduction_must_not_credit_salary_payable");
        }
        foreach (var row in rules.Statutory)
        {
            if (!Enum.IsDefined(row.Scheme)) throw Conflict("payroll_statutory_mapping_invalid");
            await RequireAccountAsync(row.EmployerExpenseAccountCode, ct, PayrollAccountKind.Expense);
            await RequireAccountAsync(row.PayableAccountCode, ct, PayrollAccountKind.Liability);
            if (row.PayableAccountCode == rules.SalaryPayableAccountCode) throw Conflict("payroll_statutory_must_not_credit_salary_payable");
        }
    }

    public static void ExpectVersion(IPayrollFinanceRepository repository, object entity, byte[] current, string expected)
    {
        byte[] bytes; try { bytes = Convert.FromBase64String(expected); } catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw Conflict("invalid_financial_version"); }
        if (bytes.Length != 8 || !bytes.SequenceEqual(current)) throw Conflict("financial_version_conflict");
        repository.ExpectVersion(entity, bytes);
    }
    public static PayrollPostingRules Rules(PayrollPostingProfile profile) => JsonSerializer.Deserialize<PayrollPostingRules>(profile.RulesJson) ?? throw Conflict("payroll_mapping_invalid");
    public static AccountResponse Map(PayrollLedgerAccount x) => new(x.Id, x.Code, x.Name, x.Kind.ToString(), x.IsActive, Convert.ToBase64String(x.RowVersion));
    public static AccountingPeriodResponse Map(PayrollAccountingPeriod x) => new(x.Id, x.PeriodStart, x.PeriodEndExclusive, x.Currency, x.State.ToString(), x.CreatedByUserId, x.ApprovedByUserId, Convert.ToBase64String(x.RowVersion));
    public static PostingProfileResponse Map(PayrollPostingProfile x)
    {
        var r = Rules(x); return new(x.Id, x.Name, x.Currency, x.EffectiveFrom, x.EffectiveToExclusive, r.SalaryExpenseAccountCode, r.SalaryPayableAccountCode,
            r.Deductions, r.Statutory, x.State.ToString(), x.CreatedByUserId, x.ApprovedByUserId, Convert.ToBase64String(x.RowVersion));
    }
    private static Exception Conflict(string code) => AttendanceOperation.Conflict(code);
    private static Exception Missing() => AttendanceOperation.Missing("payroll_finance_configuration_not_found");
}
