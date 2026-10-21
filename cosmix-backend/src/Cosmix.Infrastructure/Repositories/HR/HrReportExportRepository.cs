using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Reports;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class HrReportExportRepository(CosmixDbContext db, IAttendanceReportRepository attendance) : IHrReportExportRepository
{
    public Task<ReportExportJob?> FindAsync(Guid owner, Guid id, CancellationToken ct) => db.ReportExportJobs
        .SingleOrDefaultAsync(j => j.Id == id && j.RequestedByUserId == owner.ToString() && j.ReportType == HrReportExportRules.ReportType, ct);
    public Task<ReportExportJob?> FindWorkerAsync(Guid id, CancellationToken ct) => db.ReportExportJobs
        .SingleOrDefaultAsync(j => j.Id == id && j.ReportType == HrReportExportRules.ReportType, ct);
    public Task<StoredFile?> FindFileAsync(Guid id, CancellationToken ct) => attendance.FindFileAsync(id, ct);
    public Task LockAuthorizationAsync(CancellationToken ct) => attendance.LockAuthorizationAsync(ct);

    public async Task<T> ReadSnapshotAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("HR report preparation requires a clean unit of work.");
        db.ChangeTracker.Clear();
        try
        {
            // Stable multipage source reads, without the global authentication application lock.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action(ct);
            if (db.ChangeTracker.HasChanges())
                throw new InvalidOperationException("HR report preparation must not change business rows.");
            await transaction.CommitAsync(ct);
            return result;
        }
        finally { db.ChangeTracker.Clear(); }
    }
    public void Add(ReportExportJob job) => attendance.Add(job);
    public void Add(StoredFile file) => attendance.Add(file);
}
