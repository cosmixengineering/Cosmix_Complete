using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Insurance;
using Cosmix.Infrastructure.BackgroundJobs;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Execute the real worker, outbox, repository and delivery handler. Only relational IO and
// private bytes are synthetic. This cannot establish actual SQL locking or process restart.
var failures = new List<string>(); var checks = 0;
void Check(bool value, string name) { checks++; if (!value) failures.Add(name); }
var store = new SyntheticStore(); MemoryQueries.Store = store;
var clock = new Clock(DateTimeOffset.UtcNow);
var today = DateOnly.FromDateTime(clock.Now.UtcDateTime);
var files = new MemoryFiles(); var log = new WorkerLog();
var services = new ServiceCollection();
services.AddSingleton<TimeProvider>(clock);
services.AddSingleton<IPrivateFileStore>(files);
services.AddSingleton<ILogger<DocumentExpiryReminderWorker>>(log);
services.AddScoped(_ => new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(new SyntheticConnection(store))
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(new SyntheticSave(store)).Options));
services.AddScoped<IDurableJobRepository, DurableJobRepository>();
services.AddScoped<INotificationOutbox, NotificationOutbox>();
services.AddScoped<INotificationRepository>(sp => new NotificationRepository(sp.GetRequiredService<CosmixDbContext>(), null!, null!));
using var provider = services.BuildServiceProvider();
async Task Sweep(CancellationToken ct = default)
{
    using var worker = ActivatorUtilities.CreateInstance<DocumentExpiryReminderWorker>(provider);
    await (Task)typeof(DocumentExpiryReminderWorker).GetMethod("CheckExpiriesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(worker, [ct])!;
}
var employee = Guid.NewGuid(); var account = Guid.NewGuid();
store.Add<UserAccount>(("Id", account), ("EmployeeId", employee), ("Status", AccountStatus.Active));
for (var i = 0; i < 602; i++)
{
    store.Add<EmployeeDocument>(("EmployeeId", employee), ("ExpiresOn", today.AddDays(i % 31)));
    store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", today.AddDays(i % 31)), ("Status", EmployeeCertificationStatus.Active));
    store.Add<InsurancePolicy>(("EmployeeId", employee), ("ExpiryDate", today.AddDays(i % 31)));
}
await Sweep();
foreach (var template in new[] { NotificationTemplates.DocumentExpiring, NotificationTemplates.CertificationExpiring, NotificationTemplates.InsuranceExpiring })
    Check(store.Rows<OutboxMessage>().Count(x => x.DeduplicationKey.StartsWith(template + ":", StringComparison.Ordinal)) == 602,
        "602 eligible records enter the real outbox for " + template);
Check(store.Rows<Notification>().Count == 0, "producer never delivers before outbox dispatch");
Check(store.SaveSizes.Count > 1 && store.SaveSizes.Max() <= 100, "tracking/save size remains bounded to one 100-row page");
Check(store.NonTransactionalSaves == 0, "every producer save belongs to an explicit transaction");
var firstCount = store.Rows<OutboxMessage>().Count; var firstWrites = files.Writes;
await Sweep();
Check(store.Rows<OutboxMessage>().Count == firstCount && files.Writes == firstWrites, "fresh worker replay creates no second outbox or private payload");
Check(log.Errors.Count == 0, "sweep completes without swallowed failures: " + string.Join(",", log.Errors));

void Reset()
{
    store.Clear(); log.Errors.Clear(); files.FailWriteAt = null; files.OnWrite = null;
    store.Add<UserAccount>(("Id", account), ("EmployeeId", employee), ("Status", AccountStatus.Active));
}
Guid Document(DateOnly? expires, EmployeeDocumentLifecycleStatus state = EmployeeDocumentLifecycleStatus.Current, Guid? owner = null) =>
    store.Add<EmployeeDocument>(("EmployeeId", owner ?? employee), ("ExpiresOn", expires), ("LifecycleStatus", state)).Id;
Reset();
var dueToday = Document(today); var dueLastDay = Document(today.AddDays(30));
Document(today.AddDays(-1)); Document(today.AddDays(31)); Document(null);
Document(today, EmployeeDocumentLifecycleStatus.Archived); Document(today, EmployeeDocumentLifecycleStatus.Revoked);
var disabledEmployee = Guid.NewGuid();
store.Add<UserAccount>(("EmployeeId", disabledEmployee), ("Status", AccountStatus.Disabled));
Document(today, owner: disabledEmployee); Document(today, owner: Guid.NewGuid());
foreach (var days in new[] { -1, 0, 30, 31 })
{
    store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", today.AddDays(days)), ("Status", EmployeeCertificationStatus.Active));
    store.Add<InsurancePolicy>(("EmployeeId", employee), ("ExpiryDate", today.AddDays(days)));
}
store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", null), ("Status", EmployeeCertificationStatus.Active));
store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", today), ("Status", EmployeeCertificationStatus.Revoked));
store.Add<InsurancePolicy>(("EmployeeId", null), ("VehicleId", Guid.NewGuid()), ("ExpiryDate", today));
store.Add<EmployeeCertification>(("EmployeeId", disabledEmployee), ("ExpiresOn", today), ("Status", EmployeeCertificationStatus.Active));
store.Add<InsurancePolicy>(("EmployeeId", disabledEmployee), ("ExpiryDate", today));
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 6, "inclusive today/+30; excludes -1/+31/null/ineligible/no-account/vehicle-only across all sources");
var boundaryMessages = store.Rows<OutboxMessage>().ToArray();
foreach (var message in boundaryMessages)
{
    var intent = await NotificationOutbox.ReadAsync(message, files, default);
    Check(intent.RecipientAccountId == account && intent.ResourceId != null, "outbox payload binds exact source and account");
}
Check(boundaryMessages.Any(x => x.CorrelationId == $"expiry:doc:{dueToday:N}:{today:yyyyMMdd}") &&
    boundaryMessages.Any(x => x.CorrelationId == $"expiry:doc:{dueLastDay:N}:{today.AddDays(30):yyyyMMdd}"), "legacy identity is retained at both inclusive boundaries");

