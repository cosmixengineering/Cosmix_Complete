using Cosmix.Application.Attendance.Reports;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Reports;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceReportRepository(CosmixDbContext db) : IAttendanceReportRepository
{
    public Task<ReportExportJob?> FindAsync(Guid requesterAccountId, Guid id, CancellationToken ct) =>
        db.ReportExportJobs.SingleOrDefaultAsync(x => x.Id == id && x.RequestedByUserId == requesterAccountId.ToString() &&
            x.ReportType == AttendanceReportService.ReportType, ct);
    public Task<ReportExportJob?> FindWorkerAsync(Guid id, CancellationToken ct) =>
        db.ReportExportJobs.SingleOrDefaultAsync(x => x.Id == id && x.ReportType == AttendanceReportService.ReportType, ct);
    public Task<StoredFile?> FindFileAsync(Guid id, CancellationToken ct) => db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task LockAuthorizationAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Report generation must share the outbox transaction.");
        return db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'cosmix:authentication',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51003, 'Attendance report access is temporarily busy.', 1;
            """, ct);
    }
    public void Add(ReportExportJob job) => db.ReportExportJobs.Add(job);
    public void Add(StoredFile file) => db.StoredFiles.Add(file);
}
