using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.DailySummaries;

/// <summary>Explainable approved-rule output; storage does not execute formulas or approve deductions.</summary>
public sealed class AttendanceCalculationLine : IAppendOnlyEntity
{
    private AttendanceCalculationLine() { }
    public AttendanceCalculationLine(Guid summaryId, int sequence, string ruleCode, string explanation,
        decimal inputQuantity, decimal resultQuantity, string unitCode, decimal? monetaryAmount = null, string? currency = null)
    {
        if (monetaryAmount.HasValue != (currency is not null)) throw new ArgumentException("Money requires an explicit currency.");
        if (Math.Abs(inputQuantity) > 99999999999999.9999m || Math.Abs(resultQuantity) > 99999999999999.9999m
            || monetaryAmount is < -9999999999999999.99m or > 9999999999999999.99m) throw new ArgumentOutOfRangeException(nameof(inputQuantity));
        Id = Guid.NewGuid(); DailyAttendanceSummaryId = ModelGuard.Id(summaryId, nameof(summaryId)); Sequence = AttendanceModelGuard.Revision(sequence);
        RuleCode = ModelGuard.Code(ruleCode, 100); Explanation = ModelGuard.Required(explanation, 2000, nameof(explanation));
        InputQuantity = inputQuantity; ResultQuantity = resultQuantity; UnitCode = ModelGuard.Code(unitCode, 30);
        MonetaryAmount = monetaryAmount; Currency = currency is null ? null : ModelGuard.Code(currency, 3);
        if (Currency is not null && Currency.Length != 3) throw new ArgumentException("A three-letter currency is required.");
    }
    public Guid Id { get; private set; }
    public Guid DailyAttendanceSummaryId { get; private set; }
    public int Sequence { get; private set; }
    public string RuleCode { get; private set; } = string.Empty;
    public string Explanation { get; private set; } = string.Empty;
    public decimal InputQuantity { get; private set; }
    public decimal ResultQuantity { get; private set; }
    public string UnitCode { get; private set; } = string.Empty;
    public decimal? MonetaryAmount { get; private set; }
    public string? Currency { get; private set; }
    public DailyAttendanceSummary Summary { get; private set; } = null!;
}