Reset();
EmployeeCertification Certification(DateOnly effective, EmployeeCertificationStatus state = EmployeeCertificationStatus.Active,
    Guid? predecessor = null, DateOnly? expires = null) =>
    store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", expires ?? today.AddDays(10)),
        ("EffectiveOn", effective), ("Status", state), ("SupersedesEmployeeCertificationId", predecessor));
var oldRenewed = Certification(today.AddDays(-20));
var currentRenewal = Certification(today, predecessor: oldRenewed.Id);
var oldRevoked = Certification(today.AddDays(-20));
Certification(today, EmployeeCertificationStatus.Revoked, oldRevoked.Id);
var oldSuspended = Certification(today.AddDays(-20));
Certification(today, EmployeeCertificationStatus.Suspended, oldSuspended.Id);
var beforeFutureRenewal = Certification(today.AddDays(-20));
var futureRenewal = Certification(today.AddDays(1), predecessor: beforeFutureRenewal.Id);
var futureIssue = Certification(today.AddDays(1));
var renewedBeyondWindow = Certification(today.AddDays(-20));
Certification(today, predecessor: renewedBeyondWindow.Id, expires: today.AddDays(90));
await Sweep();
var certificationSources = new List<Guid>();
foreach (var message in store.Rows<OutboxMessage>())
    certificationSources.Add((await NotificationOutbox.ReadAsync(message, files, default)).ResourceId!.Value);
Check(certificationSources.Order().SequenceEqual(new[] { currentRenewal.Id, beforeFutureRenewal.Id }.Order()),
    "only effective active certification tips enter the outbox; revoked/suspended/superseded/future revisions excluded");
var certificationWrites = files.Writes;
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 2 && files.Writes == certificationWrites,
    "replaying certification revision selection does not duplicate protected payloads");
clock.Now = clock.Now.AddDays(1);
await Sweep();
certificationSources.Clear();
foreach (var message in store.Rows<OutboxMessage>())
    certificationSources.Add((await NotificationOutbox.ReadAsync(message, files, default)).ResourceId!.Value);
Check(certificationSources.Order().SequenceEqual(new[] { currentRenewal.Id, beforeFutureRenewal.Id, futureRenewal.Id, futureIssue.Id }.Order()),
    "next sweep stages newly effective revisions once while retaining prior outbox history");
clock.Now = clock.Now.AddDays(-1);

