using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;
using System.Numerics;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveTreatmentRules
{
    public static void Validate(LeaveRow row, LeavePayTreatment treatment, LeavePolicyEvidence evidence)
    {
        ValidatePolicyTreatment(row, treatment, evidence);
        var settings = LeavePolicySettings.Parse(evidence.Version.RulesJson, evidence.Version.RulesSchemaVersion);
        if (row.Type.Kind == LeaveKind.Emergency && row.Request.Days.Any(x => x.Minutes * 2 == settings.StandardDayMinutes) && row.Request.EvidencePrivateReference is null)
            throw AttendanceOperation.Invalid("emergency_half_day_evidence_required");
    }
    internal static void ValidatePolicyTreatment(LeaveRow row, LeavePayTreatment treatment, LeavePolicyEvidence evidence)
    {
        var settings = LeavePolicySettings.Parse(evidence.Version.RulesJson, evidence.Version.RulesSchemaVersion);
        var request = row.Request;
        if (!evidence.ProvenanceValid || !settings.EligibleStaffCategoryIds.Contains(evidence.StaffCategoryId))
            throw AttendanceOperation.Conflict("leave_policy_not_applicable");
        if (LeavePolicySettings.Year(request.StartDate, settings) != LeavePolicySettings.Year(request.EndDate, settings))
            throw AttendanceOperation.Invalid("split_leave_at_fiscal_year_boundary");
        if (settings.HolidayTreatment == LeaveHolidayTreatment.Exclude && request.Days.Any(x => evidence.Holidays.Contains(x.Date)))
            throw AttendanceOperation.Invalid("exclude_pinned_holidays_from_leave_request");
        if (request.Days.Any(x => x.Minutes > settings.StandardDayMinutes))
            throw AttendanceOperation.Invalid("leave_day_exceeds_explicit_policy_duration");
        if (treatment == LeavePayTreatment.Unpaid) return;
        if (!settings.PaidKinds.Contains(row.Type.Kind)) throw AttendanceOperation.Conflict("paid_leave_not_configured");
        if (row.Type.Kind == LeaveKind.SiteInjuryRest)
        {
            if (treatment != LeavePayTreatment.DoesNotReduceRegularLeave || row.Type.ReducesRegularBalance)
                throw AttendanceOperation.Invalid("injury_leave_cannot_reduce_regular_balance");
            return;
        }
        if (treatment != LeavePayTreatment.Paid) throw AttendanceOperation.Invalid("invalid_leave_treatment");
        if (row.Type.ReducesRegularBalance)
        {
            if (evidence.JoinedOn is null || !settings.ServiceEligible(evidence.JoinedOn.Value, request.StartDate))
                throw AttendanceOperation.Conflict("leave_service_eligibility_not_met");
            var typeLimit = row.Type.Kind switch { LeaveKind.Sick => settings.SickDays, LeaveKind.Casual => settings.CasualDays, _ => settings.AnnualDays };
            if (ExceedsDays(evidence.Usage, row.Type.Kind, request.RequestedMinutes, settings.StandardDayMinutes, settings.AnnualDays, typeLimit))
                throw AttendanceOperation.Conflict("leave_balance_insufficient_or_unavailable");
        }
    }
    public static bool ExceedsDays(LeaveBalanceUsage usage, LeaveKind kind, int requestedMinutes, int standardDayMinutes, int annualDays, int typeDays)
    {
        if (requestedMinutes <= 0 || standardDayMinutes is < 1 or > 1440)
            throw AttendanceOperation.Conflict("leave_usage_units_unavailable");
        var (annual, type) = Usage(usage, kind);
        var (annualCredit, typeCredit) = Credits(usage, kind);
        return annual.Exceeds(requestedMinutes, standardDayMinutes, annualDays, annualCredit) || type.Exceeds(requestedMinutes, standardDayMinutes, typeDays, typeCredit);
    }
    public static (LeaveQuotaBalance Annual, LeaveQuotaBalance Type) Balances(LeaveBalanceUsage usage, LeaveKind kind, int annualDays, int typeDays)
    {
        var (annual, type) = Usage(usage, kind);
        var (annualCredit, typeCredit) = Credits(usage, kind);
        return (annual.Balance(annualDays, annualCredit), type.Balance(typeDays, typeCredit));
    }
    private static (Fraction Annual, Fraction Type) Credits(LeaveBalanceUsage usage, LeaveKind kind)
    {
        var credits = usage.Credits ?? [];
        if (credits.Count > 1000 || credits.Any(x => x is null) || credits.Any(x => x.AwardId == Guid.Empty || x.Minutes is < 1 or > 527040 ||
            x.StandardDayMinutes is < 1 or > 1440 || !Enum.IsDefined(x.Kind) || x.ConsumptionOffset && x.Encashable) ||
            credits.Select(x => x.AwardId).Distinct().Count() != credits.Count)
            throw AttendanceOperation.Conflict("leave_benefit_credit_unverified");
        foreach (var group in credits.Where(x => x.ConsumptionOffset).GroupBy(x => x.Kind))
        {
            var offset = Fraction.Sum(group.Select(x => new LeaveUsageUnit(x.Minutes, x.StandardDayMinutes, x.Kind)));
            var used = Fraction.Sum((usage.Units ?? []).Where(x => x.Kind == group.Key));
            if (offset.GreaterThan(used)) throw AttendanceOperation.Conflict("leave_benefit_consumption_offset_conflict");
        }
        var selected = Fraction.Sum(credits.Where(x => x.Kind == kind).Select(x => new LeaveUsageUnit(x.Minutes, x.StandardDayMinutes, x.Kind)));
        // Another kind's award may offset that kind's recorded use, but cannot fund
        // a new request of this kind. This preserves the shared annual ceiling.
        var annual = selected;
        foreach (var group in credits.Where(x => x.Kind != kind).GroupBy(x => x.Kind))
        {
            var granted = Fraction.Sum(group.Select(x => new LeaveUsageUnit(x.Minutes, x.StandardDayMinutes, x.Kind)));
            var used = Fraction.Sum((usage.Units ?? []).Where(x => x.Kind == group.Key));
            annual = annual.Plus(granted.Min(used));
        }
        return (annual, selected);
    }
    public static LeaveExactDays RequestedDays(int minutes, int standardDayMinutes)
    {
        if (minutes <= 0 || standardDayMinutes is < 1 or > 1440) throw AttendanceOperation.Invalid("invalid_leave_day_allocation");
        return Fraction.Sum([new(minutes, standardDayMinutes, LeaveKind.Other)]).Response();
    }
    private static (Fraction Annual, Fraction Type) Usage(LeaveBalanceUsage usage, LeaveKind kind)
    {
        var units = usage.Units ?? [];
        if (units.Count > 10000 || usage.AnnualMinutes < 0 || usage.TypeMinutes < 0 ||
            units.Any(x => x.StandardDayMinutes is < 1 or > 1440 || x.UsedMinutes == 0 || !Enum.IsDefined(x.Kind)) ||
            units.Sum(x => (long)x.UsedMinutes) != usage.AnnualMinutes || units.Where(x => x.Kind == kind).Sum(x => (long)x.UsedMinutes) != usage.TypeMinutes)
            throw AttendanceOperation.Conflict("leave_usage_units_unavailable");
        return (Fraction.Sum(units), Fraction.Sum(units.Where(x => x.Kind == kind)));
    }
    private sealed record Fraction(BigInteger Numerator, BigInteger Denominator)
    {
        public static Fraction Sum(IEnumerable<LeaveUsageUnit> units)
        {
            BigInteger numerator = 0, denominator = 1;
            foreach (var unit in units)
            {
                numerator = numerator * unit.StandardDayMinutes + unit.UsedMinutes * denominator;
                denominator *= unit.StandardDayMinutes;
                var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
                numerator /= gcd; denominator /= gcd;
            }
            if (numerator < 0) throw AttendanceOperation.Conflict("leave_usage_units_unavailable");
            return new(numerator, denominator);
        }
        public bool Exceeds(int minutes, int dayMinutes, int limit, Fraction credit) =>
            (Numerator * dayMinutes + minutes * Denominator) * credit.Denominator >
            ((BigInteger)limit * credit.Denominator + credit.Numerator) * Denominator * dayMinutes;
        public Fraction Plus(Fraction other) => Reduce(Numerator * other.Denominator + other.Numerator * Denominator, Denominator * other.Denominator);
        public bool GreaterThan(Fraction other) => Numerator * other.Denominator > other.Numerator * Denominator;
        public Fraction Min(Fraction other) => Numerator * other.Denominator <= other.Numerator * Denominator ? this : other;
        public LeaveExactDays Response() => new(Numerator.ToString(System.Globalization.CultureInfo.InvariantCulture), Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public LeaveQuotaBalance Balance(int limit, Fraction credit) => new(limit, Response(),
            Reduce(((BigInteger)limit * Denominator - Numerator) * credit.Denominator + credit.Numerator * Denominator,
                Denominator * credit.Denominator).Response()) { AdditionalDays = credit.Response() };
        private static Fraction Reduce(BigInteger numerator, BigInteger denominator)
        { var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator); return new(numerator / gcd, denominator / gcd); }
    }
}
