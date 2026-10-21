using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cosmix.Api.Development;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Reports;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

var passed = 0;
var failed = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed.Add(name + ": " + error.GetBaseException().Message); Console.WriteLine("FAIL " + failed[^1]); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (Exception error) when (error is InvalidOperationException or JsonException or Cosmix.Application.Authentication.AuthenticationFailure or DbUpdateException or IOException or OperationCanceledException) { return; }
    throw new Exception("Expected fail-closed rejection");
}
Task<DurableJobClaim?> Claim(Fixture f, HrReportAcceptanceJobRepository? adapter = null) =>
    (adapter ?? f.Adapter()).ClaimNextAsync([HrReportExportRules.MessageType], f.Now, TimeSpan.FromSeconds(120), default);

await Test("exact pending job claims with actual EF and domain types", async () =>
{
    using var f = new Fixture();
    var claim = await f.Adapter().ClaimNextAsync([HrReportExportRules.MessageType], f.Now, TimeSpan.FromSeconds(120), default);
    Check(claim?.MessageId == f.Message.Id && claim.Attempt == 1 && claim.LeaseToken != Guid.Empty, "Exact first claim");
    Check(f.Connection.Updates == 1 && f.Connection.Commits == 1, "One transactional update");
    Check(f.Connection.LastUpdate.Contains("[RowVersion] = ", StringComparison.Ordinal), "EF concurrency predicate");
    Check(!f.Db.ChangeTracker.Entries().Any() && f.Db.Database.CurrentTransaction is null, "Clean scope after claim");
    Check(f.Connection.LastIsolation == IsolationLevel.Serializable, "Serializable claim transaction");
});

foreach (var (column, value) in new (string, object)[] {
    ("Server","wrong"), ("Database","OtherDatabase"), ("Owner","other"), ("Operator","other"),
    ("Family",Guid.NewGuid()), ("ProcessId",1), ("ReadOnly",true), ("Synonyms",1), ("ExternalDependencies",1),
    ("DataFile","other.mdf"), ("LogFile","other.ldf"), ("FileCount",3), ("Transport","TCP"), ("Encrypted","unknown") })
await Test("target rejects " + column, async () =>
{
    using var f = new Fixture();
    f.Connection.ChangeIdentity = t => t.Rows[0][column] = value;
    await Reject(() => Claim(f));
    Check(f.Connection.Updates == 0 && f.Files.Reads == 0 && f.Connection.Commits == 0, "No claim or protected IO on wrong target");
});

foreach (var change in new Func<SqlConnectionStringBuilder, string>[] {
    c => { c.DataSource="other"; return c.ConnectionString; },
    c => { c.InitialCatalog="other"; return c.ConnectionString; },
    c => { c.ApplicationName="other"; return c.ConnectionString; },
    c => { c.IntegratedSecurity=false; return c.ConnectionString; },
    c => { c.Encrypt=false; return c.ConnectionString; },
    c => { c.AttachDBFilename="other"; return c.ConnectionString; } })
await Test("connection rejects changed setting", async () =>
{
    using var f = new Fixture();
    f.Connection.ConnectionString = change(new SqlConnectionStringBuilder(f.Connection.ConnectionString));
    await Reject(() => Claim(f));
    Check(f.Connection.Commands == 0, "Rejected before any command");
});

foreach (var (label, change) in new (string, Func<HrReportAcceptanceCapture,HrReportAcceptanceCapture>)[] {
    ("job", c => c with { JobId=Guid.NewGuid() }),
    ("outbox", c => c with { MessageId=Guid.NewGuid() }),
    ("owner", c => c with { Requester=c.Requester with { AccountId=Guid.NewGuid() } }),
    ("employee", c => c with { Requester=c.Requester with { EmployeeId=Guid.NewGuid() } }),
    ("MFA", c => c with { Requester=c.Requester with { MfaSatisfied=false } }),
    ("session", c => c with { Requester=c.Requester with { SessionId=Guid.NewGuid() } }),
    ("empty session", c => c with { Requester=c.Requester with { SessionId=Guid.Empty } }),
    ("security version", c => c with { Requester=c.Requester with { SecurityVersion=2 } }),
    ("filter employee", c => c with { Filter=c.Filter with { EmployeeId=Guid.NewGuid() } }),
    ("broad filter", c => c with { Filter=c.Filter with { EmployeeId=null } }),
    ("page", c => c with { Filter=c.Filter with { Page=2 } }),
    ("page size", c => c with { Filter=c.Filter with { PageSize=1 } }),
    ("range", c => c with { Filter=c.Filter with { To=c.Filter.To!.Value.AddDays(1) } }),
    ("correlation", c => c with { CorrelationId="different" }),
    ("requested timestamp", c => c with { RequestedAt=c.RequestedAt.AddSeconds(-1) }),
    ("payload reference", c => c with { ProtectedScopeReference="{}" }) })