Reset();
var legacyId = Document(today);
var legacy = new Notification(account.ToString(), NotificationChannel.InApp, NotificationTemplates.DocumentExpiring, clock.Now,
    $"expiry:doc:{legacyId:N}:{today:yyyyMMdd}");
legacy.MarkDelivered(clock.Now); store.Rows<Notification>().Add(legacy);
var another = Document(today);
var foreign = new Notification(Guid.NewGuid().ToString(), NotificationChannel.InApp, NotificationTemplates.DocumentExpiring, clock.Now,
    $"expiry:doc:{another:N}:{today:yyyyMMdd}");
foreign.MarkDelivered(clock.Now); store.Rows<Notification>().Add(foreign);
var smsId = Document(today);
var sms = new Notification(account.ToString(), NotificationChannel.Sms, NotificationTemplates.DocumentExpiring, clock.Now,
    $"expiry:doc:{smsId:N}:{today:yyyyMMdd}");
sms.MarkDelivered(clock.Now); store.Rows<Notification>().Add(sms);
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 2, "legacy same-recipient in-app delivery deduplicates; other recipient/channel cannot suppress");

Reset();
var renewedId = Document(today.AddDays(5));
await Sweep();
var originalKey = store.Rows<OutboxMessage>().Single().DeduplicationKey;
var originalCulture = CultureInfo.CurrentCulture;
try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); await Sweep(); }
finally { CultureInfo.CurrentCulture = originalCulture; }
Check(store.Rows<OutboxMessage>().Count == 1 && store.Rows<OutboxMessage>()[0].DeduplicationKey == originalKey,
    "producer identity survives process culture changes");
typeof(EmployeeDocument).GetProperty("ExpiresOn")!.SetValue(store.Rows<EmployeeDocument>().Single(), today.AddDays(10));
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 2 && store.Rows<OutboxMessage>().Select(x => x.DeduplicationKey).Distinct().Count() == 2,
    "new expiry date on the same document creates exactly one new event");

foreach (var count in new[] { 0, 99, 100, 101, 500, 501 })
{
    Reset(); for (var i = 0; i < count; i++) Document(today);
    await Sweep();
    Check(store.Rows<OutboxMessage>().Count == count && log.Errors.Count == 0, "page boundary " + count + " has no omissions");
}
Reset(); for (var i = 0; i < 201; i++) Document(today);
store.FailCommitAt = 2;
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 100 && log.Errors.Count == 1, "second-page commit failure preserves only first committed page");
store.FailCommitAt = null; log.Errors.Clear();
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 201 && log.Errors.Count == 0, "fresh worker after commit failure finishes every page without duplicates");

Reset(); for (var i = 0; i < 201; i++) Document(today);
store.FailAfterCommitAt = 2;
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 200 && log.Errors.Count == 1,
    "lost commit acknowledgement retains both committed pages");
store.FailAfterCommitAt = null; log.Errors.Clear();
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 201 && log.Errors.Count == 0 &&
    store.Rows<OutboxMessage>().Select(x => x.DeduplicationKey).Distinct().Count() == 201,
    "retry after ambiguous commit deduplicates persisted page and finishes remaining records");

Reset(); for (var i = 0; i < 101; i++) Document(today);
files.FailWriteAt = files.Writes + 51;
store.Add<EmployeeCertification>(("EmployeeId", employee), ("ExpiresOn", today), ("Status", EmployeeCertificationStatus.Active));
await Sweep();
Check(store.Rows<OutboxMessage>().Count == 1 && store.Rows<OutboxMessage>()[0].CorrelationId.StartsWith("expiry:cert:", StringComparison.Ordinal),
    "private write failure rolls back the whole document page and other source kind progresses");
files.FailWriteAt = null; await Sweep();
Check(store.Rows<OutboxMessage>().Count == 102, "storage recovery stages all failed records once");

Reset(); for (var i = 0; i < 201; i++) Document(today);
using (var cancel = new CancellationTokenSource())
{
    var stopAt = files.Writes + 151;
    files.OnWrite = () => { if (files.Writes == stopAt) cancel.Cancel(); };
    await Sweep(cancel.Token);
}
Check(store.Rows<OutboxMessage>().Count == 100, "cancellation rolls back active page but retains committed progress");
files.OnWrite = null; await Sweep();
Check(store.Rows<OutboxMessage>().Count == 201, "restart after cancellation recovers exactly once");

