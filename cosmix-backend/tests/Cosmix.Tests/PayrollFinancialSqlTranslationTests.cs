using System.Data.Common;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollFinancialSqlTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Financial_history_and_target_queries_translate_before_any_connection_opens(bool target)
    {
        var guard = new NoDatabaseConnection();
        var options = new DbContextOptionsBuilder<CosmixDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(guard).Options;
        await using var db = new CosmixDbContext(options);
        var repository = new AttendancePayrollFinancialRepository(db);
        // SQL Server must translate the complete bounded query before requesting a connection.
        // This interceptor always stops before network/database access; an untranslatable query
        // throws InvalidOperationException first and fails this assertion.
        await Assert.ThrowsAsync<TranslationReachedConnection>(() => target
            ? repository.TargetAsync(new DateOnly(2030, 2, 1), default)
            : repository.HistoryAsync(Guid.Parse("afd5c9de-278e-4bbe-a71e-2691aaad2fcc"), new DateOnly(2030, 2, 1), default));
        Assert.Equal(1, guard.Attempts);
    }

    private sealed class TranslationReachedConnection : Exception;
    private sealed class NoDatabaseConnection : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new TranslationReachedConnection();
        }
    }
}
