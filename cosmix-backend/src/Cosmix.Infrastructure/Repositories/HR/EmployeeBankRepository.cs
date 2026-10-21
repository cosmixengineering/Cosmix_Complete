using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeBankRepository(CosmixDbContext db) : IEmployeeBankRepository
{
    public Task<EmployeeBankDetails?> FindBankAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct) =>
        db.EmployeeBankDetails.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == bankDetailsId, ct);
    public Task<EmployeeBankDetails?> CurrentBankAsync(Guid employeeId, DateOnly asOf, CancellationToken ct) =>
        db.EmployeeBankDetails.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= asOf)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public Task<EmployeeBankVerification?> LatestAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct) =>
        db.Set<EmployeeBankVerification>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EmployeeBankDetailsId == bankDetailsId)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    public Task<EmployeeBankVerification?> FindVerificationAsync(Guid employeeId, Guid bankDetailsId, Guid verificationId, CancellationToken ct) =>
        db.Set<EmployeeBankVerification>().AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId &&
            x.EmployeeBankDetailsId == bankDetailsId && x.Id == verificationId, ct);
    public async Task<PagedResult<EmployeeBankVerificationSummary>> HistoryAsync(Guid employeeId, Guid bankDetailsId,
        EmployeeBankHistoryQuery query, CancellationToken ct)
    {
        var rows = db.Set<EmployeeBankVerification>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EmployeeBankDetailsId == bankDetailsId);
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(x => x.Version).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new EmployeeBankVerificationSummary(x.Id, x.EmployeeId, x.EmployeeBankDetailsId, x.Version,
                x.IsVerified, x.RecordedByUserId, x.RecordedAt)).ToArrayAsync(ct);
        return new(page, count, query.Page, query.PageSize);
    }
    public Task<bool> HasConflictingHistoryAsync(Guid employeeId, Guid bankDetailsId, CancellationToken ct)
    {
        var rows = db.Set<EmployeeBankVerification>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EmployeeBankDetailsId == bankDetailsId);
        return rows.AnyAsync(row => row.Version < 1 ||
            (row.Version == 1 && row.PreviousVerificationId != null) ||
            (row.Version > 1 && !rows.Any(previous => previous.Id == row.PreviousVerificationId && previous.Version == row.Version - 1 &&
                previous.RecordedAt < row.RecordedAt && previous.IsVerified != row.IsVerified)), ct);
    }
    public void AddVerification(EmployeeBankVerification verification) => db.Set<EmployeeBankVerification>().Add(verification);
}
