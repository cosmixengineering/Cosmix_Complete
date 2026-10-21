using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Infrastructure.Zones;

public sealed class ZoneRetentionHealth : IZoneRetentionStatus
{
    private long lastCompleteTicks;
    public bool IsAvailable(DateTimeOffset now)
    {
        var value=Interlocked.Read(ref lastCompleteTicks);
        return value>0 && now.UtcTicks>=value && now.UtcTicks-value<=TimeSpan.FromMinutes(2).Ticks;
    }
    public void Complete(DateTimeOffset now)=>Interlocked.Exchange(ref lastCompleteTicks,now.UtcTicks);
    public void Fail()=>Interlocked.Exchange(ref lastCompleteTicks,0);
}
public sealed record ZoneRetentionPass(int DeletedEvents,int DeletedObjects,int InvalidEntries,string? NextCursor);
public sealed class ZoneRetentionProcessor(IZoneRepository repository,IZoneLocationStore files,
    IAccessManagementRepository transaction,ISchedulingJournalRepository journal,TimeProvider clock)
{
    public async Task<ZoneRetentionPass> RunAsync(string? cursor,CancellationToken ct)
    {
        var now=clock.GetUtcNow(); var events=await transaction.InTransactionAsync(async token=>
        {
            var rows=await repository.ExpiredLocationsAsync(now,100,token);
            foreach(var row in rows)
            {
                var reference=row.LocationPrivateReference??throw new InvalidOperationException("Zone location expiry reference is unavailable.");
                if(!await files.DeleteExpiredAsync(row.Id,row.EmployeeId,row.DutyMonitoringWindowId,row.LocationExpiresAtUtc!.Value,reference,now,token))
                    throw new InvalidOperationException("Zone location retention did not complete.");
                var deletion=new ZoneLocationDeletion(row.Id,row.LocationExpiresAtUtc.Value,clock.GetUtcNow(),ZoneProjection.Sha(reference));
                repository.Add(deletion);
                journal.AddAudit(new AuditEvent("zones.location-retention","system:zone-retention","System","Zones",row.Id.ToString(),clock.GetUtcNow(),"zone-retention"));
            }
            await repository.SaveChangesAsync(token); return rows.Count;
        },ct);
        var scan=await files.ScanExpiredAsync(now,cursor,100,ct); var removed=0;
        foreach(var item in scan.Entries)
            if(await files.DeleteExpiredAsync(item.EventId,item.EmployeeId,item.WindowId,item.ExpiresAtUtc,item.Reference,now,ct)) removed++;
        return new(events,removed,scan.InvalidEntries,scan.NextCursor);
    }
}
