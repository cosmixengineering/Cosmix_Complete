#:project ../../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Reminders;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;

var checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
async Task Failure(Func<Task> action,string code)
{try{await action();throw new Exception("Expected " + code);}catch(DurableJobFailure error){Check(error.Code==code,"failure: "+code);}}
async Task Conflict(Func<Task> action,string code)
{try{await action();throw new Exception("Expected " + code);}catch(AuthenticationFailure error){Check(error.StatusCode==409 && error.Code==code,"conflict: "+code);}}
var date=new DateOnly(2026,10,12);var employee=Guid.NewGuid();var recipient=Guid.NewGuid();var site=Guid.NewGuid();
ShiftVersion Version(int? minutes)=>new(Guid.NewGuid(),1,date,new(20,0),new(5,0),1,540,0,0,ShiftWeekdays.All,minutes);
var legacy=Version(null);var configured=Version(60);
Check(legacy.MissingCheckoutReminderAfterMinutes is null,"old versions remain unconfigured");
Check(configured.MissingCheckoutReminderAfterMinutes==60,"explicit immutable minutes retained");
try{Version(0);throw new Exception("Expected invalid");}catch(ArgumentOutOfRangeException){checks++;}
try{Version(-1);throw new Exception("Expected invalid");}catch(ArgumentOutOfRangeException){checks++;}
var oldRoster=RosterSchedule.FromVersion(employee,date,site,Guid.NewGuid(),legacy);
Check(!JsonSerializer.Serialize(oldRoster).Contains("MissingCheckoutReminderAfterMinutes"),"null field does not change historical roster fingerprints");
Check(!JsonSerializer.Serialize(new ShiftScheduleRequest()).Contains("MissingCheckoutReminderAfterMinutes"),"legacy shift request retry fingerprint omits null delay");
Check(!JsonSerializer.Serialize(new ShiftVersionResponse(legacy.Id,1,date,null,new(20,0),new(5,0),1,540,0,0,ShiftWeekdays.All)).Contains("MissingCheckoutReminderAfterMinutes"),"legacy protected shift response retains JSON shape");
var roster=RosterSchedule.FromVersion(employee,date,site,Guid.NewGuid(),configured);
Check(roster.MissingCheckoutReminderAfterMinutes==60,"roster exposes pinned setting");
var start=new DateTimeOffset(2026,10,12,15,0,0,TimeSpan.Zero);
var session=new WorkSession(Guid.NewGuid(),1,employee,date,start,null,configured.Id,null,start,null,correctionId:Guid.NewGuid(),siteId:site);
var state=new MissingCheckoutReminderState(session,configured,recipient);
var due=new DateTimeOffset(2026,10,13,1,0,0,TimeSpan.Zero);
Check(MissingCheckoutReminderRules.DueAt(state)==due,"overnight end plus delay uses original business date");
var officeDuty=new WorkSession(Guid.NewGuid(),1,employee,date,start,null,configured.Id,null,start,null,
    correctionId:Guid.NewGuid(),branchId:Guid.NewGuid(),officeRosterAssignmentId:Guid.NewGuid(),
    employeeAssignmentId:Guid.NewGuid(),officeGeofenceVersionId:Guid.NewGuid());
Check(MissingCheckoutReminderRules.DueAt(new(officeDuty,configured,recipient))==due,
    "office reminder uses retained overnight shift, not a fake site or Head Office hours");
Check(!MissingCheckoutReminderRules.IsDue(state,due.AddTicks(-1))&&MissingCheckoutReminderRules.IsDue(state,due),"exact due boundary");
var oldSession=new WorkSession(Guid.NewGuid(),1,employee,date,start,null,legacy.Id,null,start,null,correctionId:Guid.NewGuid(),siteId:site);
Check(MissingCheckoutReminderRules.DueAt(new(oldSession,legacy,recipient)) is null,"unconfigured version cannot schedule a reminder");
var closed=new WorkSession(session.SessionKey,2,employee,date,start,due,configured.Id,null,due,null,correctionId:Guid.NewGuid(),siteId:site);
Check(!MissingCheckoutReminderRules.IsDue(new(closed,configured,recipient),due.AddHours(1)),"closed correction suppresses reminder");

