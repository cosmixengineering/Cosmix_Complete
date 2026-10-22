namespace Cosmix.Application.Attendance.PunchRetention;

public sealed class PunchRetentionOptions
{
    public const string SectionName = "PunchRetention";
    public int? RetentionDays { get; set; }
    public int RunIntervalHours { get; set; } = 24;
}
