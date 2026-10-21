#:project ../../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyNotifications.packages.lock.json
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;

// In-memory repository doubles exercise real services/domain objects. No SQL, host,
// private file write, account fixture, provider or authentication bypass is installed.
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
async Task Failure(Func<Task> action, string code, int status)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure error) { Check(error.Code == code && error.StatusCode == status, code); }
}
var clock = new MutableClock(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
var actor = new Current(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true));
var inbox = new Inbox();
var jobs = new Jobs(clock, inbox);
var files = new MemoryFiles();
var tx = DispatchProxy.Create<IAccessManagementRepository, Transactions>();
var service = new NotificationService(actor, tx, inbox, clock);
var outbox = new NotificationOutbox(jobs, files, clock);
var context = new AuthRequestContext("127.0.0.1", "synthetic-notifications");
var recipient = actor.Actor.AccountId;
jobs.Accounts.Add(recipient);
var intent = new NotificationIntent(Guid.NewGuid(), recipient, NotificationTemplates.ShiftChanged, Guid.NewGuid());

jobs.InProducerTransaction = false;
try { await outbox.StageAsync(intent, context, default); throw new Exception("Expected producer transaction guard"); }
catch (InvalidOperationException) { Check(files.Writes == 0 && jobs.Messages.Count == 0, "outside transaction stages nothing"); }
jobs.InProducerTransaction = true;
await Failure(() => outbox.StageAsync(intent with { TemplateCode = "Salary 999 CNIC secret" }, context, default), "invalid_notification_intent", 400);
await Failure(() => outbox.StageAsync(intent with { EventId = Guid.Empty }, context, default), "invalid_notification_intent", 400);
await Failure(() => outbox.StageAsync(intent with { RecipientAccountId = Guid.NewGuid() }, context, default), "notification_recipient_not_found", 409);
Check(files.Writes == 0, "invalid intent does not allocate private storage");
Check(await outbox.StageAsync(intent, context, default), "new event stages");
Check(jobs.Messages.Count == 1 && jobs.Saves == 0 && inbox.Items.Count == 0, "staging has no save or precommit delivery");
Check(!await outbox.StageAsync(intent, context, default) && files.Writes == 1, "producer retry deduplicates before writing another object");
await Failure(() => outbox.StageAsync(intent with { ResourceId = Guid.NewGuid() }, context, default), "notification_event_identity_conflict", 409);
Check(!JsonSerializer.Serialize(jobs.Messages[0]).Contains(intent.ResourceId!.Value.ToString()), "business resource stays in the protected payload");
var handler = new InAppNotificationHandler(jobs, inbox, files, clock);
var options = new AttendanceJobOptions();
var processor = new DurableJobProcessor(jobs, [handler], options, clock);
Check(!await processor.DispatchOneAsync(default) && jobs.Messages[0].Attempts == 0, "worker disabled by default");
options.Enabled = true;
jobs.InProducerTransaction = false;
Check(await processor.DispatchOneAsync(default), "outbox dispatch completes");
Check(jobs.Messages[0].State == DispatchState.Completed && inbox.Items.Count == 1, "delivery and completion staged together");
Check(inbox.Items[0].DeliveredAt.HasValue && !inbox.Items[0].ReadAt.HasValue, "delivery is distinct from reading");
Check(!await processor.DispatchOneAsync(default) && inbox.Items.Count == 1, "completed event never redelivers");
Check(jobs.Audits.Count(x => x.EventType == "outbox.completed") == 1, "successful completion audited once");
Check(inbox.Items[0].ProtectedPayloadReference is null, "inbox has no confidential payload reference");

