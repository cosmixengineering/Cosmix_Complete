using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cosmix.Api.Development;

/// <summary>EF tooling builds the model without starting API workers or loading authentication secrets.</summary>
public sealed class PayrollMigrationContextFactory : IDesignTimeDbContextFactory<CosmixDbContext>
{
    public CosmixDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_EF_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set COSMIX_EF_CONNECTION explicitly for EF tooling; no database is selected by default.");
        return new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
    }
}
