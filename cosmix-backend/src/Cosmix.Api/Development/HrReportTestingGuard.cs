using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Microsoft.Extensions.Configuration;

namespace Cosmix.Api.Development;

internal static class HrReportTestingGuard
{
    internal const string Profile = "HumaHrReports20261020R1";
    internal const string PrivateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020R1";
    internal const string ApplicationName = "Cosmix.HrReports.R1";
    internal const string KeyReference = "hr-reports-r1";
    internal const string CryptoVersion = "hr-r1";
    internal const string ExportKey = "TEST-HR-R1-20261020-export";
    internal const string Reason = "TEST ONLY R1 report acceptance";
    internal const string ReportType = "employee-master";
    internal const string Email = "hr@example.invalid";
    internal const string Phone = "+12025550103";
    internal static readonly Guid HrAccount = new("8475187f-bf3b-4f9c-a3ed-4bac32253005");
    internal static readonly Guid HrEmployee = new("35d19922-70a8-4c80-8578-e1029360e9fc");
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == Profile;

    internal static void ValidateConfiguration(IConfiguration configuration)
    {
        if (configuration["Jwt:Issuer"] != ApplicationName || configuration["Jwt:Audience"] != ApplicationName ||
            configuration["MobileAuthentication:Crypto:KeyVersion"] != CryptoVersion ||
            !bool.TryParse(configuration["AttendanceJobs:Enabled"], out var enabled) || enabled)
            throw Refuse();
    }

    internal static bool IsActor(AuthenticatedAccount actor) =>
        actor.AccountId == HrAccount && actor.EmployeeId == HrEmployee &&
        actor.SessionId != Guid.Empty && actor.Client == SessionClient.Web && actor.MfaSatisfied;

    internal static InvalidOperationException Refuse() => new("R1 report test host validation failed; values suppressed.");
}