var first = inbox.Items[0];
var other = new Notification(Guid.NewGuid().ToString(), NotificationChannel.InApp, NotificationTemplates.Decision, clock.Now, "other");
other.MarkDelivered(clock.Now); inbox.Items.Add(other);
var external = new Notification(recipient.ToString(), NotificationChannel.Sms, NotificationTemplates.Decision, clock.Now, "external");
external.MarkDelivered(clock.Now); inbox.Items.Add(external);
inbox.Items.Add(new Notification(recipient.ToString(), NotificationChannel.InApp, NotificationTemplates.Decision, clock.Now, "pending"));
var list = await service.ListAsync(new(), context, default);
Check(list.TotalCount == 1 && list.Items.Single().Id == first.Id, "scope precedes pagination and excludes external/pending delivery");
Check(list.Items[0].Title == "Site schedule updated" && !JsonSerializer.Serialize(list).Contains("ProtectedPayload"), "safe static DTO only");
await Failure(() => service.GetAsync(other.Id, context, default), "notification_not_found", 404);
await Failure(() => service.MarkReadAsync(other.Id, new(Convert.ToBase64String(new byte[8])), context, default), "notification_not_found", 404);
await Failure(() => service.ListAsync(new(PageSize: 101), context, default), "invalid_notification_request", 400);
await Failure(() => service.ListAsync(new(Page: 0), context, default), "invalid_notification_request", 400);
await Failure(() => service.MarkReadAsync(first.Id, new("invalidxxxxx"), context, default), "invalid_notification_request", 400);
var oldVersion = Convert.ToBase64String(first.RowVersion);
await Failure(() => service.MarkReadAsync(first.Id, new(Convert.ToBase64String([0,0,0,0,0,0,0,99])), context, default), "notification_version_conflict", 409);
Check(first.ReadAt is null, "stale mark-read leaves unread state unchanged");
var read = await service.MarkReadAsync(first.Id, new(oldVersion), context, default);
Check(read.Changed && read.Notification.ReadAt == clock.Now, "first mark-read is persisted");
var readAudits = inbox.Audits.Count(x => x.EventType == "notification.mark-read");
clock.Now = clock.Now.AddMinutes(1);
var replay = await service.MarkReadAsync(first.Id, new(oldVersion), context, default);
Check(!replay.Changed && replay.Notification.ReadAt == read.Notification.ReadAt, "old-version retry preserves original timestamp");
Check(readAudits == inbox.Audits.Count(x => x.EventType == "notification.mark-read"), "retry does not duplicate mark-read audit");
Check((await service.ListAsync(new(UnreadOnly: true), context, default)).TotalCount == 0, "unread filter updates");
inbox.CurrentSession = false;
await Failure(() => service.ListAsync(new(), context, default), "invalid_authentication", 401);
await Failure(() => service.MarkReadAsync(first.Id, new(oldVersion), context, default), "invalid_authentication", 401);
inbox.CurrentSession = true;
actor.Actor = actor.Actor with { MfaSatisfied = false };
await Failure(() => service.GetAsync(first.Id, context, default), "access_denied", 403);
actor.Actor = actor.Actor with { Client = SessionClient.Mobile };
Check((await service.GetAsync(first.Id, context, default)).Id == first.Id, "current mobile recipient can read without web MFA flag");
actor.Actor = actor.Actor with { Client = SessionClient.Web, MfaSatisfied = true };
var unsafeTemplate = new Notification(recipient.ToString(), NotificationChannel.InApp, "salary private text", clock.Now, "unsafe");
unsafeTemplate.MarkDelivered(clock.Now); inbox.Items.Add(unsafeTemplate);
var safeLegacy = await service.GetAsync(unsafeTemplate.Id, context, default);
Check(safeLegacy.TemplateCode == "notification.notice" && !JsonSerializer.Serialize(safeLegacy).Contains("salary"), "unrecognized legacy template never renders as content");

// Shared queue ownership: unrelated pending and expired jobs belong to their own workers.
var unrelatedPending = new OutboxMessage("another-module.pending.v1", "opaque", clock.Now.AddMinutes(-5), "unrelated", "pending");
var unrelatedExpired = new OutboxMessage("another-module.expired.v1", "opaque", clock.Now.AddMinutes(-5), "unrelated", "expired");
var unrelatedLease = unrelatedExpired.Claim(clock.Now.AddMinutes(-2), clock.Now.AddMinutes(-1));
var caseVariant = new OutboxMessage(NotificationOutbox.MessageType.ToUpperInvariant(), "opaque", clock.Now, "unrelated", "case-variant");
var paddedVariant = new OutboxMessage("unrelated.padded.v1", "opaque", clock.Now, "unrelated", "padded-variant");
typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.MessageType))!.SetValue(paddedVariant,NotificationOutbox.MessageType+" ");
jobs.Messages.AddRange([unrelatedPending, unrelatedExpired, caseVariant, paddedVariant]);
var unrelated = new[] { unrelatedPending, unrelatedExpired, caseVariant, paddedVariant };
var untouched = JsonSerializer.Serialize(unrelated);
Check(!await processor.DispatchOneAsync(default), "unregistered pending/expired/case/padded variants are not claimed");
Check(JsonSerializer.Serialize(unrelated) == untouched && unrelatedExpired.LeaseToken == unrelatedLease,
    "unrelated queue rows keep state, attempts, lease, version, available time and evidence untouched");