Reset(); for (var i = 0; i < 101; i++) Document(today);
await Task.WhenAll(Sweep(), Sweep());
Check(store.Rows<OutboxMessage>().Count == 101 && log.Errors.Count == 0, "two actual worker instances deduplicate through the transaction-lock contract");
Check(store.Rows<Notification>().Count == 0, "competing producers still do not deliver inline");

async Task<DurableJobClaim?> Claim()
{
    using var scope = provider.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IDurableJobRepository>()
        .ClaimNextAsync([NotificationOutbox.MessageType], clock.Now, TimeSpan.FromSeconds(120), default);
}
async Task<bool> Complete(DurableJobClaim claim)
{
    using var scope = provider.CreateScope();
    var jobs = scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();
    var handler = new InAppNotificationHandler(jobs, scope.ServiceProvider.GetRequiredService<INotificationRepository>(), files, clock);
    return await jobs.CompleteAsync(claim, handler.ApplyAsync, default);
}
var claim = (await Claim())!;
store.FailCommitAt = store.Commits + 1;
try { await Complete(claim); Check(false, "completion commit must fail"); }
catch (IOException) { Check(true, "completion commit failure injected"); }
Check(store.Rows<Notification>().Count == 0 && store.Rows<OutboxMessage>().Single(x => x.Id == claim.MessageId).State == DispatchState.Processing,
    "real completion repository stages delivery and completion atomically under rollback");
store.FailCommitAt = null;
clock.Now = claim.LeaseExpiresAt;
Check(!await Complete(claim), "expired old worker cannot deliver");
var reclaimed = (await Claim())!;
Check(reclaimed.MessageId == claim.MessageId && reclaimed.LeaseToken != claim.LeaseToken, "fresh repository reclaims expired lease");
var results = await Task.WhenAll(Complete(reclaimed), Complete(reclaimed));
Check(results.Count(x => x) == 1 && store.Rows<Notification>().Count == 1, "competing completions create exactly one delivered notification");
Check(!await Complete(claim) && !await Complete(reclaimed), "old and completed lease replays cannot redeliver");
Check(store.Rows<OutboxMessage>().Single(x => x.Id == claim.MessageId).State == DispatchState.Completed,
    "completed outbox state persists across fresh repository scopes");
var afterDeliveryCount = store.Rows<OutboxMessage>().Count;
await Sweep();
Check(store.Rows<OutboxMessage>().Count == afterDeliveryCount && store.Rows<Notification>().Count == 1,
    "producer restart after one delivery retains pending and completed identities");

// Compile the actual candidate/claim queries with the SQL Server provider without connecting.
var guard = new NoConnections();
using (var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true").AddInterceptors(guard).Options))
{
    var candidates = typeof(DocumentExpiryReminderWorker).GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Static);
    if (candidates == null) Check(false, "worker candidate query is available for provider verification");
    else foreach (var kind in Enum.GetValues(candidates.GetParameters()[1].ParameterType))
        foreach (Guid? after in new Guid?[] { null, Guid.NewGuid() })
        {
            var query = (IQueryable)candidates.Invoke(null, [sqlDb, kind, today, after])!;
            var sql = query.ToQueryString();
            Check(sql.Contains("TOP(", StringComparison.Ordinal) && sql.Contains("ORDER BY", StringComparison.Ordinal) &&
                sql.Contains("INNER JOIN", StringComparison.Ordinal) && sql.Contains("<=", StringComparison.Ordinal),
                kind + " actual bounded cursor query translates to SQL Server");
            if (after.HasValue) Check(sql.Contains(after.Value.ToString(), StringComparison.OrdinalIgnoreCase) && sql.Contains(" > ", StringComparison.Ordinal),
                kind + " SQL cursor is an ordered source-ID keyset");
        }
    var model = sqlDb.Model.FindEntityType(typeof(OutboxMessage))!;
    Check(model.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MessageType", "DeduplicationKey" })),
        "existing outbox schema enforces producer identity uniqueness");
    Check(sqlDb.Model.FindEntityType(typeof(UserAccount))!.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId" })),
        "source-ID pagination relies on the existing unique employee account mapping");
}
Check(guard.Attempts == 0, "offline provider translation never opens SQL");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"Document expiry reliability: {checks - failures.Count}/{checks} checks passed. Synthetic relational IO only; no SQL/network/host/provider writes.");
return failures.Count == 0 ? 0 : 1;

