using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Policies;

public static class AttendancePolicyPermissions
{
    public const string Configure = "attendance.policies.configure";
    public const string Publish = "attendance.policies.publish";
    public const string ApplicabilityRead = "attendance.policies.applicability.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Configure, "Read and validate attendance, salary, OT and deduction policy settings with explicit All-scope permission and current web MFA", true, true, false),
        new(Publish, "Direct attendance policy publication and dated assignment with explicit All-scope permission and current web MFA", true, true, false),
        new(ApplicabilityRead, "Read dated attendance policy applicability within historical employee scope", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ValidateAttendancePolicySettingsRequest(AttendancePolicyRules? Rules);

public sealed record AttendancePolicySettingsValidationResponse(bool IsValid, bool Published,
    string? CanonicalJson, string? ConfigurationSha256, IReadOnlyList<AttendancePolicyRuleError> Errors);

public sealed record AttendancePolicyFinancialRecommendations(int SchemaVersion, bool Published,
    bool RequiresExplicitPublication, AttendanceOrdinaryPayRules OrdinaryPay,
    AttendanceLatePolicyChangeCountingStrategy PolicyChangeCountingStrategy,
    AttendanceLateSalaryEffectiveDateBasis SalaryEffectiveDateBasis,
    AttendanceLateOverlapTreatment OverlapTreatment);

/// <summary>Form validation only. No policy version, approval, assignment, summary or salary mutation.</summary>
public sealed class AttendancePolicySettingsService(AttendanceOperation operation)
{
    /// <summary>Reviewable form suggestions only. No read or calculation path injects these into missing policy values.</summary>
    public Task<AttendancePolicyFinancialRecommendations> FinancialRecommendationsAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendancePolicyPermissions.Configure, "attendance.policy-settings.financial-recommendations", null, context,
            (_, _) => Task.FromResult(new AttendancePolicyFinancialRecommendations(AttendancePolicyRulesCodec.SupportedSchemaVersion,
                false, true, new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays,
                    null, 1m, 0.5m, AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours,
                    AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
                    AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp),
                AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,
                AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
                AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate)), ct);

    public Task<AttendancePolicySettingsValidationResponse> ValidateAsync(ValidateAttendancePolicySettingsRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendancePolicyPermissions.Configure, "attendance.policy-settings.validate", null, context,
            (_, _) =>
            {
                var validation = AttendancePolicyRulesCodec.Validate(request?.Rules);
                var fingerprint = validation.IsValid
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(validation.CanonicalJson!))) : null;
                return Task.FromResult(new AttendancePolicySettingsValidationResponse(validation.IsValid, false,
                    validation.CanonicalJson, fingerprint, validation.Errors));
            }, ct);
}