foreach (IReadOnlyCollection<string> invalid in new IReadOnlyCollection<string>[] { [], [" "], ["Mixed.Type"], ["bad/type"], [new string('a',101)],
    [NotificationOutbox.MessageType,NotificationOutbox.MessageType], Enumerable.Range(0,33).Select(i=>"type."+i).ToArray() })
{
    try { DurableJobMessageTypes.RequireAllowed(invalid); throw new Exception("Expected invalid ownership bound"); }
    catch (InvalidOperationException) { Check(true,"invalid ownership allow-list rejected before claim"); }
}
var ownTypes = DurableJobMessageTypes.RequireAllowed([NotificationOutbox.MessageType,"attendance.report-csv.v1"]);
Check(ownTypes.Length==2 && ownTypes.Contains(NotificationOutbox.MessageType),"both registered attendance handler types have explicit ownership");

// Defense in depth: if a repository/row changes type after claim, it still cannot become success.
jobs.InProducerTransaction=true;
await outbox.StageAsync(intent with {EventId=Guid.NewGuid()},context,default);
jobs.InProducerTransaction=false;
jobs.CorruptNextClaimType="unregistered.changed.v1";
var beforeMismatch=inbox.Items.Count;
Check(await processor.DispatchOneAsync(default),"claimed message mismatch handled without delivery");
Check(jobs.Messages.Last().State==DispatchState.Failed && jobs.Messages.Last().LastFailureCode=="unsupported_message_type" && inbox.Items.Count==beforeMismatch,
    "post-claim handler mismatch remains explicit terminal failure");

// A failed completion must roll back staged notification and audit, then retry safely.
jobs.InProducerTransaction = true;
await outbox.StageAsync(intent with { EventId = Guid.NewGuid() }, context, default);
jobs.InProducerTransaction = false;
jobs.FailCommitOnce = true;
var countBefore = inbox.Items.Count;
Check(await processor.DispatchOneAsync(default), "simulated commit failure handled");
var retried = jobs.Messages.Last();
Check(inbox.Items.Count == countBefore && retried.State == DispatchState.Pending && retried.Attempts == 1, "failed commit leaves no notification and schedules retry");
Check(retried.LastFailureCode == "dispatch_unavailable" && retried.AvailableAt > clock.Now, "retry stores bounded safe failure code and delay");
Check(!await processor.DispatchOneAsync(default), "retry does not run before available time");
clock.Now = retried.AvailableAt;
Check(await processor.DispatchOneAsync(default) && inbox.Items.Count == countBefore + 1 && retried.State == DispatchState.Completed,
    "retry creates exactly one notification");

// Corrupted/wrong-purpose private data never becomes a valid delivery.
jobs.InProducerTransaction = true;
await outbox.StageAsync(intent with { EventId = Guid.NewGuid() }, context, default);
jobs.InProducerTransaction = false;
files.FailReads = true;
countBefore = inbox.Items.Count;
for (var i = 0; i < options.MaximumAttempts; i++)
{
    Check(await processor.DispatchOneAsync(default), "storage failure attempt " + (i + 1));
    if (jobs.Messages.Last().State == DispatchState.Pending) clock.Now = jobs.Messages.Last().AvailableAt;
}
Check(jobs.Messages.Last().State == DispatchState.Failed && inbox.Items.Count == countBefore, "private storage failures reach terminal bound without delivering");
files.FailReads = false;