sealed class Clock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class WorkerLog : ILogger<DocumentExpiryReminderWorker>
{
    public List<string> Errors = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
    { if (level >= LogLevel.Warning) lock (Errors) Errors.Add(format(state, error)); }
}
sealed class MemoryFiles : IPrivateFileStore
{
    private readonly Dictionary<string, (byte[] Data, string Purpose)> data = [];
    public int Writes;
    public int? FailWriteAt;
    public Action? OnWrite;
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = Guid.NewGuid().ToString("N");
        lock (data)
        {
            if (Writes + 1 == FailWriteAt) { FailWriteAt = null; throw new IOException("Synthetic storage failure"); }
            data.Add(key, (bytes.ToArray(), purpose)); Writes++;
        }
        OnWrite?.Invoke();
        return Task.FromResult(new PrivateFileReference(key, "v1", new string('A', 64), "memory-only", bytes.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (data)
        {
            var item = data[reference.StorageKey];
            if (item.Purpose != purpose) throw new IOException("Wrong private purpose");
            return Task.FromResult(item.Data.ToArray());
        }
    }
}
sealed class SyntheticStore
{
    private readonly Dictionary<Type, IList> rows = [];
    public SemaphoreSlim Gate = new(1, 1);
    public List<int> SaveSizes = [];
    public int NonTransactionalSaves;
    public int Commits;
    public int? FailCommitAt;
    public int? FailAfterCommitAt;
    public void Clear() { rows.Clear(); SaveSizes.Clear(); NonTransactionalSaves = 0; Commits = 0; FailCommitAt = null; FailAfterCommitAt = null; }
    public List<T> Rows<T>() where T : class
    {
        lock (rows)
        {
            if (!rows.TryGetValue(typeof(T), out var list)) rows[typeof(T)] = list = new List<T>();
            return (List<T>)list;
        }
    }
    public T Add<T>(params (string Name, object? Value)[] values) where T : class
    {
        var item = (T)Activator.CreateInstance(typeof(T), true)!;
        typeof(T).GetProperty("Id")!.SetValue(item, Guid.NewGuid());
        foreach (var value in values) typeof(T).GetProperty(value.Name)!.SetValue(item, value.Value);
        Rows<T>().Add(item); return item;
    }
    public IQueryable Query(Type type)
    {
        lock (rows)
        {
            if (!rows.TryGetValue(type, out var list)) rows[type] = list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
            var copy = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
            foreach (var item in list) copy.Add(Clone(item!));
            return (IQueryable)typeof(Queryable).GetMethods().Single(m => m.Name == "AsQueryable" && m.IsGenericMethod)
                .MakeGenericMethod(type).Invoke(null, [copy])!;
        }
    }
    public static object Clone(object value)
    {
        var clone = Activator.CreateInstance(value.GetType(), true)!;
        foreach (var property in value.GetType().GetProperties().Where(p => p.SetMethod != null))
            property.SetValue(clone, property.GetValue(value));
        return clone;
    }
    public void Apply(List<object> changes)
    {
        lock (rows)
            foreach (var item in changes)
            {
                var type = item.GetType();
                if (!rows.TryGetValue(type, out var list)) rows[type] = list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
                var id = type.GetProperty("Id")!.GetValue(item);
                var existing = list.Cast<object>().FirstOrDefault(x => Equals(type.GetProperty("Id")!.GetValue(x), id));
                if (existing != null) list.Remove(existing);
                if (item is OutboxMessage job && list.Cast<OutboxMessage>().Any(x => x.MessageType == job.MessageType && x.DeduplicationKey == job.DeduplicationKey))
                    throw new InvalidOperationException("Duplicate outbox identity");
                list.Add(Clone(item));
            }
    }
}
public sealed class MemoryQueries(ICurrentDbContext current) : IAsyncQueryProvider
{
    internal static SyntheticStore Store = null!;
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = Track(Execute(expression));
        return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(T).GetGenericArguments()[0]).Invoke(null, [result])!;
    }
    internal object? Track(object? result)
    {
        if (result is OutboxMessage item && !current.Context.Set<OutboxMessage>().Local.Any(x => x.Id == item.Id)) current.Context.Attach(item);
        return result;
    }
    private sealed class ReplaceRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            if (node is FromSqlQueryRootExpression sql)
            {
                var args = (object[])((ConstantExpression)sql.Argument).Value!;
                if (sql.EntityType.ClrType != typeof(OutboxMessage) || args.Length != 1 || args[0] is not Guid id ||
                    !sql.Sql.Contains("WITH (UPDLOCK, ROWLOCK)", StringComparison.Ordinal)) throw new InvalidOperationException("Unsupported synthetic SQL root");
                return Store.Query(typeof(OutboxMessage)).Cast<OutboxMessage>().Where(x => x.Id == id).Expression;
            }
            return node is EntityQueryRootExpression root ? Store.Query(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name == "AsNoTracking") return Visit(node.Arguments[0]);
            if (node.Method.DeclaringType == typeof(RelationalDbFunctionsExtensions) && node.Method.Name == "Collate") return Visit(node.Arguments[1]);
            return base.VisitMethodCall(node);
        }
    }
}
public sealed class MemoryQuery<T>(MemoryQueries provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return (T)provider.Track(item)!; }
    }
}
sealed class SyntheticSave(SyntheticStore store) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = e.Context!;
        var changes = db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => SyntheticStore.Clone(x.Entity)).ToList();
        lock (store.SaveSizes) store.SaveSizes.Add(changes.Count);
        if (db.Database.CurrentTransaction?.GetDbTransaction() is SyntheticTransaction transaction) transaction.Changes.AddRange(changes);
        else { store.NonTransactionalSaves++; store.Apply(changes); }
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(changes.Count));
    }
}
sealed class SyntheticConnection(SyntheticStore store) : DbConnection
{
    private ConnectionState state;
    [AllowNull] public override string ConnectionString { get; set; } = "Server=synthetic.invalid;Database=NeverConnect;Integrated Security=true";
    public override string Database => "SyntheticOnly";
    public override string DataSource => "NoNetwork";
    public override string ServerVersion => "16.0";
    public override ConnectionState State => state;
    public SyntheticTransaction? Transaction;
    public override void Open() => state = ConnectionState.Open;
    public override void Close() => state = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => Transaction = new(this, store, isolationLevel);
    protected override DbCommand CreateDbCommand() => new SyntheticCommand(this);
}
sealed class SyntheticTransaction(SyntheticConnection connection, SyntheticStore store, IsolationLevel isolation) : DbTransaction
{
    public List<object> Changes = [];
    private bool locked;
    public override IsolationLevel IsolationLevel => isolation;
    protected override DbConnection DbConnection => connection;
    public async Task Lock(CancellationToken ct) { if (!locked) { await store.Gate.WaitAsync(ct); locked = true; } }
    public override void Commit()
    {
        store.Commits++;
        if (store.Commits == store.FailCommitAt) throw new IOException("Synthetic commit failure");
        store.Apply(Changes);
        if (store.Commits == store.FailAfterCommitAt) throw new IOException("Synthetic lost commit acknowledgement");
    }
    public override void Rollback() => Changes.Clear();
    protected override void Dispose(bool disposing)
    {
        if (locked) { locked = false; store.Gate.Release(); }
        connection.Transaction = null;
        base.Dispose(disposing);
    }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new InvalidOperationException("Real SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("Real SQL forbidden"); }
}
sealed class SyntheticCommand(SyntheticConnection connection) : DbCommand
{
    private readonly SqlCommand parameterOwner = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection => parameterOwner.Parameters;
    protected override DbParameter CreateDbParameter() => new SqlParameter();
    public override void Cancel() { }
    public override void Prepare() { }
    public override object ExecuteScalar() => throw new InvalidOperationException("Unexpected scalar IO");
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new InvalidOperationException("Unexpected relational read IO");
    public override int ExecuteNonQuery() => throw new InvalidOperationException("Unexpected synchronous SQL IO");
    public override async Task<int> ExecuteNonQueryAsync(CancellationToken ct)
    {
        if (!CommandText.Contains("sp_getapplock", StringComparison.Ordinal) || connection.Transaction == null)
            throw new InvalidOperationException("Only a synthetic transaction lock is supported");
        await connection.Transaction.Lock(ct); return 0;
    }
}
