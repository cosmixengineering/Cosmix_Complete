using System.Linq.Expressions;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

internal static class HrOutstationO1Database
{
    internal static readonly Expression<Func<OtpDelivery, bool>> NonterminalDelivery = d =>
        d.Status != OtpDeliveryStatus.Submitted && d.Status != OtpDeliveryStatus.Failed && d.Status != OtpDeliveryStatus.Expired;

    // Called on the same already-open context used by the normal account/model/migration guard.
    internal static async Task VerifyAsync(CosmixDbContext db, IConfiguration configuration, CancellationToken ct, bool allowUnencryptedLocalPipe = false)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
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
            if (!await reader.ReadAsync(ct) || Enumerable.Range(0, 9).Any(reader.IsDBNull)) throw HrOutstationO1Guard.Refuse();
            var server = reader.GetString(0);
            var database = reader.GetString(1);
            var owner = reader.GetString(2);
            var actor = reader.GetString(3);
            var family = reader.GetGuid(4);
            var processId = reader.GetInt32(5);
            var readOnly = reader.GetBoolean(6);
            var synonyms = reader.GetInt32(7);
            var external = reader.GetInt32(8);
            if (await reader.ReadAsync(ct) || !await reader.NextResultAsync(ct)) throw HrOutstationO1Guard.Refuse();
            var files = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0)) throw HrOutstationO1Guard.Refuse();
                files.Add(reader.GetString(0));
            }
            if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct) || reader.GetInt32(0) != 2)
                throw HrOutstationO1Guard.Refuse();
            HrOutstationO1Guard.ValidateIdentity(server, database, owner, actor, family, processId, readOnly, synonyms, external, files);
            if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct) || reader.IsDBNull(0) || reader.IsDBNull(1))
                throw HrOutstationO1Guard.Refuse();
            HrOutstationO1Guard.ValidateTransport(reader.GetString(0), reader.GetString(1), allowUnencryptedLocalPipe);
            if (await reader.ReadAsync(ct)) throw HrOutstationO1Guard.Refuse();
        }
        HrOutstationO1Guard.ValidateSchema(db.Database.GetMigrations().ToArray(),
            (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray());
        command.CommandText = """
            SELECT COUNT(*) FROM auth.Roles WHERE Code LIKE 'TEST-HR-P1-20261020%';
            SELECT COUNT(*) FROM auth.Permissions WHERE Code IN
                ('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read',
                 'attendance.deployments.request','attendance.deployments.decide');
            SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'O1-%';
            SELECT COUNT(*) FROM auth.UserAccounts;
            SELECT a.Id,a.EmployeeId,a.LoginName,e.ApprovedLoginPhoneNumber,e.FullLegalName
            FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId
            WHERE a.LoginName IN ('hr@example.invalid','admin@example.invalid')
              AND a.Status=3 AND a.WebAccessEnabled=1 AND a.MfaRequired=1
              AND (a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME())
            ORDER BY a.LoginName;
            """;
        await using (var rows = await command.ExecuteReaderAsync(ct))
        {
            foreach (var expected in new[] { 2, 5, 0, 3 })
            {
                if (!await rows.ReadAsync(ct) || rows.GetInt32(0) != expected ||
                    await rows.ReadAsync(ct) || !await rows.NextResultAsync(ct)) throw HrOutstationO1Guard.Refuse();
            }
            var expectedActors = new[]
            {
                ("94a83b62-5b56-4450-9175-2a7a9e3261bf","7c505086-87ea-4fd2-980e-823ecccbe36a","admin@example.invalid","+12025550102","Engr. Tariq Mehmood (Site Supervisor)"),
                ("8475187f-bf3b-4f9c-a3ed-4bac32253005","35d19922-70a8-4c80-8578-e1029360e9fc","hr@example.invalid","+12025550103","Muhammad Rashid (Lead Engineer)")
            };
            foreach (var actor in expectedActors)
                if (!await rows.ReadAsync(ct) || rows.GetGuid(0) != Guid.Parse(actor.Item1) ||
                    rows.GetGuid(1) != Guid.Parse(actor.Item2) || rows.GetString(2) != actor.Item3 ||
                    rows.GetString(3) != actor.Item4 || rows.GetString(4) != actor.Item5) throw HrOutstationO1Guard.Refuse();
            if (await rows.ReadAsync(ct)) throw HrOutstationO1Guard.Refuse();
        }
        if (await db.Set<OtpDelivery>().AsNoTracking().AnyAsync(NonterminalDelivery, ct))
            throw HrOutstationO1Guard.Refuse();
    }
}
