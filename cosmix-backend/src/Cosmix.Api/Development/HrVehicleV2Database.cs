using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2Database
{
    // Read-only, no entity tracking. Safe to call on a fresh context immediately before SeedAsync.
    internal static async Task VerifyAsync(CosmixDbContext db, CancellationToken ct, bool allowUnencryptedLocalPipe = false)
    {
        if (!HrVehicleV2ContinuationGuard.AnySelected) throw HrVehicleV2Guard.Refuse();
        if (HrVehicleV2ContinuationGuard.IsSelected) HrVehicleV2ContinuationGuard.ValidateConnection(db.Database.GetConnectionString());
        else HrVehicleV2Guard.ValidateConnection(db.Database.GetConnectionString());
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),SUSER_SNAME(d.owner_sid),
                ORIGINAL_LOGIN(),r.family_guid,CONVERT(int,SERVERPROPERTY('ProcessID')),d.is_read_only,
                (SELECT COUNT(*) FROM sys.synonyms),
                (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL)
            FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id
            WHERE d.database_id=DB_ID() AND d.state=0 AND d.user_access=0
                AND NOT EXISTS (SELECT 1 FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0);
            SELECT physical_name FROM sys.database_files WHERE state=0 AND type IN (0,1) ORDER BY file_id;
            SELECT COUNT(*) FROM sys.database_files;
            SELECT net_transport,encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID;
            """;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct) || Enumerable.Range(0, 9).Any(reader.IsDBNull)) throw HrVehicleV2Guard.Refuse();
            var server = reader.GetString(0); var database = reader.GetString(1); var owner = reader.GetString(2);
            var actor = reader.GetString(3); var family = reader.GetGuid(4); var pid = reader.GetInt32(5);
            var readOnly = reader.GetBoolean(6); var synonyms = reader.GetInt32(7); var external = reader.GetInt32(8);
            if (await reader.ReadAsync(ct) || !await reader.NextResultAsync(ct)) throw HrVehicleV2Guard.Refuse();
            var files = new List<string>();
            while (await reader.ReadAsync(ct)) files.Add(reader.GetString(0));
            HrVehicleV2Guard.ValidateIdentity(server, database, owner, actor, family, pid, readOnly, synonyms, external, files);
            if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct) || reader.GetInt32(0) != 2 ||
                !await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct)) throw HrVehicleV2Guard.Refuse();
            HrOutstationO1Guard.ValidateTransport(reader.GetString(0), reader.GetString(1), allowUnencryptedLocalPipe);
            if (await reader.ReadAsync(ct)) throw HrVehicleV2Guard.Refuse();
        }
        HrVehicleV2Guard.ValidateSchema(db.Database.GetMigrations().ToArray(), (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray());
        command.CommandText = """
            SELECT a.Id,a.EmployeeId,a.LoginName,e.FullLegalName,e.ApprovedLoginPhoneNumber
            FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId
            WHERE a.Status=3 AND a.WebAccessEnabled=1 AND a.MfaRequired=1
                AND (a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME());
            SELECT COUNT(*) FROM auth.UserAccounts;
            """;
        await using var accounts = await command.ExecuteReaderAsync(ct);
        var seen = new HashSet<Guid>();
        while (await accounts.ReadAsync(ct))
        {
            var pin = HrVehicleV2Guard.Actors.SingleOrDefault(p => p.Account == accounts.GetGuid(0));
            if (pin is null || !seen.Add(pin.Account) || accounts.GetGuid(1) != pin.Employee ||
                accounts.GetString(2) != pin.Email || accounts.GetString(3) != pin.Name || accounts.GetString(4) != pin.Phone)
                throw HrVehicleV2Guard.Refuse();
        }
        if (seen.Count != 3 || !await accounts.NextResultAsync(ct) || !await accounts.ReadAsync(ct) || accounts.GetInt32(0) != 3)
            throw HrVehicleV2Guard.Refuse();
    }

    internal static async Task VerifyUnusedAsync(CosmixDbContext db, CancellationToken ct)
    {
        if (await db.Set<OtpDelivery>().AnyAsync(HrOutstationO1Database.NonterminalDelivery, ct) ||
            await db.Set<Vehicle>().AnyAsync(v => v.RegistrationNumber == HrVehicleV2Guard.Marker, ct) ||
            await db.Policies.AnyAsync(p => p.Category == PolicyCategory.VehicleFuel, ct) ||
            await db.Set<VehicleTrip>().AnyAsync(ct) || await db.Set<FuelClaim>().AnyAsync(ct) ||
            await db.Set<VehicleMileageEntry>().AnyAsync(ct) || await db.Set<VehicleServiceRecord>().AnyAsync(ct))
            throw HrVehicleV2Guard.Refuse();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'TEST-HR-V2-20261026-%';";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) throw HrVehicleV2Guard.Refuse();
    }
}
