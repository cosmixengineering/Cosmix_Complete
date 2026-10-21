using System.Globalization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

public static class AllowanceStates
{
    public const string Eligible = "eligible";
    public const string NotMet = "not_met";
    public const string InapplicableStaffCategory = "inapplicable_staff_category";
    public const string AmountPending = "amount_pending_management_decision";
    public const string PolicyMissing = "policy_missing";
    public const string DuplicateClaim = "duplicate_claim_exists";
    public const string DeploymentWorkflowUnavailable = "deployment_workflow_unavailable";
}

/// <summary>Pure meal-allowance eligibility (ALLOW-002/ALLOW-004). It consumes verified duty sessions,
/// the approved staff category and published policy values only; it never invents a money amount and
/// performs no persistence.</summary>
public static class AllowanceRules
{
    public const string OfficeStaffCategoryCode = "OFFICE";
    /// <summary>Meal claims carry no deployment category; the claim column still requires a value.</summary>
    public const string NonDeploymentCategory = "NONE";
    public const string DeferredReason =
        "Outstation and work-travel claims require the approved deployment workflow, which is not available yet.";

    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);

    /// <summary>One continuous duty cycle. A gap beyond the published continuity tolerance closes the
    /// cycle (ALLOW-004) even when the employee returns on the same business date.</summary>
    public sealed record DutySpan(Guid PrimaryWorkSessionId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, long Ticks);

    public static void Validate(AllowanceSubmitRequest request)
    {
        if (request is null) throw AttendanceOperation.Invalid("invalid_allowance_request");
        RosterSchedule.ValidateDate(request.BusinessDate);
        if (!Enum.IsDefined(request.Kind)) throw AttendanceOperation.Invalid("invalid_allowance_kind");
        Text(request.Reason, 1000);
    }

    public static void Text(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > length || value.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_allowance_text");
    }

    /// <summary>Merge evidence-bearing closed sessions whose gap is within the published tolerance,
    /// returning longest-verified-duty hours and the optional qualifying cycle.</summary>
    public static (IReadOnlyList<DutySpan> Spans, decimal? LongestHours, DutySpan? Qualifying)
        ContiguousSpans(IReadOnlyList<OvertimeSessionSource> sessions, decimal? requiredHours,
        TimeOnly? officeDutyStart, DateOnly businessDate, int gapToleranceMinutes, DateTimeOffset now)
    {
        var usable = sessions.Where(x => x.ValidDutyEvidence && x.EndsAtUtc.HasValue &&
                x.EndsAtUtc.Value > x.StartsAtUtc && x.EndsAtUtc.Value <= now)
            .OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).ToArray();
        var tolerance = TimeSpan.FromMinutes(gapToleranceMinutes);
        var spans = new List<DutySpan>();
        foreach (var session in usable)
        {
            var sessionStart = session.StartsAtUtc;
            var sessionEnd = session.EndsAtUtc!.Value;
            if (spans.Count == 0 || sessionStart - spans[^1].EndsAtUtc > tolerance)
                spans.Add(new DutySpan(session.Id, sessionStart, sessionEnd, (sessionEnd - sessionStart).Ticks));
            else
            {
                var merged = spans[^1];
                var end = sessionEnd > merged.EndsAtUtc ? sessionEnd : merged.EndsAtUtc;
                spans[^1] = merged with { EndsAtUtc = end, Ticks = (end - merged.StartsAtUtc).Ticks };
            }
        }

        long? longest = spans.Count == 0 ? (long?)null : spans.Max(x => x.Ticks);
        decimal? longestHours = longest is null ? null : Floor2((decimal)longest.Value / TimeSpan.TicksPerHour);
        DutySpan? qualifying = null;
        if (requiredHours is { } threshold && threshold > 0m)
        {
            var requiredTicks = threshold * TimeSpan.TicksPerHour;
            qualifying = spans.FirstOrDefault(x => x.Ticks >= requiredTicks &&
                (officeDutyStart is not { } start || StartsByOfficeDuty(x.StartsAtUtc, businessDate, start)));
        }
        return (spans, longestHours, qualifying);
    }

    public static AllowanceKindEligibility Evaluate(AllowancePolicyRules? rules, AllowanceKind kind,
        DateOnly businessDate, string staffCategory, IReadOnlyList<OvertimeSessionSource> sessions, DateTimeOffset now)
    {
        if (kind is AllowanceKind.OutstationDaily or AllowanceKind.WorkTravel)
            return new(kind, AllowanceStates.DeploymentWorkflowUnavailable, null, null, null, null, null, null,
                null, null, false, DeferredReason);

        var office = string.Equals(staffCategory, OfficeStaffCategoryCode, StringComparison.OrdinalIgnoreCase);
        var inapplicable = kind == AllowanceKind.OfficeMeal ? !office : office;
        if (inapplicable)
            return new(kind, AllowanceStates.InapplicableStaffCategory, null, null, null, null, null, null,
                null, null, false,
                office ? "Site meal kinds apply only to non-office staff." : "The office meal kind applies only to office staff.");

        if (rules is null)
            return new(kind, AllowanceStates.PolicyMissing, null, null, null, null, null, null, null, null, false,
                "No effective allowance policy is published for this employee on this date.");

        var amount = Amount(rules, kind);
        var required = Threshold(rules, kind);
        var officeStart = kind == AllowanceKind.OfficeMeal &&
            TimeOnly.TryParse(rules.OfficeDutyStartLocalTime, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : (TimeOnly?)null;
        var (_, longestHours, qualifying) = ContiguousSpans(sessions, required, officeStart, businessDate,
            rules.ContinuityGapToleranceMinutes ?? 0, now);

        if (qualifying is null)
            return new(kind, AllowanceStates.NotMet, longestHours, required, null, null, null, null,
                amount, rules.Currency, false, NotMetReason(kind, required, longestHours, officeStart));

        var key = Key(kind, qualifying);
        if (amount is null)
            return new(kind, AllowanceStates.AmountPending, longestHours, required, qualifying.StartsAtUtc,
                qualifying.EndsAtUtc, qualifying.PrimaryWorkSessionId, key, null, rules.Currency, false,
                "Duty qualifies, but management has not confirmed a money amount for this allowance kind.");

        return new(kind, AllowanceStates.Eligible, longestHours, required, qualifying.StartsAtUtc,
            qualifying.EndsAtUtc, qualifying.PrimaryWorkSessionId, key, amount, rules.Currency, false,
            $"Duty meets the published {kind} policy.");
    }

    /// <summary>Claims are keyed per kind on the primary session of the qualifying cycle.</summary>
    public static string Key(AllowanceKind kind, DutySpan span) =>
        $"{kind.ToString().ToUpperInvariant()}:{span.PrimaryWorkSessionId.ToString("N")}";

    public static decimal? Amount(AllowancePolicyRules? rules, AllowanceKind kind) => kind switch
    {
        AllowanceKind.OfficeMeal => rules?.OfficeMeal?.Amount,
        AllowanceKind.SiteDinner => rules?.SiteDinner?.Amount,
        AllowanceKind.SiteBreakfast => rules?.SiteBreakfast?.Amount,
        AllowanceKind.SiteLunch => rules?.SiteLunch?.Amount,
        AllowanceKind.OutstationDaily => rules?.OutstationDaily?.Amount,
        AllowanceKind.WorkTravel => rules?.WorkTravel?.Amount,
        _ => null
    };

    /// <summary>Cumulative verified duty needed for a cycle to reach this kind's milestone:
    /// office meal uses its own hours from the cycle start; site breakfast/lunch add their
    /// published increments to the dinner and post-dinner thresholds.</summary>
    public static decimal? Threshold(AllowancePolicyRules rules, AllowanceKind kind)
    {
        var dinner = rules.SiteDinner?.RequiredContinuousDutyHours;
        var afterDinner = rules.SiteBreakfast?.RequiredContinuousDutyHours;
        var afterBreakfast = rules.SiteLunch?.RequiredContinuousDutyHours;
        return kind switch
        {
            AllowanceKind.OfficeMeal => rules.OfficeMeal?.RequiredContinuousDutyHours,
            AllowanceKind.SiteDinner => dinner,
            AllowanceKind.SiteBreakfast => dinner + afterDinner,
            AllowanceKind.SiteLunch => dinner + afterDinner + afterBreakfast,
            _ => null
        };
    }

    private static bool StartsByOfficeDuty(DateTimeOffset startUtc, DateOnly businessDate, TimeOnly officeDutyStart)
    {
        var local = TimeZoneInfo.ConvertTime(startUtc, LocalZone);
        return DateOnly.FromDateTime(local.DateTime) == businessDate && TimeOnly.FromDateTime(local.DateTime) <= officeDutyStart;
    }

    private static string NotMetReason(AllowanceKind kind, decimal? required, decimal? longest, TimeOnly? officeStart)
    {
        var threshold = required is { } value ? value.ToString("0.00", CultureInfo.InvariantCulture) : "0.00";
        var longestText = longest is { } hours
            ? $"{hours.ToString("0.00", CultureInfo.InvariantCulture)} hours"
            : "no verified duty cycle is closed yet";
        return kind == AllowanceKind.OfficeMeal
            ? $"The office meal requires a verified cycle starting by {officeStart:HH:mm} local time that lasts at least {threshold} hours; the longest verified cycle is {longestText}."
            : $"{kind} requires at least {threshold} continuous verified duty hours; the longest verified cycle is {longestText}.";
    }

    private static decimal Floor2(decimal hours) => Math.Floor(hours * 100m) / 100m;
}
