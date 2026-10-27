using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

internal static class HrVehicleV2ContinuationDatabase
{
    internal static readonly Guid ReceiptId = new("f15ec966-27d4-41bf-94d9-259ddff3c06a");

    internal static async Task VerifyAsync(CosmixDbContext db, CancellationToken ct)
    {
        if (!HrVehicleV2ContinuationGuard.IsSelected) throw HrVehicleV2Guard.Refuse();
        var manifest = HrVehicleV2ContinuationFiles.ReadManifest();
        var tables = manifest.GetProperty("SqlTableMemberships");
        var catalog = await SelectAsync(db, "SELECT s.name+'.'+t.name AS Name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH", ct);
        if (tables.EnumerateObject().Count() != 162 || !tables.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(catalog.EnumerateArray().Select(x => x.GetProperty("Name").GetString()!).Order(StringComparer.Ordinal)))
            throw HrVehicleV2Guard.Refuse();
        // All baseline members and all columns are checked before normal authentication can write.
        foreach (var table in tables.EnumerateObject())
        {
            var name = QuoteTable(table.Name);
            var pk = await SelectAsync(db, $"SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('{name}') AND i.is_primary_key=1 ORDER BY ic.key_ordinal FOR JSON PATH", ct);
            if (pk.GetArrayLength() == 0) throw HrVehicleV2Guard.Refuse();
            var columns = string.Join(",", pk.EnumerateArray().Select(x => "t." + Quote(x.GetProperty("name").GetString()!)));
            var actual = await SelectAsync(db, $"SELECT JSON_QUERY((SELECT {columns} FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) AS [Key],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)),2) AS Sha256 FROM {name} t ORDER BY {columns} FOR JSON PATH", ct);
            if (!JsonElement.DeepEquals(table.Value, actual)) throw HrVehicleV2Guard.Refuse();
        }
        var open = await SelectAsync(db, "SELECT Id FROM auth.MobileOtpChallenges WHERE Purpose='WebLogin' AND ConsumedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt>SYSUTCDATETIME() FOR JSON PATH", ct);
        if (open.GetArrayLength() != 0) throw HrVehicleV2Guard.Refuse();
        await VerifyPendingReceiptAsync(db, ct);
    }

    internal static async Task VerifyPendingReceiptAsync(CosmixDbContext db, CancellationToken ct)
    {
        if (!HrVehicleV2ContinuationGuard.IsSelected) throw HrVehicleV2Guard.Refuse();
        var manifest = HrVehicleV2ContinuationFiles.ReadManifest();
        var expected = manifest.GetProperty("SqlTableMemberships").GetProperty("workflow.IdempotencyReceipts")
            .EnumerateArray().Single(x => x.GetProperty("Key").GetProperty("Id").GetGuid() == ReceiptId).GetProperty("Sha256").GetString();
        var actual = await SelectAsync(db, $"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)),2) AS Sha256 FROM workflow.IdempotencyReceipts t WHERE Id='{ReceiptId:D}' AND ExpiresAt>SYSUTCDATETIME() AND CompletedAt IS NOT NULL FOR JSON PATH", ct);
        if (actual.GetArrayLength() != 1 || actual[0].GetProperty("Sha256").GetString() != expected) throw HrVehicleV2Guard.Refuse();
    }

    private static string QuoteTable(string value) => string.Join(".", value.Split('.').Select(Quote));
    private static string Quote(string value) => Regex.IsMatch(value, @"\A[A-Za-z_][A-Za-z0-9_]*\z") ? "[" + value + "]" : throw HrVehicleV2Guard.Refuse();
    private static async Task<JsonElement> SelectAsync(CosmixDbContext db, string sql, CancellationToken ct)
    {
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var text = new StringBuilder();
        while (await reader.ReadAsync(ct)) text.Append(reader.GetString(0));
        using var json = JsonDocument.Parse(text.Length == 0 ? "[]" : text.ToString());
        return json.RootElement.Clone();
    }
}
