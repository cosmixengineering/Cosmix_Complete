using System.Collections.Concurrent;
using NOIR.Application.Common.Interfaces;
using NOIR.Domain.Entities.Hr;
using NOIR.Infrastructure.Persistence.Repositories;

namespace NOIR.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for integration testing using SQL Server.
/// Thin subclass of <see cref="BaseWebApplicationFactory"/> — all shared logic lives in the base.
/// Overrides FeatureChecker to enable ALL features during integration tests.
/// Registers missing repositories (e.g., EmployeeTagRepository) for DI resolution.
/// Replaces code generators that use non-composable SqlQueryRaw with test-safe implementations.
/// </summary>
public class CustomWebApplicationFactory : BaseWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace FeatureChecker with one that enables ALL features
            // This ensures Webhooks and other disabled-by-default modules are testable
            var featureCheckerDescriptors = services
                .Where(d => d.ServiceType == typeof(IFeatureChecker))
                .ToList();
            foreach (var descriptor in featureCheckerDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddScoped<IFeatureChecker, AllFeaturesEnabledFeatureChecker>();

            // Register missing EmployeeTagRepository
            // Source code is missing this repository; register it in test infrastructure
            services.AddScoped<IRepository<EmployeeTag, Guid>, TestEmployeeTagRepository>();

            // Replace code generators that use SqlQueryRaw<int>().FirstAsync() with MERGE OUTPUT.
            // In .NET 10 EF Core, FirstAsync() tries to compose (add TOP(1)) onto non-composable
            // MERGE SQL, causing: "'FromSql' or 'SqlQuery' was called with non-composable SQL
            // and with a query composing over it."
            // Test-safe implementations use ToListAsync() instead.
            ReplaceService<IEmployeeCodeGenerator, TestSafeEmployeeCodeGenerator>(services);
            ReplaceService<IProjectCodeGenerator, TestSafeProjectCodeGenerator>(services);
            ReplaceService<ITaskNumberGenerator, TestSafeTaskNumberGenerator>(services);

            // Replace EmployeeHierarchyService which uses a CTE with OPTION (MAXRECURSION 20).
            // EF Core wraps SqlQueryRaw<AncestorRow> in a subquery, making CTE syntax invalid.
            // Test-safe implementation uses iterative LINQ queries instead of raw SQL.
            ReplaceService<IEmployeeHierarchyService, TestSafeEmployeeHierarchyService>(services);
        });
    }

    private static void ReplaceService<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
        services.AddScoped<TService, TImplementation>();
    }
}

/// <summary>
/// Feature checker that enables ALL features for integration testing.
/// Ensures modules with DefaultEnabled=false (e.g., Webhooks) are testable.
/// </summary>
internal sealed class AllFeaturesEnabledFeatureChecker : IFeatureChecker
{
    private readonly IModuleCatalog _catalog;

    public AllFeaturesEnabledFeatureChecker(IModuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<EffectiveFeatureState> GetStateAsync(string featureName, CancellationToken ct = default)
        => Task.FromResult(new EffectiveFeatureState(true, true, true, _catalog.IsCore(featureName)));

    public Task<IReadOnlyDictionary<string, EffectiveFeatureState>> GetAllStatesAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, EffectiveFeatureState>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in _catalog.GetAllModules())
        {
            result[module.Name] = new(true, true, true, module.IsCore);
            foreach (var feature in module.Features)
                result[feature.Name] = new(true, true, true, false);
        }
        return Task.FromResult<IReadOnlyDictionary<string, EffectiveFeatureState>>(result);
    }
}

/// <summary>
/// Repository for EmployeeTag, missing from source code.
/// Registered in test infrastructure to satisfy Wolverine handler DI resolution.
/// </summary>
internal sealed class TestEmployeeTagRepository : Repository<EmployeeTag, Guid>, IRepository<EmployeeTag, Guid>
{
    public TestEmployeeTagRepository(
        ApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IDateTime dateTime)
        : base(dbContext, currentUser, dateTime)
    {
    }
}

/// <summary>
/// Test-safe employee code generator. Uses ToListAsync() instead of FirstAsync()
/// to avoid non-composable SQL error with MERGE OUTPUT in .NET 10 EF Core.
/// </summary>
internal sealed class TestSafeEmployeeCodeGenerator : IEmployeeCodeGenerator
{
    private readonly ApplicationDbContext _dbContext;

    public TestSafeEmployeeCodeGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNextAsync(string? tenantId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var prefix = $"EMP-{today:yyyyMMdd}-";
        var nextValue = await AtomicIncrementAsync(prefix, tenantId, ct);
        return $"{prefix}{nextValue:D6}";
    }

    private async Task<int> AtomicIncrementAsync(string prefix, string? tenantId, CancellationToken ct)
    {
        var sql = tenantId != null
            ? @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix, {1} AS TenantId) AS source
                ON target.Prefix = source.Prefix AND target.TenantId = source.TenantId
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), {1}, {0}, 1)
                OUTPUT INSERTED.CurrentValue;"
            : @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix) AS source
                ON target.Prefix = source.Prefix AND target.TenantId IS NULL
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), NULL, {0}, 1)
                OUTPUT INSERTED.CurrentValue;";

        // Use ToListAsync() instead of FirstAsync() to avoid composability error
        var results = tenantId != null
            ? await _dbContext.Database.SqlQueryRaw<int>(sql, prefix, tenantId).ToListAsync(ct)
            : await _dbContext.Database.SqlQueryRaw<int>(sql, prefix).ToListAsync(ct);

        return results.First();
    }
}

