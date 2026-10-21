#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.Leaves;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Reflect the exact builders used by LeaveRepository's production methods. No copies of repository
// LINQ, data fixtures, SQL execution, network or database connections are involved.
var connections = new NoConnections(); var commands = new NoCommands();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=leave-query-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(connections, commands).Options);
var repository = new LeaveRepository(db); var checks = 0;
void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks++; }
string Sql(string name, params object?[] arguments)
{
    var method = typeof(LeaveRepository).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new Exception("Actual leave query builder unavailable: " + name);
    var query = method.Invoke(repository, arguments) as IQueryable
        ?? throw new Exception("Actual leave builder did not return IQueryable: " + name);
    return query.ToQueryString();
}
bool Has(string sql, string text) => sql.Contains(text, StringComparison.OrdinalIgnoreCase);
bool Ids(string sql, IEnumerable<Guid> ids) => ids.All(id => Has(sql, id.ToString()));
var employee = Guid.Parse("b8daa2b8-d08c-4b30-9b56-e56b049a987c");
var from = new DateOnly(2026, 10, 13); var to = from.AddDays(15);
var requestIds = new[] { Guid.Parse("29bd63c9-82f1-45b1-b2d6-1c16f8edcb24"), Guid.Parse("73015a20-a4a3-47bf-b7dd-e84a663ed625") };
var approvalIds = new[] { Guid.Parse("8e099f11-25d2-4780-a1ef-bc869b75d547"), Guid.Parse("95a3dd0b-6bb8-4921-8486-5569739dc1b8") };

var self = Sql("Query", employee);
Check(Has(self, "[EmployeeId] =") && Ids(self, [employee]), "Base self query binds authenticated employee in SQL");
Check(Has(self, "LeaveTypes") && Has(self, "ApprovalRequests") && Has(self, "LeaveRequestDays"),
    "Base source joins actual leave type, linked approval and exact allocations");
Check(Has(self, "LEFT JOIN") && Has(self, "[ApprovalRequestId]"), "Missing approval remains visible for fail-closed provenance checks");

var list = Sql("ListQuery", employee, new LeaveListRequest(from, to, 2, 7));
Check(Ids(list, [employee]) && Has(list, "[EmployeeId] ="), "Self list retains employee predicate before paging");
Check(Has(list, "[StartDate] <=") && Has(list, "[EndDate] >=") && Has(list, from.ToString("yyyy-MM-dd")) && Has(list, to.ToString("yyyy-MM-dd")),
    "Date list applies inclusive request-range overlap in SQL");
Check(Has(list, "OFFSET") && Has(list, "FETCH NEXT"), "Production list uses SQL pagination");
Check(Has(list, "[CreatedAt] DESC") && Has(list, "[Id]"), "Page ordering is deterministic across equal creation times");
Check(Has(list, "LeaveRequestDays") && Has(list, "[ShiftVersionId]"), "Paged detail keeps exact pinned shift allocation evidence");

var queue = Sql("ListQuery", null, new LeaveListRequest(from, to, 1, 25));
Check(!Ids(queue, [employee]) && !Has(queue, "[EmployeeId] ="), "Authorized All-scope Admin queue does not invent a current employee filter");
Check(Has(queue, "[StartDate] <=") && Has(queue, "[EndDate] >=") && Has(queue, "FETCH NEXT"), "Admin queue remains date-bounded and paged");
var pending = Sql("ListQuery", employee, new LeaveListRequest(from, to, 1, 25, true));
Check(Ids(pending, [employee]) && Has(pending, "[EmployeeId] ="), "Pending self list cannot escape employee scope");
Check(Has(pending, "LeaveCancellationRequests") && Has(pending, "EXISTS") && Has(pending, "[LeaveRequestId]"),
    "Pending queue includes linked cancellation requests using a correlated EXISTS");
Check(Has(pending, "[State] = 0") && Has(pending, " OR ") && Has(pending, "[StartDate] <=") && Has(pending, "[EndDate] >="),
    "Pending application/cancellation alternatives preserve date filters");
var search = Sql("SearchQuery", employee, new LeaveListRequest(from, to, 2, 7, true));
Check(Has(search, "EXISTS") && Has(search, "[State] = 0") && Ids(search, [employee]), "Actual count/search source uses the same pending and identity filters");
Check(!Has(search, "OFFSET") && !Has(search, "FETCH NEXT"), "Search/count source does not accidentally count only a page");