// Expired ownership cannot complete; a new owner receives a different lease.
var leaseMessage = new OutboxMessage("lease-test", "opaque", clock.Now, "leases", "lease-test");
var lease1 = leaseMessage.Claim(clock.Now, clock.Now.AddSeconds(30));
try { leaseMessage.Claim(clock.Now, clock.Now.AddSeconds(30)); throw new Exception("Expected competing lease rejection"); }
catch (InvalidOperationException) { Check(true, "active lease excludes a second claimant"); }
clock.Now = clock.Now.AddSeconds(30);
var lease2 = leaseMessage.Claim(clock.Now, clock.Now.AddSeconds(30));
Check(lease1 != lease2 && leaseMessage.Attempts == 2, "expired lease reclaims with new identity");
try { leaseMessage.Complete(lease1, clock.Now); throw new Exception("Expected stale lease rejection"); }
catch (InvalidOperationException) { Check(true, "stale claimant cannot complete"); }
leaseMessage.Complete(lease2, clock.Now);
Check(leaseMessage.State == DispatchState.Completed, "current claimant completes");

// A new processor instance uses the persisted fake queue; the real SQL restart test remains outstanding.
jobs.InProducerTransaction = true;
await outbox.StageAsync(intent with { EventId = Guid.NewGuid() }, context, default);
jobs.InProducerTransaction = false;
var oldClaim = await jobs.ClaimNextAsync([NotificationOutbox.MessageType], clock.Now, TimeSpan.FromSeconds(options.LeaseSeconds), default);
clock.Now = oldClaim!.LeaseExpiresAt;
var restarted = new DurableJobProcessor(jobs, [handler], options, clock);
countBefore = inbox.Items.Count;
Check(await restarted.DispatchOneAsync(default) && inbox.Items.Count == countBefore + 1, "fresh processor reclaims interrupted lease");
Check(!await jobs.CompleteAsync(oldClaim, (_, _) => Task.CompletedTask, default), "original owner cannot duplicate a reclaimed completion");
Check(JsonSerializer.Serialize(unrelated)==untouched,"processing supported notifications and retries never changes another worker's queue rows");

// Model inspection only: no SQL connection is opened.
var membershipProxy = DispatchProxy.Create<IShiftRepository, MembershipProxy>();
var membershipState = (MembershipProxy)(object)membershipProxy;
var employeeA = Guid.NewGuid(); var employeeB = Guid.NewGuid(); var employeeLater = Guid.NewGuid();
var siteA = Guid.NewGuid(); var siteB = Guid.NewGuid(); var from = new DateOnly(2026,10,10);
EmployeeAssignment Membership(Guid employeeId, Guid siteId, DateOnly start) => new(employeeId, Guid.NewGuid(), Guid.NewGuid(),
    Guid.NewGuid(), Guid.NewGuid(), null, siteId, null, start, null, "synthetic", clock.Now);
membershipState.Rows = [Membership(employeeA, siteA, from), Membership(employeeB, siteB, from), Membership(employeeLater, siteA, from.AddDays(10))];
var recipients = new RecipientRepository { Rows = [new(employeeA, recipient), new(employeeB, Guid.NewGuid()), new(employeeLater, Guid.NewGuid())] };
var scheduledNotices = new SchedulingNotifications(membershipProxy, recipients, outbox);
jobs.InProducerTransaction = true;
var schedulingEvent = Guid.NewGuid(); var oldMessages = jobs.Messages.Count;
await scheduledNotices.StageIntervalAsync(schedulingEvent, siteA, from, from.AddDays(5), context, default);
Check(jobs.Messages.Count == oldMessages + 1, "site notice selects only overlapping historical membership at target site");
await scheduledNotices.StageEmployeesAsync(schedulingEvent, siteB, [employeeA], context, default);
Check(jobs.Messages.Count == oldMessages + 1, "shared version across transferred sites deduplicates for same employee");
await scheduledNotices.StageEmployeesAsync(Guid.NewGuid(), siteA, [Guid.NewGuid()], context, default);
Check(jobs.Messages.Count == oldMessages + 1, "employee without account gets no fabricated recipient");
recipients.Poison = true;
await Failure(() => scheduledNotices.StageEmployeesAsync(Guid.NewGuid(),siteA,[employeeA],context,default), "scheduling_notification_recipient_conflict",409);
await Failure(() => scheduledNotices.StageEmployeesAsync(Guid.Empty,siteA,[employeeA],context,default), "scheduling_notification_manifest_invalid",409);
jobs.InProducerTransaction = false;

