#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

// This inspects the actual repository and EF query translation. Neither interceptor permits IO.
var connections = new DisputeNoConnections(); var commands = new DisputeNoCommands();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=c04-no-connection.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(connections, commands).Options);
var repository = new KpiDisputeRepository(db); var checks = 0;
void Check(bool condition, string explanation) { if (!condition) throw new Exception(explanation); checks++; }
async Task Guard(Func<Task> action) {
    try { await action(); throw new Exception("Expected existing transaction guard."); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains("authorized transaction"), "Actual repository transaction guard"); }
}
Task Sync(Action action) { action(); return Task.CompletedTask; }
var employee = Guid.NewGuid(); var account = Guid.NewGuid(); var id = Guid.NewGuid(); var from = new DateOnly(2025, 7, 1);
var request = new ApprovalRequest(KpiDisputeRules.YearPrefix(from) + Guid.NewGuid().ToString("N"), employee,
    account.ToString(), "synthetic-private-reference", 1, new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
var decision = request.Decide(ApprovalState.Returned, Guid.NewGuid().ToString(), "SyntheticIndependentRole", "Synthetic return",
    request.RequestedAt, "synthetic-response-reference");
var audit = new AuditEvent(KpiDisputeRules.SubmittedEvent, account.ToString(), "SyntheticRole", KpiDisputeRules.TargetType,
    id.ToString(), request.RequestedAt, "synthetic-c04-repository", employee, "synthetic-private-reference");
await Guard(() => Sync(repository.RequireTransaction));
await Guard(() => repository.FindAsync(employee, id, default));
await Guard(() => repository.YearAsync(employee, from, account, null, default));
await Guard(() => repository.PageAsync(employee, new(from), account, null, default));
await Guard(() => repository.EvidenceAsync(employee, id, KpiDisputeRules.SubmittedEvent, default));
await Guard(() => repository.DocumentsAsync(employee, [], default));
await Guard(() => Sync(() => repository.Add(request)));
await Guard(() => Sync(() => repository.Add(decision)));
await Guard(() => Sync(() => repository.Add(audit)));
await Guard(() => Sync(() => repository.ExpectVersion(request, new byte[8])));
await Guard(() => repository.SaveChangesAsync(default));
Check(db.ChangeTracker.Entries().Count() == 0 && connections.Attempts == 0 && commands.Attempts == 0,
    "No rows staged and no connections/commands attempted by guards.");

// The synthetic marker is only for constructing the actual private YearQuery expression.
// It is deliberately not a SQL transaction or proof of isolation, and it cannot commit/save.
var manager = db.GetService<IDbContextTransactionManager>();
var transactionProperty = manager.GetType().GetProperty(nameof(IDbContextTransactionManager.CurrentTransaction))
    ?? throw new Exception("Cannot inspect current EF transaction property.");
if (transactionProperty.SetMethod is null) throw new Exception("Current EF transaction property is not settable for the offline query marker.");
try {
    IQueryable<ApprovalRequest> Query(Guid subject, DateOnly year, Guid? requester, Guid? excluded) {
        transactionProperty.SetValue(manager, new DisputeQueryTransactionMarker());
        try {
            return (IQueryable<ApprovalRequest>)typeof(KpiDisputeRepository).GetMethod("YearQuery", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(repository, [subject, year, requester, excluded])!;
        }
        finally { transactionProperty.SetValue(manager, null); }
    }
    // Translate after clearing the marker; a real SQL transaction is never attached to a command.
    var own = Query(employee, from, account, null);
    var ownSql = own.ToQueryString();
    Check(ownSql.Contains("[workflow].[ApprovalRequests]") && ownSql.Contains("[SubjectId]") && ownSql.Contains(employee.ToString()),
        "Actual query filters employee before pagination.");
    Check(ownSql.Contains("[RequesterUserId]") && ownSql.Contains(account.ToString()) && ownSql.Contains("[Stage] = 1"),
        "Actual own history filters requester and controlled stage.");
    Check(ownSql.Contains(KpiDisputeRules.YearPrefix(from)) && ownSql.Contains("[RequestType] LIKE"),
        "Actual annual query pins the dispute prefix and exact retained annual start.");
    Check(own.Expression.ToString().Contains("AsNoTracking"), "Actual annual reads are not tracked.");
    var excluded = Guid.NewGuid(); var review = Query(employee, from, null, excluded).ToQueryString();
    Check(review.Contains("[RequesterUserId] <>") && review.Contains(excluded.ToString()),
        "Actual reviewer candidate query excludes the requester before count/page.");
    var bounded = own.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Take(KpiDisputeRules.YearLimit + 1).ToQueryString();
    Check(bounded.Contains("TOP(") && bounded.Contains("129") && bounded.Contains("ORDER BY"),
        "Actual candidate query supports ordered overflow-detection bound.");
    var paged = own.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip(50).Take(50).ToQueryString();
    Check(paged.Contains("OFFSET") && paged.Contains("FETCH NEXT") && paged.Contains("[RequestedAt] DESC") &&
        paged.Contains("[RequesterUserId]") && paged.Contains(employee.ToString()),
        "Actual scoped candidate query translates bounded stable pagination.");
    foreach (var scopeCase in new object?[][] {
        [Guid.Empty, from, null, null], [employee, default(DateOnly), null, null],
        [employee, from, Guid.Empty, null], [employee, from, null, Guid.Empty], [employee, from, account, account]
    }) {
        try { Query((Guid)scopeCase[0]!, (DateOnly)scopeCase[1]!, (Guid?)scopeCase[2], (Guid?)scopeCase[3]); throw new Exception("Malformed scope was accepted."); }
        catch (TargetInvocationException ex) when (ex.InnerException is AuthenticationFailure failure) {
            Check(failure.StatusCode == 400, "Actual query rejects malformed/ambiguous scope before IO.");
        }
    }
    async Task Invalid(Func<Task> action, string code) {
        transactionProperty.SetValue(manager, new DisputeQueryTransactionMarker());
        try { await action(); throw new Exception("Expected invalid repository input."); }
        catch (AuthenticationFailure failure) { Check(failure.StatusCode == 400 && failure.Code == code, code); }
        finally { transactionProperty.SetValue(manager, null); }
    }
    await Invalid(() => repository.PageAsync(employee, new(from, 1, 51), account, null, default), "invalid_kpi_dispute_page");
    await Invalid(() => repository.FindAsync(employee, Guid.Empty, default), "invalid_attendance_request");
    await Invalid(() => repository.DocumentsAsync(employee, [Guid.Empty], default), "invalid_kpi_dispute_documents");
    await Invalid(() => repository.EvidenceAsync(employee, id, "unrelated.audit", default), "invalid_attendance_request");
    Type ResultType(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>) ? type.GetGenericArguments()[0] : type;
    Check(typeof(IKpiDisputeRepository).GetMethods().All(x => !typeof(IQueryable).IsAssignableFrom(ResultType(x.ReturnType))),
        "Application repository contract exposes only materialized reads.");
    Check(db.Model.FindEntityType(typeof(ApprovalRequest))!.FindProperty(nameof(ApprovalRequest.RowVersion))!.IsConcurrencyToken,
        "Existing approval rowversion concurrency retained.");
    Check(typeof(IAppendOnlyEntity).IsAssignableFrom(typeof(ApprovalDecision)) && typeof(IAppendOnlyEntity).IsAssignableFrom(typeof(AuditEvent)),
        "Decision and audit history use existing append-only entities.");
}
finally { transactionProperty.SetValue(manager, null); }
Check(db.Database.CurrentTransaction is null && db.ChangeTracker.Entries().Count() == 0 && connections.Attempts == 0 && commands.Attempts == 0,
    "Offline query inspection opened no connection, executed no SQL and changed no rows.");
Console.WriteLine($"C04 actual repository: {checks} offline guard/model/query assertions passed; zero SQL connections/commands, no isolation or database execution claim.");

sealed class DisputeNoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("C04 repository SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("C04 repository SQL connections forbidden"); }
}
sealed class DisputeNoCommands : DbCommandInterceptor
{
    public int Attempts;
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("C04 repository SQL commands forbidden"); }
}
sealed class DisputeQueryTransactionMarker : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public bool SupportsSavepoints => false;
    private static Exception Forbidden() => new InvalidOperationException("Offline query marker cannot perform transaction IO.");
    public void Commit() => throw Forbidden();
    public void Rollback() => throw Forbidden();
    public Task CommitAsync(CancellationToken cancellationToken = default) => throw Forbidden();
    public Task RollbackAsync(CancellationToken cancellationToken = default) => throw Forbidden();
    public void CreateSavepoint(string name) => throw Forbidden();
    public void RollbackToSavepoint(string name) => throw Forbidden();
    public void ReleaseSavepoint(string name) => throw Forbidden();
    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) => throw Forbidden();
    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) => throw Forbidden();
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
