using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;

namespace Cosmix.Application.Finance.Costing;

public sealed record PayrollCostSource(Guid BatchId, string Fingerprint, DateOnly PeriodStart, string Currency,
    IReadOnlyList<PayrollCostBasis> Employees);

/// <summary>Employee costs reconcile to committed salary and employer expense, excluding salary payments.</summary>
public sealed class PayrollCostSourceReader(IPayrollFrozenBatchReader frozen, IPayrollAccountingRepository accounting,
    IPayrollFinanceRepository finance, IPayrollHandoffRepository? employees = null)
{
    public async Task<PayrollCostSource> ReadAsync(Guid batchId, CancellationToken ct)
    {
        var salary = await frozen.ReadAsync(batchId, ct);
        var obligation = await accounting.GetObligationByBatchIdAsync(batchId, ct)
            ?? throw AttendanceOperation.Conflict("payroll_cost_accrual_required");
        PayrollAccountingService.ValidateObligationSource(obligation, salary);
        var journals = (await accounting.GetJournalEntriesForObligationAsync(obligation.Id, ct))
            .Where(x => x.Kind is JournalEntryKind.SalaryAccrual or JournalEntryKind.StatutoryContribution).ToArray();
        if (!journals.Any(x => x.Kind == JournalEntryKind.SalaryAccrual)) throw AttendanceOperation.Conflict("payroll_cost_accrual_required");
        var accounts = (await finance.AccountsAsync(ct)).ToDictionary(x => x.Code, StringComparer.Ordinal);
        if (journals.Any(x => !accounts.TryGetValue(x.DebitAccountCode, out var debit) || debit.Kind != PayrollAccountKind.Expense ||
            !accounts.ContainsKey(x.CreditAccountCode) || x.Currency != salary.Currency))
            throw AttendanceOperation.Conflict("payroll_cost_journal_invalid");
        var amounts = salary.Lines.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.Where(line => line.Amount > 0).Sum(line => line.Amount));
        foreach (var line in salary.Lines.Where(x => x.Amount < 0))
        {
            var key = $"ACCRUAL-DED-{obligation.Id:N}-{line.Id:N}";
            var matching = journals.Where(x => x.SourceKey == key && x.Kind == JournalEntryKind.SalaryAccrual).ToArray();
            if (matching.Length != 1 || matching[0].Amount != -line.Amount) throw AttendanceOperation.Conflict("payroll_cost_journal_invalid");
            if (accounts[matching[0].CreditAccountCode].Kind == PayrollAccountKind.Expense) amounts[line.EmployeeId] += line.Amount;
        }
        var statutory = new Dictionary<Guid, StatutoryContribution>();
        foreach (var line in salary.Lines.Where(x => x.SourceType == nameof(StatutoryContributionEntry)))
        {
            var entry = await finance.EntryAsync(line.SourceId, ct) ?? throw AttendanceOperation.Conflict("payroll_cost_statutory_missing");
            if (entry.EmployeeId != line.EmployeeId || entry.EmployeeContributionAmount != -line.Amount)
                throw AttendanceOperation.Conflict("payroll_cost_statutory_missing");
            if (!statutory.TryGetValue(entry.ContributionId, out var contribution))
            {
                contribution = await accounting.GetStatutoryContributionByIdAsync(entry.ContributionId, ct)
                    ?? throw AttendanceOperation.Conflict("payroll_cost_statutory_missing");
                statutory.Add(contribution.Id, contribution);
                var postings = await accounting.ContributionPostingsAsync(contribution.Id, ct);
                var relevant = contribution.Entries.Where(x => amounts.ContainsKey(x.EmployeeId)).ToArray();
                if (relevant.Any(x => !postings.Any(p => p.EntryId == x.Id && p.ObligationId == obligation.Id)) &&
                    !(contribution.State == ContributionState.Posted && contribution.ObligationId == obligation.Id))
                    throw AttendanceOperation.Conflict("payroll_cost_statutory_posting_required");
                var employer = relevant.Sum(x => x.EmployerContributionAmount);
                if (journals.Where(x => x.Kind == JournalEntryKind.StatutoryContribution && x.StatutoryContributionId == contribution.Id).Sum(x => x.Amount) != employer)
                    throw AttendanceOperation.Conflict("payroll_cost_statutory_posting_required");
            }
            amounts[line.EmployeeId] += entry.EmployerContributionAmount;
        }
        var expense = journals.Sum(x => x.Amount - (accounts[x.CreditAccountCode].Kind == PayrollAccountKind.Expense ? x.Amount : 0));
        if (amounts.Values.Any(x => x < 0) || amounts.Values.Sum() != expense)
            throw AttendanceOperation.Conflict("payroll_cost_expense_mismatch");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            salary.Fingerprint, obligation.Id, Journals = journals.OrderBy(x => x.Id).Select(x => new { x.Id, x.SourceKey, x.Amount, x.DebitAccountCode, x.CreditAccountCode }),
            Employees = amounts.OrderBy(x => x.Key)
        })));
        var names = employees is null ? new Dictionary<Guid, string>() : await employees.EmployeeNamesAsync(amounts.Keys.ToArray(), ct);
        return new(batchId, fingerprint, salary.PeriodStart, salary.Currency,
            amounts.OrderBy(x => x.Key).Select(x => new PayrollCostBasis(x.Key, x.Value, salary.Currency)
            { EmployeeName = names.GetValueOrDefault(x.Key) }).ToArray());
    }
}