using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnlyNeverOpened;Integrated Security=true;TrustServerCertificate=true").Options);
var claimSql=DurableJobRepository.ClaimCandidates(db.OutboxMessages,ownTypes,clock.Now).Take(1).ToQueryString();
Check(claimSql.Contains("COLLATE Latin1_General_100_BIN2") && claimSql.Contains("notification.in-app.v1") && claimSql.Contains("attendance.report-csv.v1"),
    "actual EF SQL applies exact registered message type allow-list before claim");
Check(claimSql.Contains("[MessageType] + N'|'") && claimSql.Contains("notification.in-app.v1|"),
    "actual SQL compares suffixed identifiers so trailing-space padding cannot claim an unregistered alias");
Check(claimSql.Contains("[State] = 0") && claimSql.Contains("[State] = 1") && claimSql.Contains("[AvailableAt]") && claimSql.Contains("[LeaseExpiresAt]") && claimSql.Contains("ORDER BY") && claimSql.Contains("TOP"),
    "actual EF claim query keeps pending/expired lease eligibility and bounded deterministic ordering");
Check(db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed,"claim translation/model inspection never opens a database connection");
var outboxModel = db.Model.FindEntityType(typeof(OutboxMessage))!;
Check(outboxModel.FindProperty(nameof(OutboxMessage.RowVersion))!.IsConcurrencyToken, "outbox has mapped rowversion");
Check(outboxModel.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MessageType", "DeduplicationKey" })),
    "outbox producer identity has SQL unique index");
Check(db.Model.FindEntityType(typeof(Notification))!.FindProperty(nameof(Notification.RowVersion))!.IsConcurrencyToken,
    "notification has mapped rowversion");
Check(typeof(NotificationResponse).GetProperties().All(x => !x.Name.Contains("Reference") && !x.Name.Contains("Recipient") && !x.Name.Contains("Employee")),
    "public notification contract excludes internal references and identity fields");
Console.WriteLine($"Notifications/outbox: {checks} offline service/domain/model checks passed. SQL concurrency, rollback, HTTP and restart are NOT exercised.");

