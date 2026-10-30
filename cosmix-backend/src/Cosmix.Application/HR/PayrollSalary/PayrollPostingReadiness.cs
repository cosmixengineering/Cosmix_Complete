using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.PayrollAccounting;
using System.Text.Json;

namespace Cosmix.Application.HR.PayrollSalary;

public static class PayrollPostingReadiness
{
    public static async Task<int> CountApplicableAsync(IReadOnlyList<PayrollPostingProfile> profiles,
        PayrollFinanceSetupService finance, DateOnly month, CancellationToken ct)
    {
        var count = 0;
        foreach (var profile in profiles)
        {
            ct.ThrowIfCancellationRequested();
            // The existing salary engine accepts PKR. Use exactly the same whole-period,
            // independent-approval and account compatibility rules as actual posting.
            if (!PayrollFinanceSetupService.IsProfileApplicable(profile, "PKR", month, month.AddMonths(1))) continue;
            try
            {
                await finance.ValidateRulesAsync(PayrollFinanceSetupService.Rules(profile), ct);
                count++;
            }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 409 && failure.Code is
                "payroll_mapping_invalid" or "payroll_account_invalid_or_inactive" or
                "payroll_deduction_mapping_invalid" or "payroll_contra_expense_must_use_distinct_account" or
                "payroll_deduction_must_not_credit_salary_payable" or "payroll_statutory_mapping_invalid" or
                "payroll_statutory_must_not_credit_salary_payable")
            { /* This approved profile cannot currently be used for posting. */ }
            catch (JsonException)
            { /* Invalid persisted mapping JSON is not ready configuration. */ }
        }
        return count;
    }
}