var overlap = Sql("OverlapQuery", employee, from, to);
Check(Ids(overlap, [employee]) && Has(overlap, "[EmployeeId] ="), "Overlap source binds employee before materializing history");
Check(Has(overlap, "[StartDate] <=") && Has(overlap, "[EndDate] >="), "Overlap includes boundary dates and multi-day requests");
Check(Has(overlap, "TOP(") && Has(overlap, "1001") && Has(overlap, "ORDER BY"), "Overlap has deterministic overflow sentinel cap");
Check(!Has(overlap, "[State] ="), "Overlap does not silently discard unsupported/corrupt approval states before provenance verification");

var cancellations = Sql("CancellationQuery", (object)requestIds);
Check(Ids(cancellations, requestIds) && Has(cancellations, "[LeaveRequestId]"), "Cancellation history is restricted to requested parent identities");
Check(Has(cancellations, "ApprovalRequests") && Has(cancellations, "LEFT JOIN"), "Cancellation source retains missing approval relationships");
Check(Has(cancellations, "TOP(") && Has(cancellations, "5001") && Has(cancellations, "ORDER BY") && Has(cancellations, "[CreatedAt]"),
    "Cancellation history is ordered and bounded with overflow detection");
var decisions = Sql("DecisionQuery", (object)approvalIds);
Check(Ids(decisions, approvalIds) && Has(decisions, "[ApprovalRequestId]"), "Decision query binds only fetched application/cancellation approval IDs");
Check(Has(decisions, "TOP(") && Has(decisions, "ORDER BY") && Has(decisions, "[DecidedAt]"), "Decision history uses stable order and a bounded duplicate-detection cap");
Check(!Has(decisions, "[Decision] ="), "Decision query does not hide conflicting rejected/returned/approved history");
var successors = Sql("SuccessorQuery", (object)requestIds);
Check(Ids(successors, requestIds) && Has(successors, "[PreviousRequestId] IS NOT NULL"), "Returned successors are tied to non-null requested predecessor IDs");
Check(Has(successors, "[PreviousRequestId]") && !Has(successors, "[Reason]") && !Has(successors, "[EvidencePrivateReference]"),
    "Successor relationship projection does not load confidential submission fields");

var yearFrom = new DateOnly(2026, 7, 1); var yearTo = new DateOnly(2027, 7, 1);
var quota = Sql("LedgerQuotaQuery", employee, yearFrom, yearTo);
Check(Ids(quota, [employee]) && Has(quota, "[EmployeeId] ="), "Quota ledger is employee-scoped in SQL");
Check(Has(quota, "[LeaveYearStart] =") && Has(quota, "[LeaveYearEnd] =") && Has(quota, "2026-07-01") && Has(quota, "2027-07-01"),
    "Quota uses the exact exclusive fiscal-year identity");
Check(Has(quota, "[EntryType]") && (Has(quota, " IN ") || Has(quota, " OR ")), "Quota query selects utilization and release entry kinds");
Check(Has(quota, "LeaveTypes") && Has(quota, "[Kind]") && Has(quota, "PolicyVersions") && Has(quota, "Policies"),
    "Actual quota projection translates leave kind, immutable policy version and parent category joins");
Check(Has(quota, "[RulesJson]") && Has(quota, "[RulesSchemaVersion]") && Has(quota, "[Authority]") && Has(quota, "[Category]"),
    "Quota carries each historical policy's explicit units and authority for validation");
Check(Has(quota, "[ReversesEntryId]") && Has(quota, "[MinutesDelta]") && Has(quota, "[LeaveRequestId]") && Has(quota, "[PolicyVersionId]"),
    "Quota retains exact reversal identity, source request and signed units");
Check(Has(quota, "TOP(") && Has(quota, "10001") && Has(quota, "ORDER BY") && Has(quota, "[RecordedAt]"),
    "Quota history is ordered and bounded with overflow detection");
Check(!Has(quota, "Compensation") && !Has(quota, "Bank"), "Leave quota projection does not access compensation or bank tables");
Check(connections.Attempts == 0 && commands.Executions == 0, "No database connection or command execution occurred");
Console.WriteLine($"PASS: {checks} actual leave EF query translation checks; zero database connections or command executions.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
sealed class NoCommands : DbCommandInterceptor
{
    public int Executions;
    private Exception Forbidden() { Executions++; return new Exception("SQL execution forbidden"); }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) => throw Forbidden();
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default) => throw Forbidden();
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) => throw Forbidden();
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default) => throw Forbidden();
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) => throw Forbidden();
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) => throw Forbidden();
}