public sealed class MutableClock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now; }
public sealed class Current(AuthenticatedAccount actor) : ICurrentAuthentication { public AuthenticatedAccount Actor = actor; public AuthenticatedAccount Require() => Actor; }
public class Transactions : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name == "InTransactionAsync"
        ? ((Delegate)arguments![0]!).DynamicInvoke(arguments[1]) : throw new NotSupportedException();
}
public sealed class MemoryFiles : IPrivateFileStore
{
    public int Writes; public bool FailReads;
    private readonly Dictionary<string, (byte[] Bytes, string Purpose)> values = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); Writes++; values[key] = (content.ToArray(), purpose);
        return Task.FromResult(new PrivateFileReference(key, "v1", new string('A', 64), "memory-only", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        if (FailReads || !values.TryGetValue(reference.StorageKey, out var value) || value.Purpose != purpose)
            throw new AuthenticationFailure("private_storage_unavailable", 503);
        return Task.FromResult(value.Bytes.ToArray());
    }
}
public sealed class Inbox : INotificationRepository
{
    public List<Notification> Items = []; public List<AuditEvent> Audits = []; public bool CurrentSession = true;
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct) => Task.FromResult(CurrentSession);
    public Task<PagedResult<Notification>> ListAsync(string recipient, NotificationListRequest request, CancellationToken ct)
    {
        var query = Items.Where(x => x.RecipientUserId == recipient && x.Channel == NotificationChannel.InApp && x.DeliveredAt != null &&
            (!request.UnreadOnly || x.ReadAt is null)).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToArray();
        return Task.FromResult(new PagedResult<Notification>(query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(),
            query.Length, request.Page, request.PageSize));
    }
    public Task<Notification?> FindAsync(string recipient, Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x =>
        x.Id == id && x.RecipientUserId == recipient && x.Channel == NotificationChannel.InApp && x.DeliveredAt != null));
    public void ExpectVersion(Notification notification, byte[] version) { }
    public void Add(Notification notification) { SetVersion(notification, 1); Items.Add(notification); }
    public void AddAudit(AuditEvent audit) => Audits.Add(audit);
    public Task SaveChangesAsync(CancellationToken ct) { foreach (var item in Items) SetVersion(item, 2); return Task.CompletedTask; }
    private static void SetVersion(Notification item, long version) => typeof(Notification).GetProperty("RowVersion")!.SetValue(item, BitConverter.GetBytes(version));
}
public sealed class Jobs(MutableClock clock, Inbox inbox) : IDurableJobRepository
{
    public List<OutboxMessage> Messages = []; public List<AuditEvent> Audits = []; public HashSet<Guid> Accounts = [];
    public bool InProducerTransaction, FailCommitOnce; public int Saves; public string? CorruptNextClaimType;
    public void RequireProducerTransaction() { if (!InProducerTransaction) throw new InvalidOperationException("No transaction"); }
    public Task<OutboxMessage?> FindByIdentityAsync(string kind, string key, CancellationToken ct) =>
        Task.FromResult(Messages.SingleOrDefault(x => x.MessageType == kind && x.DeduplicationKey == key));
    public Task<bool> RecipientExistsAsync(Guid accountId, CancellationToken ct) => Task.FromResult(Accounts.Contains(accountId));
    public void Stage(OutboxMessage message) { RequireProducerTransaction(); Messages.Add(message); }
    public void AddAudit(AuditEvent audit) => Audits.Add(audit);
    public Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> allowedMessageTypes, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        var allowed=DurableJobMessageTypes.RequireAllowed(allowedMessageTypes);
        var item = Messages.FirstOrDefault(x => allowed.Contains(x.MessageType,StringComparer.Ordinal) &&
            (x.State == DispatchState.Pending && x.AvailableAt <= now || x.State == DispatchState.Processing && x.LeaseExpiresAt <= now));
        if (item is null) return Task.FromResult<DurableJobClaim?>(null);
        var token = item.Claim(now, now.Add(lease)); Saves++;
        if(CorruptNextClaimType is {} changed){typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.MessageType))!.SetValue(item,changed);CorruptNextClaimType=null;}
        return Task.FromResult<DurableJobClaim?>(new(item.Id, token, item.LeaseExpiresAt!.Value, item.Attempts));
    }
    public async Task<bool> CompleteAsync(DurableJobClaim claim, Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct)
    {
        var item = Messages.Single(x => x.Id == claim.MessageId);
        if (!Owns(item, claim)) return false;
        var count = inbox.Items.Count; var auditCount = Audits.Count;
        InProducerTransaction = true;
        try
        {
            await action(item, ct);
            if (FailCommitOnce) { FailCommitOnce = false; throw new IOException("Synthetic commit failure"); }
            item.Complete(claim.LeaseToken, clock.Now); Saves++;
            return true;
        }
        catch { inbox.Items.RemoveRange(count, inbox.Items.Count - count); Audits.RemoveRange(auditCount, Audits.Count - auditCount); throw; }
        finally { InProducerTransaction = false; }
    }
    public Task<bool> FailAsync(DurableJobClaim claim, string code, DateTimeOffset? retryAt, CancellationToken ct)
    {
        var item = Messages.Single(x => x.Id == claim.MessageId);
        if (!Owns(item, claim)) return Task.FromResult(false);
        item.RecordFailure(claim.LeaseToken, clock.Now, code, retryAt); Saves++;
        return Task.FromResult(true);
    }
    private bool Owns(OutboxMessage item, DurableJobClaim claim) => item.State == DispatchState.Processing && item.LeaseToken == claim.LeaseToken &&
        item.LeaseExpiresAt == claim.LeaseExpiresAt && item.LeaseExpiresAt > clock.Now;
}

public class MembershipProxy : DispatchProxy
{
    public IReadOnlyList<EmployeeAssignment> Rows = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "GetSiteMembershipImpactAsync"
        ? Task.FromResult(Rows) : throw new NotSupportedException();
}
public sealed class RecipientRepository : ISchedulingNotificationRepository
{
    public IReadOnlyList<SchedulingNotificationRecipient> Rows = []; public bool Poison;
    public Task<IReadOnlyList<SchedulingNotificationRecipient>> AccountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SchedulingNotificationRecipient>>(Poison ? [new(Guid.NewGuid(),Guid.NewGuid())] : Rows.Where(x=>ids.Contains(x.EmployeeId)).ToArray());
}
