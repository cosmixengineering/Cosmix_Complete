namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Generates unique employee codes per tenant using atomic database-level increment.
/// </summary>
public interface IEmployeeCodeGenerator
{
    Task<string> GenerateNextAsync(string? tenantId, CancellationToken ct = default);
}
