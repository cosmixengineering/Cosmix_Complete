namespace Cosmix.Infrastructure.Files;

public sealed record ClamAvScannerOptions
{
    public const string SectionName = "EmployeeFileScanner";

    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 3310;
    public bool AllowPrivateNetwork { get; init; }
    public int TimeoutSeconds { get; init; } = 15;
    public int MaxSizeBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxResponseBytes { get; init; } = 4096;
}