await Test("captured binding rejects " + label, async () =>
{
    using var f = new Fixture();
    await Reject(() => Claim(f, f.Adapter(change(f.Capture))));
    Check(f.Connection.Updates == 0 && f.Connection.Commits == 0, "No claim");
});

foreach (var (field, value) in new (string, object)[] {
    ("RequestedByUserId",Guid.NewGuid().ToString()), ("ReportType","attendance.report"), ("Format",1),
    ("State",1), ("ProtectedFilterScopeReference","{}"), ("PeriodFrom",new DateOnly(2026,10,19)),
    ("StoredFileId",Guid.NewGuid()), ("SourceLockReference","other"), ("FailureCode","old"),
    ("RowVersion",Array.Empty<byte>()) })
await Test("stored report rejects " + field, async () =>
{
    using var f = new Fixture(); f.Connection.JobOverrides[field] = value;
    await Reject(() => Claim(f));
    Check(f.Connection.Updates == 0, "No claim on mismatched report");
});

foreach (var (field, value) in new (string, object)[] {
    ("MessageType","notification.in-app.v1"), ("DeduplicationKey",Guid.NewGuid().ToString("N")),
    ("EncryptedPayloadReference","{}"), ("CorrelationId","other"), ("State",1), ("State",2), ("State",3),
    ("Attempts",1), ("LeaseToken",Guid.NewGuid()), ("LeaseExpiresAt",new DateTimeOffset(2026,10,20,9,0,0,TimeSpan.Zero)),
    ("CompletedAt",new DateTimeOffset(2026,10,20,8,0,0,TimeSpan.Zero)), ("LastFailureCode","old"),
    ("AvailableAt",new DateTimeOffset(2026,10,20,8,1,0,TimeSpan.Zero)), ("RowVersion",Array.Empty<byte>()) })
await Test("stored outbox rejects " + field + "=" + value, async () =>
{
    using var f = new Fixture(); f.Connection.MessageOverrides[field] = value;
    await Reject(() => Claim(f));
    Check(f.Connection.Updates == 0, "No claim on mismatched outbox");
});

foreach (var (label, mutate) in new (string, Action<JsonObject>)[] {
    ("report type", j => j["ReportType"]="audit"),
    ("format", j => j["Format"]=1),
    ("requester", j => j["Requester"]!["AccountId"]=Guid.NewGuid()),
    ("employee", j => j["Requester"]!["EmployeeId"]=Guid.NewGuid()),
    ("session", j => j["Requester"]!["SessionId"]=Guid.NewGuid()),
    ("filter", j => j["Filter"]!["EmployeeId"]=Guid.NewGuid()),
    ("selection", j => j["Selection"]!["EmployeeIds"]=new JsonArray(JsonValue.Create(Guid.NewGuid()))),
    ("empty selection", j => j["Selection"]!["EmployeeIds"]=new JsonArray()),
    ("broad selection", j => j["Selection"]!["EmployeeIds"]!.AsArray().Add(Guid.NewGuid())),
    ("scope alternative", j => j["Selection"]!["ScopeGroups"]![0]!["Alternatives"]![0]!["EmployeeId"]=Guid.NewGuid()),
    ("unscoped audit", j => j["Selection"]!["IncludeUnscopedAudit"]=true),
    ("empty roles", j => j["Selection"]!["ReadRoleAssignmentIds"]=new JsonArray()),
    ("unknown property", j => j["Unknown"]=true),
    ("case alias", j => j["reportType"]="audit") })
await Test("authenticated payload rejects " + label, async () =>
{
    using var f = new Fixture();
    var data = JsonNode.Parse(f.Files.Bytes)!.AsObject(); mutate(data);
    f.Files.Bytes = JsonSerializer.SerializeToUtf8Bytes(data);
    var reference = JsonSerializer.Serialize(f.Files.Reference);
    f.Connection.JobOverrides["ProtectedFilterScopeReference"] = reference;
    f.Connection.MessageOverrides["EncryptedPayloadReference"] = reference;
    await Reject(() => Claim(f, f.Adapter(f.Capture with { ProtectedScopeReference=reference })));
    Check(f.Connection.Updates == 0, "No claim for substituted scoped content");
});

