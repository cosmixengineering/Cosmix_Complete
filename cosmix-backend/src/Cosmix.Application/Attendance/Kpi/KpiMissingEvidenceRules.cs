using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiMissingEvidenceRules
{
    public const string RequestPrefix = "kpi-missing-v1:";
    public const string ApprovedEvent = "attendance.kpi.missing-evidence.verified.v1";
    public const string RevisedEvent = "attendance.kpi.missing-evidence.revised.v1";
    public const string TargetType = "KpiMissingEvidence";
    public static string RequestType(DateOnly date, Guid leave) => RequestPrefix + Target(date, leave);
    public static string Target(DateOnly date, Guid leave) => $"{date:yyyyMMdd}:{leave:N}";
    public static string RequestPurpose(Guid employee) => $"attendance:kpi:missing:{employee:N}:request:v1";
    public static string ProofPurpose(Guid employee) => $"attendance:kpi:missing:{employee:N}:approved:v1";
    public static string RevisionPurpose(Guid employee) => $"attendance:kpi:missing:{employee:N}:revision:v1";
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static bool Digest(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);
    public static DateOnly End(DateOnly date) => date == default || date == DateOnly.MaxValue ?
        throw AttendanceOperation.Invalid("invalid_kpi_missing_evidence_date") : date.AddDays(1);
    public static void Range(Guid employee, DateOnly from, DateOnly to)
    {
        if (employee == Guid.Empty || from == default || to <= from || to.DayNumber - from.DayNumber > 366)
            throw AttendanceOperation.Invalid("invalid_kpi_missing_evidence_range");
    }
    public static void Independent(AuthenticatedAccount actor, Guid employee, IEnumerable<string?> requesters)
    {
        if (actor.EmployeeId == employee || requesters.Contains(actor.AccountId.ToString(), StringComparer.OrdinalIgnoreCase))
            throw AttendanceOperation.Denied();
    }
    public static void Unique(IReadOnlyList<ApprovalRequest> rows)
    {
        if (rows.Count > 32) throw AttendanceOperation.Conflict("kpi_missing_evidence_target_limit");
        if (rows.Any(x => x.State == ApprovalState.Approved)) throw AttendanceOperation.Conflict("kpi_missing_evidence_event_already_reviewed");
        if (rows.Any(x => x.State == ApprovalState.Pending)) throw AttendanceOperation.Conflict("kpi_missing_evidence_review_pending");
    }
    public static string Duration(long leaveTicks, long standardDayTicks) => standardDayTicks <= 0 || leaveTicks <= 0 ? "unavailable" :
        leaveTicks == standardDayTicks ? "full_day" : leaveTicks <= long.MaxValue / 2 && leaveTicks * 2 == standardDayTicks ? "half_day" : "other_interval";
    public static bool SettingsValid(KpiMissingEligibilityRules? settings) => settings is not null &&
        settings.Cases is { Count: > 0 and <= 2 } && settings.Cases.All(x => Enum.IsDefined(x)) && settings.Cases.Distinct().Count() == settings.Cases.Count &&
        settings.AssessmentBasis == KpiMissingAssessmentSetting.AtIndependentReview && settings.DurationBasis is { } duration && Enum.IsDefined(duration) &&
        settings.AllowedLeaveStates is { Count: > 0 and <= 4 } && settings.AllowedLeaveStates.All(x => x is
            ApprovalState.Pending or ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) &&
        settings.AllowedLeaveStates.Distinct().Count() == settings.AllowedLeaveStates.Count;

    public static KpiMissingEligibilityRules? Eligibility(AdvancedAttendanceRules rules) =>
        SettingsValid(rules.Kpi?.MissingEvidenceEligibility) ? rules.Kpi!.MissingEvidenceEligibility : null;
    public static byte[] Version(ApprovalRequest row, string expected)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(expected ?? ""); } catch (FormatException) { throw AttendanceOperation.Invalid("invalid_kpi_missing_version"); }
        if (bytes.Length != 8) throw AttendanceOperation.Invalid("invalid_kpi_missing_version");
        if (!row.RowVersion.SequenceEqual(bytes)) throw AttendanceOperation.Conflict("kpi_missing_evidence_version_conflict");
        return bytes;
    }
    public static IReadOnlyList<AttendanceResourceScope> Scopes(Guid employee, params IReadOnlyList<AttendanceResourceScope>[] sets)
    {
        var scopes = sets.SelectMany(x => x).DistinctBy(Hash).ToArray();
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.Any(s => s.Alternatives.Any(x => x.EmployeeId != employee)))
            throw AttendanceOperation.Conflict("kpi_missing_evidence_scope_required");
        return scopes;
    }
}
