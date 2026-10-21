using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class CaptureValidationEvidenceRepository(CosmixDbContext db) : ICaptureValidationEvidenceRepository
{
    internal IQueryable<RecentCaptureEvidence> RecentQuery(Guid employeeId, DateTimeOffset fromInclusiveUtc, DateTimeOffset throughInclusiveUtc)
    {
        Scope(employeeId);
        if (fromInclusiveUtc.Offset != TimeSpan.Zero || throughInclusiveUtc.Offset != TimeSpan.Zero || fromInclusiveUtc > throughInclusiveUtc)
            throw AttendanceOperation.Invalid("capture_validation_invalid_range");
        return WithAssessment(db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
                x.ReceivedAtUtc >= fromInclusiveUtc && x.ReceivedAtUtc <= throughInclusiveUtc)
            .OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.Id).Take(CaptureValidationPolicyRules.MaximumRecentRows + 1));
    }
    internal IQueryable<RecentCaptureEvidence> PunchQuery(Guid employeeId, Guid punchId)
    {
        Scope(employeeId, punchId);
        return WithAssessment(db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Id == punchId).Take(2));
    }
    internal IQueryable<CaptureValidationFinalEvidence> FinalQuery(Guid employeeId, Guid punchId)
    {
        Scope(employeeId, punchId);
        return db.Set<CaptureValidationFinalEvidence>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.RawPunchEventId == punchId).Take(2);
    }
    private IQueryable<RecentCaptureEvidence> WithAssessment(IQueryable<RawPunchEvent> raws) => raws.Select(raw =>
        new RecentCaptureEvidence(raw, db.PunchAssessments.AsNoTracking().Where(x => x.EmployeeId == raw.EmployeeId && x.RawPunchEventId == raw.Id)
            .OrderByDescending(x => x.Revision).FirstOrDefault()));
    public async Task<IReadOnlyList<RecentCaptureEvidence>> RecentAsync(Guid employeeId, DateTimeOffset fromInclusiveUtc,
        DateTimeOffset throughInclusiveUtc, CancellationToken ct)
    {
        var rows = await RecentQuery(employeeId, fromInclusiveUtc, throughInclusiveUtc).ToArrayAsync(ct);
        ValidateRows(rows, employeeId, fromInclusiveUtc, throughInclusiveUtc);
        // The overflow sentinel is preserved so the evaluator can record pending evidence.
        return rows;
    }
    public async Task<RecentCaptureEvidence?> PunchAsync(Guid employeeId, Guid punchId, CancellationToken ct)
    {
        var rows = await PunchQuery(employeeId, punchId).ToArrayAsync(ct);
        if (rows.Length > 1 || rows.Any(x => x.Raw.Id != punchId)) throw AttendanceOperation.Conflict("capture_validation_query_scope_conflict");
        ValidateRows(rows, employeeId, null, null);
        return rows.SingleOrDefault();
    }
    public async Task<CaptureValidationEvidence?> FinalAsync(Guid employeeId, Guid punchId, CancellationToken ct)
    {
        var rows = await FinalQuery(employeeId, punchId).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("capture_validation_final_binding_conflict");
        var row = rows.SingleOrDefault();
        if (row is null) return null;
        if (row.EmployeeId != employeeId || row.RawPunchEventId != punchId) throw AttendanceOperation.Conflict("capture_validation_final_binding_conflict");
        try
        {
            var codes = JsonSerializer.Deserialize<string[]>(row.CodesJson);
            var related = JsonSerializer.Deserialize<Guid[]>(row.RelatedRawPunchIdsJson);
            if (codes is not { Length: > 0 } || codes.Any(x => !CaptureValidationPolicyRules.Text(x, 100)) ||
                !codes.Contains(row.FinalReasonCode) || related is null || related.Any(x => x == Guid.Empty || x == punchId) ||
                related.Distinct().Count() != related.Length)
                throw AttendanceOperation.Conflict("capture_validation_final_binding_conflict");
            return new(row.Id, row.RawPunchEventId, row.EmployeeId, row.Action, row.IsOffline, row.RegisteredDeviceId, row.RequestSha256,
                row.ReceivedAtUtc, row.EvaluatedAtUtc, row.AssessedAtUtc, new(row.CandidateOutcome, row.CandidateReasonCode),
                new(new(row.FinalOutcome, row.FinalReasonCode), Array.AsReadOnly(codes), row.PolicyVersionId, row.PolicyRevision,
                    row.SourceFingerprint, Array.AsReadOnly(related), row.DeviceObservationCode));
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("capture_validation_final_binding_conflict"); }
    }
    internal static void ValidateRows(IReadOnlyList<RecentCaptureEvidence> rows, Guid employeeId, DateTimeOffset? from, DateTimeOffset? through)
    {
        Scope(employeeId);
        if (rows.Count > CaptureValidationPolicyRules.MaximumRecentRows + 1 || rows.Any(x => x is null || x.Raw is null) ||
            rows.Select(x => x.Raw.Id).Distinct().Count() != rows.Count)
            throw AttendanceOperation.Conflict("capture_validation_query_scope_conflict");
        foreach (var item in rows)
        {
            var raw = item.Raw; var assessment = item.Assessment;
            if (raw.Id == Guid.Empty || raw.EmployeeId != employeeId || !Enum.IsDefined(raw.Action) ||
                raw.ReceivedAtUtc.Offset != TimeSpan.Zero || from.HasValue && raw.ReceivedAtUtc < from || through.HasValue && raw.ReceivedAtUtc > through ||
                assessment is not null && (assessment.Id == Guid.Empty || assessment.EmployeeId != employeeId || assessment.RawPunchEventId != raw.Id ||
                    assessment.Revision < 1 || !Enum.IsDefined(assessment.Outcome) || assessment.AssessedAtUtc < raw.ReceivedAtUtc ||
                    through.HasValue && assessment.AssessedAtUtc > through))
                throw AttendanceOperation.Conflict("capture_validation_query_scope_conflict");
        }
    }
    public void Add(CaptureValidationEvidence evidence)
    {
        var final = evidence.FinalAssessment;
        db.Set<CaptureValidationFinalEvidence>().Add(new(evidence.Id, evidence.RawPunchEventId, evidence.EmployeeId,
            evidence.Action, evidence.IsOffline, evidence.RegisteredDeviceId, evidence.RequestSha256, evidence.ReceivedAtUtc,
            evidence.EvaluatedAtUtc, evidence.AssessedAtUtc, evidence.CandidateDecision.Outcome, evidence.CandidateDecision.ReasonCode,
            final.Decision.Outcome, final.Decision.ReasonCode, final.PolicyVersionId, final.PolicyRevision, final.SourceFingerprint,
            JsonSerializer.Serialize(final.Codes), JsonSerializer.Serialize(final.RelatedRawPunchIds), final.DeviceObservationCode));
    }
    private static void Scope(Guid employeeId, Guid? punchId = null)
    {
        if (employeeId == Guid.Empty || punchId == Guid.Empty) throw AttendanceOperation.Invalid("capture_validation_invalid_scope");
    }
}