var repository=new Repository{State=state};var clock=new Clock{Now=due};var jobs=new Jobs();var files=new Files();
repository.Queued=key=>jobs.Messages.Any(x=>x.MessageType==MissingCheckoutReminderProcessor.MessageType&&x.DeduplicationKey==key.ToString("N"));
var journal=DispatchProxy.Create<ISchedulingJournalRepository,Journal>();var transactions=DispatchProxy.Create<IAccessManagementRepository,Transactions>();
var options=new MissingCheckoutReminderOptions{Enabled=true};var delivery=new AttendanceJobOptions{Enabled=true};
var processor=new MissingCheckoutReminderProcessor(repository,transactions,journal,jobs,files,options,delivery,clock);
repository.Candidates=[new(employee,1)];
Check((await processor.RunBatchAsync(0,default)).Staged==1,"due open duty persists durable reminder");
Check(jobs.Messages.Count==1 && files.Writes==1,"one protected payload and outbox intent");
Check((await processor.RunBatchAsync(0,default)).Staged==0&&jobs.Messages.Count==1,"repeat sweep idempotent");
var originalQueued=repository.Queued;repository.Queued=_=>true;
Check((await processor.RunBatchAsync(0,default)).Staged==0&&files.Writes==1,"existence hint can only skip staging, never authorize delivery or write evidence");
repository.Queued=originalQueued;
var notices=DispatchProxy.Create<INotificationRepository,Notices>();
var handler=new MissingCheckoutReminderHandler(repository,jobs,notices,files,options,clock);
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1&&((Notices)(object)notices).Items.Single().RecipientUserId==recipient.ToString(),"delivery targets existing employee account only");
Check(((Notices)(object)notices).Items.Single().TemplateCode==NotificationTemplates.MissingPunch,"safe existing in-app template");
repository.State=new(closed,configured,recipient);
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1&&jobs.Audits.Last().EventType=="attendance.missing-checkout-reminder.suppressed","closure before delivery is suppressed with audit");
repository.State=null;
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1,"inactive account or unavailable active duty never receives a stale reminder");
options.Enabled=false;
var reads=repository.Loads;
Check((await processor.RunBatchAsync(0,default)).Staged==0&&repository.Loads==reads,"disabled producer writes nothing");
options.Enabled=true;delivery.Enabled=false;
Check((await processor.RunBatchAsync(0,default)).Staged==0&&repository.Loads==reads,"outbox disabled also prevents production");
Check(repository.LockCalls==3,"delivery serializes authorization and duty checks");
options.Enabled=false;
await Failure(()=>handler.ApplyAsync(jobs.Messages.Single(),default),"missing_checkout_reminders_disabled");
Check(((Notices)(object)notices).Items.Count==1,"disabled handler cannot deliver");
options.Enabled=true;delivery.Enabled=true;
foreach(var minutes in new[]{30,60,120})
{
 var dayVersion=new ShiftVersion(Guid.NewGuid(),1,date,new(9,0),new(18,0),0,540,0,0,ShiftWeekdays.All,minutes);
 var duty=new WorkSession(Guid.NewGuid(),1,employee,date,start.AddHours(-11),null,dayVersion.Id,null,start,null,correctionId:Guid.NewGuid(),siteId:site);
 var expected=new DateTimeOffset(2026,10,12,13,0,0,TimeSpan.Zero).AddMinutes(minutes);
 Check(MissingCheckoutReminderRules.DueAt(new(duty,dayVersion,recipient))==expected,"shift form delay " + minutes + " minutes applies after same-day scheduled end");
}
var restVersion=new ShiftVersion(Guid.NewGuid(),1,date,new(9,0),new(18,0),0,540,0,0,ShiftWeekdays.Sunday,30);
var restSession=new WorkSession(Guid.NewGuid(),1,employee,date,start,null,restVersion.Id,null,start,null,correctionId:Guid.NewGuid(),siteId:site);
Check(MissingCheckoutReminderRules.DueAt(new(restSession,restVersion,recipient)) is null,"no scheduled end on rest day means no guessed deadline");
await Conflict(()=>Task.FromResult(MissingCheckoutReminderRules.DueAt(new(session,legacy,recipient))),"missing_checkout_reminder_source_conflict");
await Conflict(()=>Task.FromResult(MissingCheckoutReminderRules.DueAt(new(session,configured,Guid.Empty))),"missing_checkout_reminder_source_conflict");
repository.State=new(session,configured,Guid.NewGuid());
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1,"recipient replacement does not redirect an existing intent");
var revisedOpen=new WorkSession(session.SessionKey,2,employee,date,start,null,configured.Id,null,due,null,correctionId:Guid.NewGuid(),siteId:site);
repository.State=new(revisedOpen,configured,recipient);
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1,"even open corrected revision suppresses stale source intent");
repository.State=state;clock.Now=due.AddTicks(-1);
await handler.ApplyAsync(jobs.Messages.Single(),default);
Check(((Notices)(object)notices).Items.Count==1,"delivery rechecks deadline and never sends early");
clock.Now=due;
repository.Error=new AuthenticationFailure("missing_checkout_reminder_multiple_open_duties",409);
await Failure(()=>handler.ApplyAsync(jobs.Messages.Single(),default),"missing_checkout_reminder_multiple_open_duties");
Check(((Notices)(object)notices).Items.Count==1,"ambiguous duty never delivers");
Check((await processor.RunBatchAsync(0,default)).ReviewRequired==1,"sweep records conflicting source and continues");
repository.Error=null;
repository.Candidates=[new(employee,2),new(employee,3)];
await Conflict(()=>processor.RunBatchAsync(0,default),"missing_checkout_reminder_candidates_invalid");
repository.Candidates=[new(employee,3),new(Guid.NewGuid(),2)];
await Conflict(()=>processor.RunBatchAsync(0,default),"missing_checkout_reminder_candidates_invalid");
repository.Candidates=[new(employee,1)];options.BatchSize=1;
Check((await processor.RunBatchAsync(0,default)).NextEmployeeNumber==1,"full bounded page advances keyset");
repository.Candidates=[];
Check((await processor.RunBatchAsync(1,default)).NextEmployeeNumber==0,"end of bounded sweep restarts safely");
var malformed=new OutboxMessage(MissingCheckoutReminderProcessor.MessageType,jobs.Messages.Single().EncryptedPayloadReference,due,"synthetic-invalid","not-a-session-key");
await Failure(()=>handler.ApplyAsync(malformed,default),"missing_checkout_reminder_identity_invalid");
var wrongKey=new OutboxMessage(MissingCheckoutReminderProcessor.MessageType,jobs.Messages.Single().EncryptedPayloadReference,due,"synthetic-wrong-key",Guid.NewGuid().ToString("N"));
// Actual private storage rejects a swapped purpose; the synthetic store also verifies that binding.
try{await handler.ApplyAsync(wrongKey,default);throw new Exception("Expected purpose mismatch");}catch(Exception error)when(error.Message=="purpose mismatch"){checks++;}
Console.WriteLine($"Missing checkout reminders: {checks} synthetic domain/service checks passed. SQL, real encrypted disk and live notification delivery NOT executed.");

