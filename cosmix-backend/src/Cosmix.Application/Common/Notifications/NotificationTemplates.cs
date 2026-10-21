namespace Cosmix.Application.Common.Notifications;

/// <summary>Only generic in-app text. Details are read through their separately authorized feature APIs.</summary>
public static class NotificationTemplates
{
    public const string NewDevice = "authentication.new-device";
    public const string AttendanceException = "attendance.exception";
    public const string MissingPunch = "attendance.missing-punch";
    public const string Decision = "attendance.decision";
    public const string ShiftChanged = "attendance.shift-changed";
    public const string ReportReady = "attendance.report-ready";
    public const string ZoneNotice = "attendance.zone-notice";
    public const string SiteReport = "attendance.site-report";
    public const string DocumentExpiring = "hr.document-expiring";
    public const string CertificationExpiring = "hr.certification-expiring";
    public const string InsuranceExpiring = "hr.insurance-expiring";

    public static bool TryGet(string code, out (string Title, string Body) text)
    {
        text = code switch
        {
            NewDevice => ("New device signed in", "Review your account sessions and revoke any device you do not recognize."),
            AttendanceException => ("Attendance needs attention", "Open your attendance record to review the details."),
            MissingPunch => ("Attendance record needs review", "Open your attendance record to check for a missing event."),
            Decision => ("Attendance request updated", "Open your request to view its current status."),
            ShiftChanged => ("Site schedule updated", "Open My Shift to check your applicable site schedule."),
            ReportReady => ("Report ready", "Open your report request to access the authorized result."),
            ZoneNotice => ("Site instruction available", "Open the application to review your site instruction."),
            SiteReport => ("Site report updated", "Open your site reports to view the current status."),
            DocumentExpiring => ("Document expiring soon", "One or more of your employment identity or compliance documents is nearing expiration."),
            CertificationExpiring => ("Certification expiring soon", "Your professional certification or licence is nearing expiration."),
            InsuranceExpiring => ("Insurance policy update", "Your group health or life insurance coverage record has an upcoming status update."),
            _ => default
        };
        return text.Title is not null;
    }
}
