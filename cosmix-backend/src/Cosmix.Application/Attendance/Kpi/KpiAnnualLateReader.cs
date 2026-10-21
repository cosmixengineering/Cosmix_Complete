using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiAnnualLateReader(IKpiRepository repository, Func<Guid, DateOnly, CancellationToken, Task<KpiSource>> sourceReader,
    AttendanceOperation operation) : IWorkTreatmentAnnualLateReader
{
    public async Task<WorkAnnualLateEvidence?> ReadAsync(Guid employee, Guid sourceId, CancellationToken ct)
    {
        var row = await repository.FindAsync(employee, sourceId, ct);
        if (row is null) return null;
        if (row.Request.RequestType != AttendanceKpiService.RequestType || row.Request.SubjectId != employee ||
            row.Request.State != ApprovalState.Pending || row.Decisions.Count != 0)
            throw AttendanceOperation.Conflict("kpi_annual_late_source_not_pending");
        var snapshot = await operation.ReadPrivateAsync<KpiRequestSnapshot>(row.Request.SnapshotPrivateReference,
            AttendanceKpiService.RequestPurpose(employee), ct);
        var current = await sourceReader(employee, snapshot.AsOf, ct);
        if (snapshot.SchemaVersion != 1 || snapshot.EmployeeId != employee || snapshot.Source.EmployeeId != employee ||
            snapshot.RequesterAccountId.ToString() != row.Request.RequesterUserId || snapshot.Source.Fingerprint != current.Fingerprint ||
            snapshot.Source.Calculation.LateOccurrences <= 120 || snapshot.Source.PolicyVersionId is null ||
            snapshot.Source.PolicyFingerprint is null || snapshot.Source.ToExclusive != snapshot.Source.From.AddYears(1))
            throw AttendanceOperation.Conflict("kpi_annual_late_source_changed");
        return new(sourceId, employee, snapshot.Source.From, snapshot.Source.ToExclusive.AddDays(-1),
            snapshot.Source.Calculation.LateOccurrences, snapshot.Source.PolicyVersionId.Value,
            snapshot.Source.PolicyFingerprint, snapshot.Source.Fingerprint, snapshot.Source.Scopes);
    }
}
