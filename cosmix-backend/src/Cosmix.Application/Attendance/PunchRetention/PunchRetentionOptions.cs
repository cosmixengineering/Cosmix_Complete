namespace Cosmix.Application.Attendance.PunchRetention;

public sealed class PunchRetentionOptions
{
    public const string SectionName = "PunchRetention";
    public int? RetentionDays { get; set; }
    public bool IsPolicyApproved { get; set; }
    public int RunIntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 500;
    public int MaxBatchesPerRun { get; set; } = 100;
}
