using System.Collections;
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Reports;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Repository SQL verification only. No HTTP, authentication simulation, fixture or schema writes.
const string connectionString = @"Server=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=false;Connect Timeout=5;Pooling=false;Application Name=Cosmix.HrReports.ReadOnly20261020";
var employee = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc");
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
async Task<string> Scalar(string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.CommandTimeout = 15;
    return Convert.ToString(await command.ExecuteScalarAsync()) ?? "";
}
var identity = await Scalar("""
SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',DB_NAME(),'|',ORIGINAL_LOGIN(),'|',
SUSER_SNAME(d.owner_sid),'|',CONVERT(varchar(36),r.family_guid),'|',d.state_desc,'|',
CONVERT(int,SERVERPROPERTY('ProcessID'))) FROM sys.databases d
JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID()
""");
if (!identity.Equals("Software-PC1\\LOCALDB#9F3001F7|Cosmix_Testing|COSMIXRND\\Huma.Khan|COSMIXRND\\Huma.Khan|d9c34627-18a2-41cf-a804-821e39516303|ONLINE|25768", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("STOP: approved SQL identity differs.");
var paths = await Scalar("SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files");
if (!paths.Equals(@"C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("STOP: database files differ.");
if (await Scalar("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory") != "25")
    throw new InvalidOperationException("STOP: migration history differs.");
var guard = new ReadOnlyCommands();
await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection).AddInterceptors(guard, new NoSaves()).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);
var schemas = new HashSet<string>(["hr", "reports", "jobs", "workflow", "audit", "files"], StringComparer.Ordinal);
var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await using (var metadata = connection.CreateCommand())
{
    metadata.CommandTimeout = 15;
    metadata.CommandText = """
SELECT s.name + '.' + t.name + '.' + c.name
FROM sys.schemas s JOIN sys.tables t ON t.schema_id=s.schema_id
JOIN sys.columns c ON c.object_id=t.object_id
WHERE s.name IN ('hr','reports','jobs','workflow','audit','files')
""";
    await using var reader = await metadata.ExecuteReaderAsync();
    while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
}
var requiredTables = db.Model.GetRelationalModel().Tables.Where(t => schemas.Contains(t.Schema ?? "dbo")).ToArray();
var requiredColumns = requiredTables.SelectMany(t => t.Columns.Select(c => $"{t.Schema}.{t.Name}.{c.Name}")).ToArray();
var missing = requiredColumns.Where(c => !columns.Contains(c)).ToArray();
if (missing.Length != 0) throw new InvalidOperationException("STOP: mapped HR/storage columns absent: " + string.Join(", ", missing));
Console.WriteLine($"PASS mapped column presence: {requiredColumns.Length} columns across {requiredTables.Length} HR/support tables. Types, constraints and write behavior are separate checks.");
var account = await db.UserAccounts.Where(x => x.Id == Guid.Parse("8475187f-bf3b-4f9c-a3ed-4bac32253005"))
    .Select(x => new { x.EmployeeId, x.LoginName }).SingleAsync();
if (account.EmployeeId != employee || account.LoginName != "hr@example.invalid")
    throw new InvalidOperationException("STOP: synthetic account binding differs.");
var rosters = DispatchProxy.Create<IRosterRepository, NoSyntheticScope>();
var repository = new HrReportRepository(db, rosters);
var date = new DateOnly(2026, 10, 20);
var filter = new HrReportFilter(From: date, To: date, Date: date, Year: 2026, Month: 10,
    EmployeeId: employee, Page: 1, PageSize: 10);
var methods = typeof(HrReportRepository).GetMethods()
    .Where(x => x.Name.StartsWith("Get", StringComparison.Ordinal) && x.Name.EndsWith("Async", StringComparison.Ordinal))
    .OrderBy(x => x.Name).ToArray();
if (methods.Length != 14) throw new InvalidOperationException("STOP: report surface changed.");
var checks = 0;
foreach (var method in methods)
{
    foreach (var ids in new[] { new[] { employee }, Array.Empty<Guid>() })
    {
        // Explicit repository selection, not an authorization verdict or a fabricated permission grant.
        var selection = new HrReportSelection(ids, false);
        var task = (Task)method.Invoke(repository, [filter, selection, CancellationToken.None])!;
        await task;
        var rows = ((IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!).Cast<object>().ToArray();
        if (rows.Length > 10 || ids.Length == 0 && rows.Length != 0)
            throw new InvalidOperationException("STOP: report row bound/empty selection failed: " + method.Name);
        foreach (var row in rows)
        {
            var id = row.GetType().GetProperty("EmployeeId");
            if (id is not null && id.GetValue(row) is Guid value && value != employee)
                throw new InvalidOperationException("STOP: foreign employee row: " + method.Name);
        }
        if (method.Name == "GetEmployeeMasterAsync" && ids.Length == 1 && rows.Length != 1)
            throw new InvalidOperationException("STOP: expected synthetic employee missing.");
        checks++;
        Console.WriteLine($"PASS {method.Name}, scope={(ids.Length == 0 ? "empty" : "synthetic HR")}, rows={rows.Length}");
    }
}
if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("STOP: tracked changes unexpected.");
Console.WriteLine($"PASS {checks}/28 actual repository SQL checks; {guard.Count} SELECT commands. No writes or authenticated HTTP acceptance claimed.");

public sealed class NoSaves : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result) =>
        throw new InvalidOperationException("SQL writes forbidden.");
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken ct = default) => throw new InvalidOperationException("SQL writes forbidden.");
}
public sealed class ReadOnlyCommands : DbCommandInterceptor
{
    public int Count { get; private set; }
    private void Check(DbCommand command)
    {
        if (command.CommandType != System.Data.CommandType.Text ||
            !command.CommandText.TrimStart().StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase) ||
            command.CommandText.Contains(';') || ++Count > 250)
            throw new InvalidOperationException("Non-SELECT or excessive query rejected.");
        command.CommandTimeout = 15;
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result) { Check(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Check(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data,
        InterceptionResult<int> result) => throw new InvalidOperationException("Non-query rejected.");
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken ct = default) => throw new InvalidOperationException("Non-query rejected.");
}
public class NoSyntheticScope : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        throw new InvalidOperationException("Unexpected roster resolution; no synthetic authorization is supplied.");
}