/// <summary>
/// Test-safe project code generator. Uses ToListAsync() instead of FirstAsync()
/// to avoid non-composable SQL error with MERGE OUTPUT in .NET 10 EF Core.
/// </summary>
internal sealed class TestSafeProjectCodeGenerator : IProjectCodeGenerator
{
    private readonly ApplicationDbContext _dbContext;

    public TestSafeProjectCodeGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNextAsync(string? tenantId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var prefix = $"PRJ-{today:yyyyMMdd}-";
        var nextValue = await AtomicIncrementAsync(prefix, tenantId, ct);
        return $"{prefix}{nextValue:D6}";
    }

    private async Task<int> AtomicIncrementAsync(string prefix, string? tenantId, CancellationToken ct)
    {
        var sql = tenantId != null
            ? @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix, {1} AS TenantId) AS source
                ON target.Prefix = source.Prefix AND target.TenantId = source.TenantId
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), {1}, {0}, 1)
                OUTPUT INSERTED.CurrentValue;"
            : @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix) AS source
                ON target.Prefix = source.Prefix AND target.TenantId IS NULL
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), NULL, {0}, 1)
                OUTPUT INSERTED.CurrentValue;";

        var results = tenantId != null
            ? await _dbContext.Database.SqlQueryRaw<int>(sql, prefix, tenantId).ToListAsync(ct)
            : await _dbContext.Database.SqlQueryRaw<int>(sql, prefix).ToListAsync(ct);

        return results.First();
    }
}

/// <summary>
/// Test-safe task number generator. Uses ToListAsync() instead of FirstAsync()
/// to avoid non-composable SQL error with MERGE OUTPUT in .NET 10 EF Core.
/// </summary>
internal sealed class TestSafeTaskNumberGenerator : ITaskNumberGenerator
{
    private readonly ApplicationDbContext _dbContext;

    public TestSafeTaskNumberGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateNextAsync(string projectPrefix, string? tenantId, CancellationToken cancellationToken = default)
    {
        var prefix = $"{projectPrefix}-";
        var nextValue = await AtomicIncrementAsync(prefix, tenantId, cancellationToken);
        return $"{prefix}{nextValue:D3}";
    }

    private async Task<int> AtomicIncrementAsync(string prefix, string? tenantId, CancellationToken ct)
    {
        var sql = tenantId != null
            ? @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix, {1} AS TenantId) AS source
                ON target.Prefix = source.Prefix AND target.TenantId = source.TenantId
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), {1}, {0}, 1)
                OUTPUT INSERTED.CurrentValue;"
            : @"MERGE INTO SequenceCounters WITH (HOLDLOCK) AS target
                USING (SELECT {0} AS Prefix) AS source
                ON target.Prefix = source.Prefix AND target.TenantId IS NULL
                WHEN MATCHED THEN UPDATE SET CurrentValue = target.CurrentValue + 1
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, CurrentValue) VALUES (NEWID(), NULL, {0}, 1)
                OUTPUT INSERTED.CurrentValue;";

        var results = tenantId != null
            ? await _dbContext.Database.SqlQueryRaw<int>(sql, prefix, tenantId).ToListAsync(ct)
            : await _dbContext.Database.SqlQueryRaw<int>(sql, prefix).ToListAsync(ct);

        return results.First();
    }
}

/// <summary>
/// Test-safe employee hierarchy service. Replaces the production implementation that uses a
/// CTE with OPTION (MAXRECURSION 20) inside SqlQueryRaw&lt;AncestorRow&gt;. EF Core wraps
/// SqlQueryRaw results in a subquery, which makes CTE syntax invalid in SQL Server.
/// This iterative implementation uses plain LINQ queries instead.
/// </summary>
internal sealed class TestSafeEmployeeHierarchyService : IEmployeeHierarchyService
{
    private readonly ApplicationDbContext _dbContext;

    public TestSafeEmployeeHierarchyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HierarchyChain> GetAncestorChainAsync(
        Guid employeeId, int maxDepth, string? tenantId, CancellationToken ct)
    {
        var ancestorIds = new HashSet<Guid>();
        var currentId = employeeId;
        int depth = 0;

        while (depth < maxDepth)
        {
            var managerIdRow = await _dbContext.Set<Employee>()
                .Where(e => e.Id == currentId && !e.IsDeleted)
                .Select(e => new { e.Id, e.ManagerId })
                .FirstOrDefaultAsync(ct);

            if (managerIdRow == null)
                break;

            ancestorIds.Add(managerIdRow.Id);

            if (managerIdRow.ManagerId == null)
                break;

            currentId = managerIdRow.ManagerId.Value;
            depth++;
        }

        return new HierarchyChain(depth, ancestorIds);
    }
}

/// <summary>
/// Collection fixture for sharing the WebApplicationFactory across tests.
/// Improves test performance by reusing the same server instance.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition].
}
