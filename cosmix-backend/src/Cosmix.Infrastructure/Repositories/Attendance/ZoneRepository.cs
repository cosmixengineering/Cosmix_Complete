using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>All access takes place inside the shared serializable attendance authorization transaction.</summary>
public sealed class ZoneRepository(CosmixDbContext db) : IZoneRepository
{
    private const int HistoryLimit=2048;
    public async Task ValidateSiteAsync(Guid siteId,CancellationToken ct)
    { if(!await db.Sites.AnyAsync(x=>x.Id==siteId && x.IsActive,ct)) throw AttendanceOperation.Missing("zone_site_unavailable"); }
    public Task<OperationalZone?> ZoneAsync(Guid siteId,Guid zoneId,CancellationToken ct)=>
        db.OperationalZones.SingleOrDefaultAsync(x=>x.Id==zoneId && x.SiteId==siteId,ct);
    public async Task<IReadOnlyList<OperationalZone>> ZonesAsync(Guid siteId,int page,int size,CancellationToken ct)=>
        await db.OperationalZones.AsNoTracking().Where(x=>x.SiteId==siteId).OrderBy(x=>x.Code).ThenBy(x=>x.Id).Skip((page-1)*size).Take(size+1).ToArrayAsync(ct);
    public async Task<ZoneType> TypeAsync(string code,CancellationToken ct)
    {
        if(code is not ("OPERATIONAL" or "RED")) throw AttendanceOperation.Invalid("unsupported_zone_type");
        var expected=code=="RED"?ZoneEntryRule.Prohibited:ZoneEntryRule.Permitted;
        var row=await db.ZoneTypes.SingleOrDefaultAsync(x=>x.Code==code,ct);
        if(row is not null)
        { if(!row.IsActive || row.EntryRule!=expected) throw AttendanceOperation.Conflict("zone_type_history_conflict"); return row; }
        row=new ZoneType(code,code=="RED"?"Red restricted area":"Operational allowed area",code=="RED"?"red":"green",expected);
        db.ZoneTypes.Add(row); return row;
    }
    public async Task<IReadOnlyList<ZoneVersion>> VersionsAsync(Guid zoneId,CancellationToken ct)=>
        Bounded(await db.ZoneVersions.AsNoTracking().Include(x=>x.ZoneType).Where(x=>x.OperationalZoneId==zoneId).OrderBy(x=>x.VersionNumber).Take(HistoryLimit+1).ToArrayAsync(ct));
    public Task<ZoneVersion?> VersionAsync(Guid siteId,Guid versionId,CancellationToken ct)=>
        db.ZoneVersions.AsNoTracking().Include(x=>x.Zone).Include(x=>x.ZoneType).SingleOrDefaultAsync(x=>x.Id==versionId && x.Zone.SiteId==siteId,ct);
    public async Task<IReadOnlyList<ZoneMonitoringConfigurationVersion>> ConfigurationsAsync(Guid siteId,CancellationToken ct)=>
        Bounded(await db.Set<ZoneMonitoringConfigurationVersion>().AsNoTracking().Where(x=>x.SiteId==siteId).OrderBy(x=>x.VersionNumber).Take(HistoryLimit+1).ToArrayAsync(ct));
    public async Task<IReadOnlyList<ZoneAssignmentRecord>> AssignmentsAsync(Guid zoneId,CancellationToken ct)
    {
        var rows=Bounded(await db.ZoneAssignments.Include(x=>x.ZoneVersion).ThenInclude(x=>x.ZoneType)
            .Where(x=>x.ZoneVersion.OperationalZoneId==zoneId).OrderBy(x=>x.EffectiveFromUtc).ThenBy(x=>x.Id).Take(HistoryLimit+1).ToArrayAsync(ct));
        return await WithEnds(rows,ct);
    }
    public async Task ValidatePopulationAsync(Guid siteId,ZonePopulationKind kind,Guid targetId,CancellationToken ct)
    {
        var exists=kind switch
        {
            ZonePopulationKind.Site=>targetId==siteId,
            ZonePopulationKind.Employee=>await db.Employees.AnyAsync(x=>x.Id==targetId,ct),
            ZonePopulationKind.Role=>await db.Roles.AnyAsync(x=>x.Id==targetId && x.IsActive,ct),
            ZonePopulationKind.Team=>await db.Teams.AnyAsync(x=>x.Id==targetId && x.IsActive,ct),
            _=>false
        };
        if(!exists) throw AttendanceOperation.Invalid("invalid_zone_population");
    }
    internal IQueryable<Cosmix.Domain.Attendance.WorkSessions.WorkSession> ActiveDutyQuery(Guid employeeId,DateTimeOffset now)=>
        db.WorkSessions.AsNoTracking().Where(s=>s.EmployeeId==employeeId && s.EndsAtUtc==null && s.StartsAtUtc<=now && s.SiteId!=null &&
            s.CheckInPunchId!=null && !db.WorkSessions.Any(later=>later.SessionKey==s.SessionKey && later.Revision>s.Revision) &&
            db.RawPunchEvents.Any(p=>p.Id==s.CheckInPunchId && p.EmployeeId==s.EmployeeId && p.Action==PunchAction.CheckIn &&
                p.SiteId==s.SiteId && p.BusinessDate==s.AttendanceDate && p.ReceivedAtUtc==s.StartsAtUtc &&
                db.PunchAssessments.Any(a=>a.RawPunchEventId==p.Id && a.EmployeeId==p.EmployeeId &&
                    (a.Outcome==PunchAssessmentOutcome.Accepted || a.Outcome==PunchAssessmentOutcome.AcceptedWithException) &&
                    !db.PunchAssessments.Any(n=>n.RawPunchEventId==p.Id && n.Revision>a.Revision) &&
                    db.FaceVerificationAttempts.Any(f=>f.Id==a.FaceVerificationAttemptId && f.EmployeeId==p.EmployeeId &&
                        f.Outcome==FaceVerificationOutcome.Passed && f.LivenessPassed==true && f.MatchPassed==true &&
                        f.ConsumedByPunchId==p.Id && f.ConsumedAtUtc!=null && f.RegisteredDeviceId==p.RegisteredDeviceId && f.PunchIdempotencyKey==p.IdempotencyKey))) &&
            !db.RawPunchEvents.Any(p=>p.EmployeeId==s.EmployeeId && p.Action==PunchAction.CheckOut && p.ReceivedAtUtc>=s.StartsAtUtc && p.ReceivedAtUtc<=now &&
                db.PunchAssessments.Any(a=>a.RawPunchEventId==p.Id && (a.Outcome==PunchAssessmentOutcome.Accepted || a.Outcome==PunchAssessmentOutcome.AcceptedWithException) &&
                    !db.PunchAssessments.Any(n=>n.RawPunchEventId==p.Id && n.Revision>a.Revision))));
    public async Task<ZoneDuty?> DutyAsync(Guid employeeId,DateTimeOffset now,CancellationToken ct)
    {
        var rows=await ActiveDutyQuery(employeeId,now).OrderBy(x=>x.StartsAtUtc).Take(2).ToArrayAsync(ct);
        if(rows.Length>1) throw AttendanceOperation.Conflict("zone_active_duty_conflict");
        return rows.Length==0?null:new(rows[0],rows[0].CheckInPunchId!.Value);
    }
    internal IQueryable<Cosmix.Domain.Attendance.WorkSessions.WorkSession> HistoricalDutyQuery(Guid employeeId,Guid sessionKey,DateTimeOffset capturedAtUtc)
    {
        if(employeeId==Guid.Empty || sessionKey==Guid.Empty || capturedAtUtc.Offset!=TimeSpan.Zero || capturedAtUtc==DateTimeOffset.MinValue)
            throw new ArgumentException("An explicit employee/session/UTC capture scope is required.");
        return db.WorkSessions.AsNoTracking().Where(s=>s.EmployeeId==employeeId && s.SessionKey==sessionKey &&
            s.StartsAtUtc<=capturedAtUtc && (s.EndsAtUtc==null || s.EndsAtUtc>capturedAtUtc) && s.SiteId!=null && s.CheckInPunchId!=null &&
            !db.WorkSessions.Any(later=>later.EmployeeId==s.EmployeeId && later.SessionKey==s.SessionKey && later.Revision>s.Revision) &&
            db.RawPunchEvents.Any(p=>p.Id==s.CheckInPunchId && p.EmployeeId==s.EmployeeId && p.Action==PunchAction.CheckIn &&
                p.SiteId==s.SiteId && p.BusinessDate==s.AttendanceDate && p.ReceivedAtUtc==s.StartsAtUtc &&
                db.PunchAssessments.Any(a=>a.RawPunchEventId==p.Id && a.EmployeeId==p.EmployeeId &&
                    (a.Outcome==PunchAssessmentOutcome.Accepted || a.Outcome==PunchAssessmentOutcome.AcceptedWithException) &&
                    !db.PunchAssessments.Any(n=>n.RawPunchEventId==p.Id && n.Revision>a.Revision) &&
                    db.FaceVerificationAttempts.Any(f=>f.Id==a.FaceVerificationAttemptId && f.EmployeeId==p.EmployeeId &&
                        f.Outcome==FaceVerificationOutcome.Passed && f.LivenessPassed==true && f.MatchPassed==true &&
                        f.CompletedAtUtc!=null && f.CompletedAtUtc<=p.ReceivedAtUtc && f.ConsumedByPunchId==p.Id &&
                        f.ConsumedAtUtc!=null && f.ConsumedAtUtc<=p.ReceivedAtUtc &&
                        f.RegisteredDeviceId==p.RegisteredDeviceId && f.PunchIdempotencyKey==p.IdempotencyKey))) &&
            !db.RawPunchEvents.Any(p=>p.EmployeeId==s.EmployeeId && p.Action==PunchAction.CheckOut &&
                p.ReceivedAtUtc>=s.StartsAtUtc && p.ReceivedAtUtc<=capturedAtUtc &&
                db.PunchAssessments.Any(a=>a.RawPunchEventId==p.Id && a.EmployeeId==p.EmployeeId &&
                    (a.Outcome==PunchAssessmentOutcome.Accepted || a.Outcome==PunchAssessmentOutcome.AcceptedWithException) &&
                    !db.PunchAssessments.Any(n=>n.RawPunchEventId==p.Id && n.Revision>a.Revision))));
    }
    public async Task<ZoneDuty?> HistoricalDutyAsync(Guid employeeId,Guid sessionKey,DateTimeOffset capturedAtUtc,CancellationToken ct)
    {
        // Select today's latest retained interpretation, including a closure recorded after capture.
        var rows=await HistoricalDutyQuery(employeeId,sessionKey,capturedAtUtc).OrderByDescending(x=>x.Revision).ThenBy(x=>x.Id).Take(2).ToArrayAsync(ct);
        if(rows.Length>1) throw AttendanceOperation.Conflict("zone_historical_duty_conflict");
        return rows.Length==0?null:new(rows[0],rows[0].CheckInPunchId!.Value);
    }
    internal IQueryable<ZoneEvent> EventKeyQuery(Guid employeeId,string key)
    {
        if(employeeId==Guid.Empty) throw new ArgumentException("An explicit employee scope is required.");
        return db.ZoneEvents.AsNoTracking().Where(x=>x.EmployeeId==employeeId && x.IdempotencyKey==key);
    }
    public Task<bool> HasEventKeyAsync(Guid employeeId,string key,CancellationToken ct)=>EventKeyQuery(employeeId,key).AnyAsync(ct);
    internal IQueryable<ZoneEvent> OwnOfflineEventQuery(Guid employeeId,Guid accountId,Guid windowId,string key)
    {
        if(accountId==Guid.Empty || windowId==Guid.Empty) throw new ArgumentException("Explicit account/window scope is required.");
        return EventKeyQuery(employeeId,key).Include(x=>x.ZoneVersion).ThenInclude(x=>x.Zone).Where(x=>
            x.UserAccountId==accountId && x.DutyMonitoringWindowId==windowId && x.Source==ZoneEventSource.EmployeeReport && x.IsOffline);
    }
    public async Task<ZoneEventRecord?> OwnOfflineEventAsync(Guid employeeId,Guid accountId,Guid windowId,string key,CancellationToken ct)
    {
        var row=await OwnOfflineEventQuery(employeeId,accountId,windowId,key).SingleOrDefaultAsync(ct);
        return row is null?null:new(row,row.ZoneVersion.Zone.SiteId,await db.ZoneEventResponses.AsNoTracking()
            .Where(x=>x.ZoneEventId==row.Id).OrderByDescending(x=>x.AtUtc).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct));
    }
    internal IQueryable<ZoneAssignment> ApplicableQuery(Guid employeeId,Guid siteId,DateTimeOffset now)
    {
        var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now,"Asia/Karachi").DateTime);
        return db.ZoneAssignments.AsNoTracking().Include(x=>x.ZoneVersion).ThenInclude(x=>x.Zone).Include(x=>x.ZoneVersion).ThenInclude(x=>x.ZoneType)
            .Where(a=>a.ZoneVersion.Zone.SiteId==siteId && a.ZoneVersion.Zone.IsActive &&
                a.EffectiveFromUtc<=now && a.EffectiveToUtc>now && a.ZoneVersion.EffectiveFromUtc<=now && a.ZoneVersion.EffectiveToUtc>now &&
                !db.Set<ZoneAssignmentEnd>().Any(e=>e.ZoneAssignmentId==a.Id && e.EffectiveToUtc<=now) &&
                (a.PopulationKind==ZonePopulationKind.Employee && a.EmployeeId==employeeId ||
                 a.PopulationKind==ZonePopulationKind.Site && a.SiteId==siteId ||
                 a.PopulationKind==ZonePopulationKind.Team && db.TeamMembers.Any(m=>m.EmployeeId==employeeId && m.TeamId==a.TeamId && m.Team.IsActive &&
                    m.EffectiveFrom<=date && (m.EffectiveTo==null || m.EffectiveTo>date) && (m.TerminatedEffectiveTo==null || m.TerminatedEffectiveTo>date)) ||
                 a.PopulationKind==ZonePopulationKind.Role && db.UserRoleAssignments.Any(r=>r.EmployeeId==employeeId && r.RoleId==a.RoleId && r.Role.IsActive &&
                    r.EffectiveFrom<=now && (r.EffectiveTo==null || r.EffectiveTo>now) && (r.RevokedAt==null || r.RevokedAt>now))));
    }
    public async Task<IReadOnlyList<ZoneAssignmentRecord>> ApplicableAssignmentsAsync(Guid employeeId,Guid siteId,DateTimeOffset now,CancellationToken ct)=>
        await WithEnds(Bounded(await ApplicableQuery(employeeId,siteId,now).OrderBy(x=>x.Id).Take(HistoryLimit+1).ToArrayAsync(ct)),ct);
    private async Task<IReadOnlyList<ZoneAssignmentRecord>> WithEnds(IReadOnlyList<ZoneAssignment> rows,CancellationToken ct)
    {
        var ids=rows.Select(x=>x.Id).ToArray();
        var ends=await db.Set<ZoneAssignmentEnd>().AsNoTracking().Where(x=>ids.Contains(x.ZoneAssignmentId)).ToDictionaryAsync(x=>x.ZoneAssignmentId,ct);
        return rows.Select(x=>new ZoneAssignmentRecord(x,ends.GetValueOrDefault(x.Id))).ToArray();
    }
    public Task<DutyMonitoringWindow?> WindowAsync(Guid employeeId,Guid id,CancellationToken ct)=>
        db.DutyMonitoringWindows.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.EmployeeId==employeeId,ct);
    public async Task<IReadOnlyList<DutyMonitoringWindow>> CurrentWindowsAsync(Guid employeeId,DateTimeOffset now,CancellationToken ct)=>
        Bounded(await db.DutyMonitoringWindows.AsNoTracking().Where(x=>x.EmployeeId==employeeId && x.StartsAtUtc<=now && x.EndsAtUtc>now && x.ConfigurationVersionId!=null)
            .OrderByDescending(x=>x.StartsAtUtc).ThenBy(x=>x.Id).Take(HistoryLimit+1).ToArrayAsync(ct));
    public Task<ZoneMonitoringCheckpoint?> CheckpointAsync(Guid windowId,CancellationToken ct)=>
        db.Set<ZoneMonitoringCheckpoint>().SingleOrDefaultAsync(x=>x.DutyMonitoringWindowId==windowId,ct);
    public async Task<ZoneEventRecord?> EventAsync(Guid eventId,CancellationToken ct)
    {
        var row=await db.ZoneEvents.AsNoTracking().Include(x=>x.ZoneVersion).ThenInclude(x=>x.Zone).SingleOrDefaultAsync(x=>x.Id==eventId,ct);
        return row is null?null:new(row,row.ZoneVersion.Zone.SiteId,await db.ZoneEventResponses.AsNoTracking().Where(x=>x.ZoneEventId==row.Id).OrderByDescending(x=>x.AtUtc).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct));
    }
    internal IQueryable<ZoneEvent> EventsQuery(Guid? employeeId,Guid? siteId,AttendanceCursorPosition? cursor)
    {
        var query=db.ZoneEvents.AsNoTracking().Include(x=>x.ZoneVersion).ThenInclude(x=>x.Zone).Where(x=>
            (!employeeId.HasValue || x.EmployeeId==employeeId) && (!siteId.HasValue || x.ZoneVersion.Zone.SiteId==siteId));
        if(cursor is not null) query=query.Where(x=>x.ReceivedAtUtc<cursor.CreatedAtUtc || x.ReceivedAtUtc==cursor.CreatedAtUtc && x.Id.CompareTo(cursor.Id)<0);
        return query.OrderByDescending(x=>x.ReceivedAtUtc).ThenByDescending(x=>x.Id);
    }
    public async Task<IReadOnlyList<ZoneEventRecord>> EventsAsync(Guid? employeeId,Guid? siteId,AttendanceCursorPosition? cursor,int take,CancellationToken ct)
    {
        var rows=await EventsQuery(employeeId,siteId,cursor).Take(Math.Clamp(take,1,101)).ToArrayAsync(ct);
        var ids=rows.Select(x=>x.Id).ToArray();
        var responses=await db.ZoneEventResponses.AsNoTracking().Where(x=>ids.Contains(x.ZoneEventId) && !db.ZoneEventResponses.Any(n=>n.ZoneEventId==x.ZoneEventId &&
            (n.AtUtc>x.AtUtc || n.AtUtc==x.AtUtc && n.Id.CompareTo(x.Id)>0))).ToDictionaryAsync(x=>x.ZoneEventId,ct);
        return rows.Select(x=>new ZoneEventRecord(x,x.ZoneVersion.Zone.SiteId,responses.GetValueOrDefault(x.Id))).ToArray();
    }
    public async Task<IReadOnlyList<ZoneEvent>> ExpiredLocationsAsync(DateTimeOffset now,int take,CancellationToken ct)=>
        await db.ZoneEvents.AsNoTracking().Where(x=>x.LocationPrivateReference!=null && x.LocationExpiresAtUtc<=now &&
            !db.Set<ZoneLocationDeletion>().Any(d=>d.ZoneEventId==x.Id)).OrderBy(x=>x.LocationExpiresAtUtc).ThenBy(x=>x.Id).Take(Math.Clamp(take,1,100)).ToArrayAsync(ct);
    public Task<bool> LocationDeletedAsync(Guid eventId,CancellationToken ct)=>db.Set<ZoneLocationDeletion>().AnyAsync(x=>x.ZoneEventId==eventId,ct);
    internal IQueryable<Guid> ReviewerQuery(Guid siteId,DateTimeOffset now)=>
        (from assignment in db.UserRoleAssignments.AsNoTracking()
         join permission in db.RolePermissions.AsNoTracking() on assignment.RoleId equals permission.RoleId
         join scope in db.UserAccessScopes.AsNoTracking() on assignment.Id equals scope.UserRoleAssignmentId
         where assignment.UserAccount.Status==AccountStatus.Active && assignment.UserAccount.WebAccessEnabled && assignment.Role.IsActive &&
            assignment.EffectiveFrom<=now && (assignment.EffectiveTo==null || assignment.EffectiveTo>now) && (assignment.RevokedAt==null || assignment.RevokedAt>now) &&
            permission.Permission.Code==ZonePermissions.Review && permission.RevokedAt==null && permission.GrantedAt<=now &&
            (scope.Kind==AccessScopeKind.All || scope.Kind==AccessScopeKind.Site && scope.SiteId==siteId)
         select assignment.UserAccountId).Distinct().OrderBy(x=>x).Take(257);
    public async Task<IReadOnlyList<Guid>> ReviewerAccountIdsAsync(Guid siteId,DateTimeOffset now,CancellationToken ct)
    {
        var rows=await ReviewerQuery(siteId,now).ToArrayAsync(ct);
        return rows.Length<=256?rows:throw AttendanceOperation.Conflict("zone_reviewer_recipient_limit");
    }
    public void Add(object entity)=>db.Add(entity);
    public void ExpectVersion(ZoneMonitoringCheckpoint entity,byte[] version)=>db.Entry(entity).Property(x=>x.RowVersion).OriginalValue=version;
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
    private static IReadOnlyList<T> Bounded<T>(T[] rows)=>rows.Length<=HistoryLimit?rows:throw AttendanceOperation.Conflict("zone_history_limit");
}
