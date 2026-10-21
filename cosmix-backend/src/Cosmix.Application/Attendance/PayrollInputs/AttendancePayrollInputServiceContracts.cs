using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Offices;

namespace Cosmix.Application.Attendance.PayrollInputs;

public static class AttendancePayrollInputPermissions
{
    public const string Read = "attendance.payroll-inputs.read";
    public const string Calculate = "attendance.payroll-inputs.calculate";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read restricted attendance deduction input previews and retained financial history", true, true, false),
        new(Calculate, "Prepare immutable scoped attendance payroll inputs; cannot release payments or activate penalties", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SaveAttendancePayrollInputsRequest(
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedPreviewFingerprint,
    [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedRevision,
    [Required, StringLength(1000)] string Reason);

/// <summary>Explicit financial permission is required. Never embed in employee daily attendance.</summary>
public sealed record AttendancePayrollInputLineResponse(string Key, string Kind, DateOnly CompletionDate,
    IReadOnlyList<DateOnly> SourceDates, IReadOnlyList<Guid> PolicyVersionIds, Guid CompensationRecordId,
    string SalaryBasis, decimal MonthlySalary, int SalaryDivisorDays, decimal DeductionDays,
    long ShortfallTicks, long ScheduledTicks, decimal CandidateAmount, decimal Amount, string Currency,
    string Rounding, string ApplicationCode, decimal OrdinaryComparisonAmount,
    IReadOnlyList<Guid> SourceIds, IReadOnlyList<string> SourceFingerprints, string CompensationFingerprint,
    string ExactAmountNumerator, string ExactAmountDenominator);
public sealed record AttendancePayrollInputPreview(Guid EmployeeId, DateOnly MonthStart, DateOnly MonthEndExclusive,
    string State, string PreviewFingerprint, int ExpectedRevision, Guid? LatestRevisionId, bool CanSave,
    bool IsLocked, bool PayrollReady, bool PaymentInstruction, string LegalStatus,
    decimal? DeductionTotal, string? Currency, IReadOnlyList<AttendancePayrollInputLineResponse> Lines,
    IReadOnlyList<AttendancePayrollInputIssue> Issues, IReadOnlyList<AttendancePayrollJurisdictionContext> Jurisdictions);
public sealed record AttendancePayrollInputRevisionResponse(Guid Id, Guid EmployeeId, DateOnly MonthStart,
    int Revision, Guid? SupersedesRevisionId, string CalculationVersion, string InputFingerprint,
    DateTimeOffset CreatedAtUtc, Guid ActorUserId, string State);
public sealed record AttendancePayrollInputDetail(AttendancePayrollInputRevisionResponse Revision,
    AttendancePayrollInputPreview Inputs);

public sealed record AttendancePayrollJurisdictionDay(DateOnly BusinessDate, Guid? SiteId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null);
public sealed record AttendancePayrollJurisdictionContext(DateOnly BusinessDate, Guid? SiteId,
    Guid? ConfigurationVersionId, string? JurisdictionCode, string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null);
/// <summary>Historical site location only. A configured jurisdiction does not certify legality.</summary>
public interface IAttendancePayrollJurisdictionReader
{
    Task<IReadOnlyList<AttendancePayrollJurisdictionContext>> ReadAsync(
        IReadOnlyList<AttendancePayrollJurisdictionDay> days, CancellationToken ct);
}

internal sealed record AttendancePayrollInputEvidence(int SchemaVersion, Guid EmployeeId, DateOnly MonthStart,
    int Revision, string PreviewFingerprint, AttendancePayrollInputCalculation Calculation,
    IReadOnlyList<AttendancePayrollJurisdictionContext> Jurisdictions,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes, Guid ActorUserId, string Reason, DateTimeOffset CreatedAtUtc);
