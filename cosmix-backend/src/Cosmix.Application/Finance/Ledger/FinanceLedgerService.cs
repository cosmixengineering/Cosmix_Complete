using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;

namespace Cosmix.Application.Finance.Ledger;

public sealed class FinanceLedgerService(SensitiveComplianceOperation operation, IFinanceLedgerRepository repository)
{
    public const string ReadPermission = "finance.ledger.read";

    public Task<FinanceTrialBalance> TrialBalanceAsync(DateOnly from, DateOnly untilExclusive,
        AuthRequestContext context, CancellationToken ct)
    {
        FinanceTrialBalanceProjection.ValidatePeriod(from, untilExclusive);
        return operation.ExecuteAsync(ReadPermission, "finance.trial-balance.read", "FinanceLedger", $"{from:yyyy-MM-dd}/{untilExclusive:yyyy-MM-dd}",
            context, async (_, token) => FinanceTrialBalanceProjection.Calculate(from, untilExclusive,
                await repository.ThroughAsync(untilExclusive, token)), ct);
    }

    public Task<FinanceLedgerPage> LedgerAsync(DateOnly from, DateOnly untilExclusive, string? accountCode,
        string? currency, int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        FinanceTrialBalanceProjection.ValidatePeriod(from, untilExclusive);
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100 || accountCode?.Length > 50 ||
            currency is not null && (currency.Length != 3 || currency.Any(x => x is < 'A' or > 'Z')))
            throw AttendanceOperation.Invalid("finance_ledger_filter_invalid");
        return operation.ExecuteAsync(ReadPermission, "finance.ledger.read", "FinanceLedger", $"{from:yyyy-MM-dd}/{untilExclusive:yyyy-MM-dd}",
            context, (_, token) => repository.PageAsync(from, untilExclusive, accountCode, currency, page, pageSize, token), ct);
    }
}
