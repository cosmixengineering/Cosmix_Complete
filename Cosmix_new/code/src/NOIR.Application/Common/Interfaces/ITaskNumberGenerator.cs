namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Generates unique task numbers for project tasks using atomic database-level increment.
/// Format: {projectPrefix}-NNN where NNN is a zero-padded sequence per project.
/// </summary>
public interface ITaskNumberGenerator
{
    Task<string> GenerateNextAsync(string projectPrefix, string? tenantId, CancellationToken cancellationToken = default);
}