public sealed class Clock:TimeProvider{public DateTimeOffset Now;public override DateTimeOffset GetUtcNow()=>Now;}
public sealed class Repository:IMissingCheckoutReminderRepository
{
 public MissingCheckoutReminderState? State;public IReadOnlyList<MissingCheckoutReminderCandidate> Candidates=[];public int Loads,LockCalls;public AuthenticationFailure? Error;public Func<Guid,bool> Queued=_=>false;
 public Task<IReadOnlyList<MissingCheckoutReminderCandidate>> CandidatesAsync(long after,int limit,DateTimeOffset now,CancellationToken ct)=>Task.FromResult(Candidates);
 public Task<MissingCheckoutReminderState?> LoadAsync(Guid employeeId,DateTimeOffset now,CancellationToken ct){Loads++;if(Error is not null)throw Error;return Task.FromResult(State);}
 public Task LockAsync(CancellationToken ct){LockCalls++;return Task.CompletedTask;}
 public Task<bool> AlreadyQueuedAsync(Guid key,CancellationToken ct)=>Task.FromResult(Queued(key));
}
public sealed class Jobs:IDurableJobRepository
{
 public List<OutboxMessage> Messages=[];public List<AuditEvent> Audits=[];
 public void RequireProducerTransaction(){}
 public Task<OutboxMessage?> FindByIdentityAsync(string type,string key,CancellationToken ct)=>Task.FromResult(Messages.SingleOrDefault(x=>x.MessageType==type&&x.DeduplicationKey==key));
 public Task<bool> RecipientExistsAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
 public void Stage(OutboxMessage message)=>Messages.Add(message);
 public void AddAudit(AuditEvent audit)=>Audits.Add(audit);
 public Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> types,DateTimeOffset now,TimeSpan lease,CancellationToken ct)=>throw new NotSupportedException();
 public Task<bool> CompleteAsync(DurableJobClaim claim,Func<OutboxMessage,CancellationToken,Task> action,CancellationToken ct)=>throw new NotSupportedException();
 public Task<bool> FailAsync(DurableJobClaim claim,string code,DateTimeOffset? retry,CancellationToken ct)=>throw new NotSupportedException();
}
public sealed class Files:IPrivateFileStore
{
 public int Writes;private readonly Dictionary<string,(byte[] Bytes,string Purpose)> data=[];
 public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct){Writes++;var id=Guid.NewGuid().ToString("N");data[id]=(bytes,purpose);return Task.FromResult(new PrivateFileReference(id,"v1",new string('A',64),"synthetic",bytes.Length));}
 public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var found=data[reference.StorageKey];if(found.Purpose!=purpose)throw new Exception("purpose mismatch");return Task.FromResult(found.Bytes);}
}
public class Journal:DispatchProxy
{protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name switch{"AddAudit"=>null,"SaveChangesAsync"=>Task.CompletedTask,_=>throw new NotSupportedException(method?.Name)};}
public class Transactions:DispatchProxy
{protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="InTransactionAsync"?((Delegate)args![0]!).DynamicInvoke(args[1]):throw new NotSupportedException(method?.Name);}
public class Notices:DispatchProxy
{public List<Notification> Items=[];protected override object? Invoke(MethodInfo? method,object?[]? args){if(method?.Name=="Add"){Items.Add((Notification)args![0]!);return null;}throw new NotSupportedException(method?.Name);}}
