using System.Linq.Expressions;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Api.Development;

internal static class AuthOnlyTestingDatabase
{
    internal static readonly Expression<Func<OtpDelivery, bool>> NonterminalDelivery = d =>
        d.Status != OtpDeliveryStatus.Submitted && d.Status != OtpDeliveryStatus.Failed && d.Status != OtpDeliveryStatus.Expired;

    // Called on the same already-open context used by the normal account/model/migration guard.
    internal static async Task VerifyAsync(CosmixDbContext db, CancellationToken ct, bool allowUnencryptedLocalPipe = false)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME(),
                   SUSER_SNAME(d.owner_sid), ORIGINAL_LOGIN(), r.family_guid,
                   CONVERT(int, SERVERPROPERTY('ProcessID')), d.is_read_only,
                   (SELECT COUNT(*) FROM sys.synonyms),
                   (SELECT COUNT(*) FROM sys.sql_expression_dependencies
                    WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL)
            FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id
            WHERE d.database_id=DB_ID() AND d.state=0 AND d.user_access=0 AND NOT EXISTS (SELECT 1 FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0);
            SELECT physical_name FROM sys.database_files WHERE state=0 AND type IN (0,1) ORDER BY file_id;
            SELECT COUNT(*) FROM sys.database_files;
            SELECT net_transport, encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID;
            """;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct) || Enumerable.Range(0, 9).Any(reader.IsDBNull)) throw AuthOnlyTestingGuard.Refuse();
            var server = reader.GetString(0);
            var database = reader.GetString(1);
            var owner = reader.GetString(2);
            var actor = reader.GetString(3);
            var family = reader.GetGuid(4);
            var processId = reader.GetInt32(5);
            var readOnly = reader.GetBoolean(6);
            var synonyms = reader.GetInt32(7);
            var external = reader.GetInt32(8);
            if (await reader.ReadAsync(ct) || !await reader.NextResultAsync(ct)) throw AuthOnlyTestingGuard.Refuse();
            var files = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0)) throw AuthOnlyTestingGuard.Refuse();
                files.Add(reader.GetString(0));
            }
            if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct) || reader.GetInt32(0) != 2)
                throw AuthOnlyTestingGuard.Refuse();
            AuthOnlyTestingGuard.ValidateIdentity(server, database, owner, actor, family, processId, readOnly, synonyms, external, files);
            if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct) || reader.IsDBNull(0) || reader.IsDBNull(1))
                throw AuthOnlyTestingGuard.Refuse();
            AuthOnlyTestingGuard.ValidateTransport(reader.GetString(0), reader.GetString(1), allowUnencryptedLocalPipe);
            if (await reader.ReadAsync(ct)) throw AuthOnlyTestingGuard.Refuse();
        }
        AuthOnlyTestingGuard.ValidateSchema(db.Database.GetMigrations().ToArray(),
            (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray());
        // Expired pending/dispatching rows are still historical work: never reclaim or clean them here.
        if (await db.Set<OtpDelivery>().AsNoTracking().AnyAsync(NonterminalDelivery, ct))
            throw new InvalidOperationException("C02 cannot start with pre-existing nonterminal OTP deliveries. No cleanup was performed.");
    }
}


