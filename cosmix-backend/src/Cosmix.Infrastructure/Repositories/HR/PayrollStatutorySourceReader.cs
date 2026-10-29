using Cosmix.Application.HR.PayrollAccounting;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class PayrollStatutorySourceReader(CosmixDbContext db, IPrivateFileStore files) : IPayrollStatutorySourceReader
{
    public async Task<PayrollStatutoryProjection> ProjectAsync(DateOnly periodStart, DateOnly periodEndExclusive,
        IReadOnlyList<PayrollSalaryEmployeeBasis> employees, CancellationToken ct)
    {
        if (periodStart.Day != 1 || periodEndExclusive != periodStart.AddMonths(1))
            throw new PayrollFinanceConflictException("Statutory payroll requires an exact calendar month.");
        if (employees.Select(x => x.EmployeeId).Distinct().Count() != employees.Count)
            throw new PayrollFinanceConflictException("Duplicate payroll employees.");
        var lines = new List<PayrollSalaryAdditionalLine>();
        var fingerprints = new List<object>();
        foreach (var employee in employees.OrderBy(x => x.EmployeeId))
        {
            var coverage = await db.EmployeeStatutoryCoverages.AsNoTracking().Where(x => x.EmployeeId == employee.EmployeeId &&
                x.ApprovedAt != null && x.EffectiveFrom < periodEndExclusive &&
                (x.EffectiveToExclusive == null || x.EffectiveToExclusive > periodStart)).ToArrayAsync(ct);
            if (coverage.Length != 1 || coverage[0].EffectiveFrom > periodStart || coverage[0].EffectiveToExclusive < periodEndExclusive ||
                string.IsNullOrWhiteSpace(coverage[0].ApprovedByUserId) || coverage[0].ApprovedByUserId == coverage[0].CreatedByUserId)
                throw new PayrollFinanceConflictException("Explicit independently approved statutory coverage is required for every employee and the full payroll month.");
            var basis = coverage[0];
            await StatutoryContributionService.ValidateCoverageProofAsync(basis, files, ct);
            fingerprints.Add(new { basis.Id, basis.EmployeeId, basis.Schemes, basis.ApprovedAt, basis.ApprovedByUserId, basis.EvidenceSha256, basis.PrivateProofReference });
            var entries = await db.Set<StatutoryContributionEntry>().AsNoTrackingWithIdentityResolution().Include(x => x.Contribution).ThenInclude(x => x.Entries)
                .Where(x => x.EmployeeId == employee.EmployeeId && x.Contribution.ContributionMonth == periodStart &&
                    (x.Contribution.State == ContributionState.Approved || x.Contribution.State == ContributionState.Posted)).ToArrayAsync(ct);
            var schemes = basis.ReadSchemes();
            if (entries.Length != schemes.Count || entries.Select(x => x.Contribution.Scheme).Distinct().Count() != entries.Length ||
                entries.Any(x => !schemes.Contains(x.Contribution.Scheme)))
                throw new PayrollFinanceConflictException("Actual approved statutory entries must exactly match declared employee coverage.");
            foreach (var entry in entries.OrderBy(x => x.Contribution.Scheme))
            {
                var contribution = entry.Contribution;
                PayrollFinanceErrors.Run(contribution.ValidateEntries);
                if (contribution.Currency != employee.Currency || contribution.ApprovedAt is null ||
                    string.IsNullOrWhiteSpace(contribution.ApprovedByUserId) || contribution.ApprovedByUserId == contribution.CreatedByUserId)
                    throw new PayrollFinanceConflictException("Contribution approval/currency does not match salary.");
                await StatutoryContributionService.ValidateEntryProofAsync(contribution, entry, files, ct);
                lines.Add(new(new PayrollHandoffPreviewLine(employee.EmployeeId, nameof(PayrollComponentKind.StatutoryEmployeeDeduction),
                    nameof(StatutoryContributionEntry), entry.Id, 1, -entry.EmployeeContributionAmount, contribution.Currency, null, null), entry.PrivateProofReference));
                fingerprints.Add(new { entry.Id, ContributionId = contribution.Id, contribution.Scheme, contribution.ApprovedAt, contribution.ApprovedByUserId,
                    entry.EmployeeContributionAmount, entry.EmployerContributionAmount, entry.EvidenceSha256, entry.PrivateProofReference });
            }
        }
        return new(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fingerprints))), lines);
    }
}