await Test("protected plaintext digest mismatch", async () =>
{
    using var f = new Fixture(); f.Files.Bytes[0] ^= 1;
    await Reject(() => Claim(f)); Check(f.Connection.Updates == 0, "Hash before parse/claim");
});
await Test("foreign handler type cannot select a queue", async () =>
{
    using var f = new Fixture();
    await Reject(() => f.Adapter().ClaimNextAsync(["notification.in-app.v1"],f.Now,TimeSpan.FromSeconds(120),default));
    Check(f.Connection.Commands == 0, "No foreign claim query");
});
await Test("rowversion conflict rolls back and never returns claim", async () =>
{
    using var f = new Fixture(); f.Connection.Conflict = true;
    await Reject(() => Claim(f));
    Check(f.Connection.Updates == 1 && f.Connection.Commits == 0 && f.Connection.Persisted.Count == 0 &&
        !f.Db.ChangeTracker.Entries().Any(), "No persisted claim after zero-row update");
});
await Test("commit failure rolls back and never returns claim", async () =>
{
    using var f = new Fixture(); f.Connection.FailCommit = true;
    var adapter = f.Adapter();
    await Reject(() => Claim(f,adapter)); await Reject(() => Claim(f,adapter));
    Check(f.Connection.Updates == 1 && f.Connection.Commits == 0 && f.Connection.Persisted.Count == 0, "No implicit retry");
});
await Test("lost commit acknowledgement remains one shot", async () =>
{
    using var f = new Fixture(); f.Connection.LostAcknowledgement = true;
    var adapter = f.Adapter();
    await Reject(() => Claim(f,adapter)); await Reject(() => Claim(f,adapter));
    Check(f.Connection.Updates == 1 && f.Connection.Commits == 1 &&
        Convert.ToInt32(f.Connection.Persisted["Attempts"]) == 1, "Committed ambiguity cannot claim again");
});
await Test("cancellation before claim does no IO", async () =>
{
    using var f = new Fixture(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
    await Reject(() => f.Adapter().ClaimNextAsync([HrReportExportRules.MessageType],f.Now,TimeSpan.FromSeconds(120),cancel.Token));
    Check(f.Connection.Commands == 0, "No IO");
});
await Test("cancellation during protected read rolls back", async () =>
{
    using var f = new Fixture(); f.Files.Cancel = true;
    await Reject(() => Claim(f)); Check(f.Connection.Updates == 0 && f.Connection.Commits == 0, "No save");
});
await Test("dirty context rejected before target commands", async () =>
{
    using var f = new Fixture(); f.Db.Add(f.Message);
    await Reject(() => Claim(f)); Check(f.Connection.Commands == 0 && f.Db.ChangeTracker.HasChanges(), "Caller changes preserved");
});
await Test("second claim and forged completion cannot dispatch", async () =>
{
    using var f = new Fixture(); var adapter = f.Adapter(); var claim = (await Claim(f,adapter))!;
    await Reject(() => Claim(f,adapter));
    await Reject(() => adapter.CompleteAsync(claim with { MessageId=Guid.NewGuid() }, (_,_)=>Task.CompletedTask, default));
    await Reject(() => adapter.FailAsync(claim with { LeaseToken=Guid.NewGuid() }, "failure", null, default));
    Check(f.Connection.Updates == 1, "No second claim or foreign terminal action");
});
await Test("completion delegates real lease lock and transactional callback", async () =>
{
    using var f = new Fixture(); var adapter = f.Adapter(); var claim = (await Claim(f,adapter))!;
    var invoked = false;
    await Reject(() => adapter.CompleteAsync(claim, (message,_) =>
    {
        invoked = true;
        Check(f.Db.Database.CurrentTransaction is not null && message.Id == claim.MessageId &&
            message.LeaseToken == claim.LeaseToken && message.Attempts == 1, "Real completion binding");
        throw new IOException("Synthetic callback abort");
    }, default));
    Check(invoked && f.Connection.Locks == 1 && f.Connection.Commits == 1 && f.Connection.Updates == 1,
        "Existing completion transaction aborts without new writes");
});
await Test("failure delegates real lease lock without accepting a changed lease", async () =>
{
    using var f = new Fixture(); var adapter = f.Adapter(); var claim = (await Claim(f,adapter))!;
    f.Connection.MessageOverrides["LeaseToken"] = Guid.NewGuid();
    Check(!await adapter.FailAsync(claim,"synthetic_failure",null,default), "Stale lease returns false");
    Check(f.Connection.Locks == 1 && f.Connection.Updates == 1, "No failure write to wrong lease");
});
await Test("completion succeeds through real repository domain transition", async () =>
{
    using var f = new Fixture(); var adapter = f.Adapter(); var claim = (await Claim(f,adapter))!;
    Check(await adapter.CompleteAsync(claim,(_,_)=>Task.CompletedTask,default), "Completion committed");
    Check(Convert.ToInt32(f.Connection.Persisted["State"]) == (int)DispatchState.Completed &&
        f.Connection.Persisted["LeaseToken"] == DBNull.Value && f.Connection.Commits == 2, "Domain lease cleared atomically");
});
await Test("dispatch facade rejects absent profile before any IO", async () =>
{
    using var f = new Fixture();
    var previous = Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST");
    try
    {
        Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST",null);
        await Reject(() => new HrReportTestingDispatcher(f.Db,f.Files,new Clock(f.Now),null!)
            .DispatchAsync(f.Job.Id,f.Actor,default));
        Check(f.Connection.Commands == 0 && f.Files.Reads == 0,"No implicit dispatch activation");
    }
    finally { Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST",previous); }
});
await Test("normal producer methods remain delegated", async () =>
{
    using var f = new Fixture(); var adapter = f.Adapter();
    await Reject(() => { adapter.Stage(f.Message); return Task.CompletedTask; });
    await f.Db.Database.OpenConnectionAsync();
    await using var transaction = await f.Db.Database.BeginTransactionAsync();
    adapter.RequireProducerTransaction(); adapter.Stage(f.Message);
    Check(f.Db.OutboxMessages.Local.Single() == f.Message, "Real repository stages in same context");
});

Console.WriteLine($"Acceptance job adapter: {passed}/{passed + failed.Count} offline checks passed.");
return failed.Count == 0 ? 0 : 1;

sealed class Fixture : IDisposable
{
    public readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    public readonly AuthenticatedAccount Actor = new(
        new("8475187f-bf3b-4f9c-a3ed-4bac32253005"), new("35d19922-70a8-4c80-8578-e1029360e9fc"),
        Guid.NewGuid(), 1, SessionClient.Web, true);
    public readonly HrReportFilter Filter = new(From: new(2026, 10, 20), To: new(2026, 10, 20),
        Date: new(2026, 10, 20), Year: 2026, Month: 10,
        EmployeeId: new("35d19922-70a8-4c80-8578-e1029360e9fc"), Page: 1, PageSize: 1000);
    public readonly HrReportAcceptanceTarget Target = new(
        @"np:\\.\pipe\LOCALDB#9F3001F7\tsql\query", @"Software-PC1\LOCALDB#9F3001F7", @"COSMIXRND\Huma.Khan",
        new("D9C34627-18A2-41CF-A804-821E39516303"), 25768,
        @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf",
        "Cosmix.HrReports.R1", true);
    public readonly MemoryFiles Files;
    public readonly ReportExportJob Job;
    public readonly OutboxMessage Message;
    public readonly HrReportAcceptanceCapture Capture;
    public readonly ProxyConnection Connection;
    public readonly CosmixDbContext Db;
    public Fixture()
    {
        var selection = new HrReportSelection([Actor.EmployeeId], false)
        {
            ReadRoleAssignmentIds = [Guid.NewGuid()],
            ScopeGroups = [new AttendanceResourceScope([new(EmployeeId: Actor.EmployeeId)])]
        };
        Files = new(new HrReportExportScope(1, Actor, HrReportTypes.EmployeeMaster, ReportFormat.Pdf, Filter, selection));
        var reference = JsonSerializer.Serialize(Files.Reference);
        Job = new(Actor.AccountId.ToString(), HrReportExportRules.ReportType, ReportFormat.Pdf, reference, Now, Filter.From, Filter.To);
        Message = new(HrReportExportRules.MessageType, reference, Now, "actual-server-trace", Job.Id.ToString("N"));
        Capture = new(Job.Id, Message.Id, Actor, Filter, reference, Message.CorrelationId, Now);
        Connection = new(Target);
        Db = new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(Connection).Options);
        Connection.Db = Db;
        Connection.Job = Job;
        Connection.Message = Message;
    }
    public HrReportAcceptanceJobRepository Adapter(HrReportAcceptanceCapture? capture = null) =>
        new(Db, Target, capture ?? Capture, Files, new Clock(Now));
    public void Dispose() { Db.Dispose(); Connection.Dispose(); }
}

sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

sealed class MemoryFiles(HrReportExportScope scope) : IPrivateFileStore
{
    public byte[] Bytes = JsonSerializer.SerializeToUtf8Bytes(scope);
    public PrivateFileReference Reference => new("synthetic-scope", "1", Convert.ToHexString(SHA256.HashData(Bytes)), "hr-reports-r1", Bytes.Length);
    public int Reads;
    public bool Cancel;
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Reads++;
        if (Cancel) throw new OperationCanceledException("Synthetic cancellation");
        if (purpose != HrReportExportRules.FilterPurpose(scope.Requester.AccountId)) throw new Exception("Wrong scope purpose");
        return Task.FromResult(Bytes.ToArray());
    }
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct) =>
        throw new Exception("Private writes forbidden in this suite.");
}

