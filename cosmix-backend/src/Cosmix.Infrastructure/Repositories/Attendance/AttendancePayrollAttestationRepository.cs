using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendancePayrollAttestationRepository(CosmixDbContext db) : IAttendancePayrollAttestationRepository
{
    public async Task<IReadOnlyList<AttendancePayrollAttestation>> HistoryAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct) =>
        await db.Set<AttendancePayrollAttestation>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.PeriodStart == monthStart)
            .OrderBy(x => x.Step).Take(4).ToArrayAsync(ct);
    public Task<bool> ReceiptExistsAsync(string externalReceiptKey, int step, CancellationToken ct) =>
        db.Set<AttendancePayrollAttestation>().AsNoTracking()
            .AnyAsync(x => x.ExternalReceiptKey == externalReceiptKey && x.Step == step, ct);
    public void Add(AttendancePayrollAttestation attestation)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stage payroll attestations inside AttendanceOperation's transaction.");
        db.Set<AttendancePayrollAttestation>().Add(attestation);
    }
}
