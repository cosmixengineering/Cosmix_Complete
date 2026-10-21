using System.Data;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

internal static class FreshLocalTestingDatabase
{
    internal const string FinalMigration = "20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence";

    internal static async Task VerifyAsync(CosmixDbContext db, FreshLocalTestingTarget target, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var binding = new SqlConnectionStringBuilder(connection.ConnectionString);
        if (!binding.DataSource.Equals(target.DataSource, StringComparison.OrdinalIgnoreCase) ||
            binding.InitialCatalog != LocalTesting.Database || !binding.IntegratedSecurity ||
            !string.IsNullOrEmpty(binding.UserID) || !string.IsNullOrEmpty(binding.Password) ||
            !string.IsNullOrEmpty(binding.AttachDBFilename))
            throw new InvalidOperationException("Fresh local-testing connection differs from its reviewed target.");

        if (connection.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME(),
                   SUSER_SNAME(d.owner_sid), ORIGINAL_LOGIN(), d.is_read_only,
                   (SELECT COUNT(*) FROM sys.synonyms),
                   (SELECT COUNT(*) FROM sys.sql_expression_dependencies
                    WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL)
            FROM sys.databases d WHERE d.database_id=DB_ID();
            SELECT physical_name FROM sys.database_files ORDER BY file_id;
            """;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct) || Enumerable.Range(0, 7).Any(reader.IsDBNull))
                throw new InvalidOperationException("Fresh local-testing SQL identity is unavailable.");
            var server = reader.GetString(0);
            var database = reader.GetString(1);
            var owner = reader.GetString(2);
            var actor = reader.GetString(3);
            var readOnly = reader.GetBoolean(4);
            var synonyms = reader.GetInt32(5);
            var external = reader.GetInt32(6);
            if (await reader.ReadAsync(ct) || !await reader.NextResultAsync(ct))
                throw new InvalidOperationException("Fresh local-testing SQL identity is ambiguous.");
            var files = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0)) throw new InvalidOperationException("Fresh local-testing physical files are unavailable.");
                files.Add(reader.GetString(0));
            }
            FreshLocalTestingGuard.ValidateIdentity(target, server, database, owner, actor, files, readOnly, synonyms, external);
        }

        var expected = db.Database.GetMigrations().ToArray();
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (expected.Length != 21 || expected[^1] != FinalMigration || !expected.SequenceEqual(applied) ||
            db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Fresh local-testing requires the exact reviewed 21-migration schema.");
    }
}