// The SQL Server provider generates and materializes real EF commands. Only ADO IO is synthetic.
// No connection here can open a socket or named pipe; unsupported commands fail closed.
sealed class ProxyConnection(HrReportAcceptanceTarget target) : DbConnection
{
    private ConnectionState state;
    [AllowNull] public override string ConnectionString { get; set; } =
        $"Server={target.Pipe};Database=Cosmix_Testing;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name={target.ApplicationName};Pooling=false";
    public override string Database => "Cosmix_Testing";
    public override string DataSource => target.Pipe;
    public override string ServerVersion => "16.0";
    public override ConnectionState State => state;
    public CosmixDbContext Db = null!;
    public ReportExportJob Job = null!;
    public OutboxMessage Message = null!;
    public int Updates, Commits, Commands, Locks;
    public bool Conflict, FailCommit, LostAcknowledgement;
    public Action<DataTable>? ChangeIdentity;
    public Dictionary<string, object> JobOverrides = [], MessageOverrides = [], Persisted = [];
    public IsolationLevel LastIsolation;
    public string LastUpdate = "";
    public ProxyTransaction? Transaction;
    public override void Open() => state = ConnectionState.Open;
    public override void Close() => state = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) => throw new Exception("Database switch forbidden.");
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolation)
    { LastIsolation = isolation; return Transaction = new(this, isolation); }
    protected override DbCommand CreateDbCommand() => new ProxyCommand(this);
    public DataTable Identity()
    {
        var table = new DataTable();
        foreach (var (name, type) in new (string, Type)[] {
            ("Server",typeof(string)), ("Database",typeof(string)), ("Owner",typeof(string)), ("Operator",typeof(string)),
            ("Family",typeof(Guid)), ("ProcessId",typeof(int)), ("ReadOnly",typeof(bool)), ("Synonyms",typeof(int)),
            ("ExternalDependencies",typeof(int)), ("DataFile",typeof(string)), ("LogFile",typeof(string)),
            ("FileCount",typeof(int)), ("Transport",typeof(string)), ("Encrypted",typeof(string)) }) table.Columns.Add(name,type);
        table.Rows.Add(target.Server, "Cosmix_Testing", target.Owner, target.Owner, target.Family, target.ProcessId,
            false, 0, 0, target.DataFile, target.LogFile, 2, "Named pipe", "FALSE");
        ChangeIdentity?.Invoke(table);
        return table;
    }
    public DbDataReader Read(ProxyCommand command)
    {
        Commands++;
        if (command.CommandText.Contains("SERVERPROPERTY", StringComparison.Ordinal)) return Identity().CreateDataReader();
        if (command.CommandText.Contains("UPDATE [jobs].[OutboxMessages]", StringComparison.Ordinal))
        {
            if (Transaction is null) throw new Exception("Update outside transaction");
            Updates++; LastUpdate = command.CommandText;
            var result = new DataTable(); result.Columns.Add("RowVersion", typeof(byte[]));
            if (!Conflict)
            {
                result.Rows.Add(new byte[] { 0,0,0,0,0,0,0,2 });
                Transaction.Staged = Db.ChangeTracker.Entries<OutboxMessage>().Single().Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue ?? DBNull.Value);
            }
            return result.CreateDataReader();
        }
        object entity = command.CommandText.Contains("[reports].[ReportExportJobs]", StringComparison.Ordinal) ? Job :
            command.CommandText.Contains("[jobs].[OutboxMessages]", StringComparison.Ordinal) ? Message :
            throw new Exception("Unexpected command");
        if (!command.CommandText.Contains("WITH (UPDLOCK, ROWLOCK)", StringComparison.Ordinal) &&
            (!Regex.IsMatch(command.CommandText, @"WHERE \[[^\]]+\]\.\[Id\] = @") ||
             command.CommandText.Contains(" OR ",StringComparison.Ordinal) ||
             command.CommandText.Contains("ORDER BY",StringComparison.Ordinal)))
            throw new Exception("Unbounded or non-ID report/outbox read");
        var metadata = Db.Model.FindEntityType(entity.GetType())!;
        var tableId = StoreObjectIdentifier.Table(metadata.GetTableName()!, metadata.GetSchema());
        var columns = Regex.Matches(command.CommandText.Split("FROM", StringSplitOptions.None)[0], @"\[[^\]]+\]\.\[([^\]]+)\]")
            .Select(m => m.Groups[1].Value).ToArray();
        if (columns.Length == 0 && command.CommandText.Contains("WITH (UPDLOCK, ROWLOCK)", StringComparison.Ordinal))
            columns = metadata.GetProperties().Select(p => p.GetColumnName(tableId)!).ToArray();
        var data = new DataTable();
        var values = new List<object>();
        foreach (var column in columns)
        {
            var property = metadata.GetProperties().Single(p => p.GetColumnName(tableId) == column);
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type.IsEnum) type = typeof(int);
            data.Columns.Add(column, type);
            var overrides = entity is ReportExportJob ? JobOverrides : MessageOverrides;
            var value = overrides.TryGetValue(property.Name, out var replacement) ? replacement :
                entity is OutboxMessage && Persisted.TryGetValue(property.Name, out var persisted) ? persisted :
                property.PropertyInfo!.GetValue(entity);
            values.Add(property.Name == "RowVersion" && !overrides.ContainsKey(property.Name) ? new byte[] { 0,0,0,0,0,0,0,1 } :
                value is Enum ? Convert.ToInt32(value) : value ?? DBNull.Value);
        }
        if (command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid id && id == (entity is ReportExportJob j ? j.Id : Message.Id)))
            data.Rows.Add(values.ToArray());
        return data.CreateDataReader();
    }
}

