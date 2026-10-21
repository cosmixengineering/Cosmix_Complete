using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Cosmix.Api.Development;

internal static class HrVehicleTestingGuard
{
    internal const string Profile = "HumaHrVehicle20261020V1";
    internal const string PrivateRoot = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020V1";
    internal const string Marker = "TEST-HR-V1-20261020";
    internal const string Model = "TEST ONLY custody acceptance";
    internal const string ChangedModel = "TEST ONLY custody changed payload";
    internal const string IssueReference = "TEST ONLY V1 issue condition; synthetic reference, no attachment";
    internal const string ReturnReference = "TEST ONLY V1 return condition; synthetic reference, no attachment";
    internal static readonly Guid AdminAccount = new("94A83B62-5B56-4450-9175-2A7A9E3261BF");
    internal static readonly Guid AdminEmployee = new("7C505086-87EA-4FD2-980E-823ECCCBE36A");
    internal static readonly Guid HrAccount = new("8475187F-BF3B-4F9C-A3ED-4BAC32253005");
    internal static readonly Guid HrEmployee = new("35D19922-70A8-4C80-8578-E1029360E9FC");
    internal static bool IsSelected => Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == Profile;
    internal static readonly string[] ConfigurationKeys = ["LocalTesting:V1:IssuedAt", "LocalTesting:V1:ReturnedAt"];

    internal static void ValidateConfiguration(IConfiguration config)
    {
        var issued = ParseInstant(config[ConfigurationKeys[0]]);
        var returned = ParseInstant(config[ConfigurationKeys[1]]);
        if (issued >= returned || returned > DateTimeOffset.UtcNow ||
            issued.UtcDateTime.Date != new DateTime(2026, 10, 20) ||
            returned.UtcDateTime.Date != issued.UtcDateTime.Date)
            throw HrBusinessTestingGuard.Refuse();
    }

    internal static DateTimeOffset ParseInstant(string? value) =>
        DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
        ? result : throw HrBusinessTestingGuard.Refuse();
}
