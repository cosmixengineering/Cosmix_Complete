using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class CaptureValidationPolicyRepository(CosmixDbContext db) : ICaptureValidationPolicyRepository
{
    internal IQueryable<CaptureValidationPolicyVersion> TimelineQuery() => db.Set<CaptureValidationPolicyVersion>()
        .AsNoTracking().OrderBy(x => x.Revision).Take(CaptureValidationPolicyRules.MaximumTimelineRows + 1);
    public async Task<IReadOnlyList<CaptureValidationPolicyEntry>> TimelineAsync(CancellationToken ct)
    {
        var rows = await TimelineQuery().ToArrayAsync(ct);
        if (rows.Length > CaptureValidationPolicyRules.MaximumTimelineRows) throw AttendanceOperation.Conflict("capture_policy_history_limit");
        try
        {
            var entries = rows.Select(x => new CaptureValidationPolicyEntry(x.Id, x.Revision, x.SupersedesVersionId,
                x.EffectiveFromUtc, x.PublishedAtUtc, x.PublishedBy, x.ApprovalReference,
                JsonSerializer.Deserialize<CaptureValidationSettings>(x.SettingsJson) ?? throw AttendanceOperation.Conflict("capture_policy_source_conflict"),
                x.Reason, x.SourceFingerprint, x.ProtectedEvidenceReference)).ToArray();
            CaptureValidationPolicyRules.ValidateTimeline(entries);
            return entries;
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("capture_policy_source_conflict"); }
    }
    public void Add(CaptureValidationPolicyEntry entry)
    {
        CaptureValidationPolicyRules.ValidateEntry(entry);
        db.Set<CaptureValidationPolicyVersion>().Add(new(entry.Id, entry.Revision, entry.SupersedesVersionId, entry.EffectiveFromUtc,
            entry.PublishedAtUtc, entry.PublishedBy, entry.ApprovalReference,
            JsonSerializer.Serialize(CaptureValidationPolicyRules.Freeze(entry.Settings)), entry.Reason, entry.SourceFingerprint,
            entry.ProtectedEvidenceReference));
    }
}