sealed class ProxyTransaction(ProxyConnection connection, IsolationLevel isolation) : DbTransaction
{
    public Dictionary<string,object> Staged = [];
    public override IsolationLevel IsolationLevel => isolation;
    protected override DbConnection DbConnection => connection;
    public override void Commit()
    {
        if (connection.FailCommit) throw new IOException("Synthetic commit failure");
        connection.Commits++;
        foreach (var pair in Staged) connection.Persisted[pair.Key] = pair.Value;
        if (connection.LostAcknowledgement) throw new IOException("Synthetic lost commit acknowledgement");
    }
    public override void Rollback() { }
    protected override void Dispose(bool disposing) { connection.Transaction = null; base.Dispose(disposing); }
}

sealed class ProxyCommand(ProxyConnection connection) : DbCommand
{
    private readonly SqlCommand parameters = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
    protected override DbParameter CreateDbParameter() => new SqlParameter();
    public override void Cancel() { }
    public override void Prepare() { }
    public override int ExecuteNonQuery()
    {
        if (connection.Transaction is not null && CommandText.Contains("sp_getapplock", StringComparison.Ordinal))
        { connection.Locks++; return 0; }
        if (connection.Transaction is not null && (CommandText.StartsWith("SAVE TRANSACTION", StringComparison.Ordinal) ||
            CommandText.StartsWith("ROLLBACK TRANSACTION", StringComparison.Ordinal))) return 0;
        throw new Exception("Non-query IO forbidden: " + CommandText);
    }
    public override object ExecuteScalar() => throw new Exception("Scalar IO forbidden.");
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => connection.Read(this);
}
