using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class SiteOperationalRepository(CosmixDbContext db, TimeProvider clock,
    ISiteRosterRepository rosters, ISiteGeofenceRepository geofences) : ISiteOperationalRepository
{
    public async Task<SiteOperationalDetailsResponse?> GetSiteOperationalDetailsAsync(Guid siteId, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == siteId, ct);

        if (site is null)
            return null;

        var nowUtc = clock.GetUtcNow();
        var today = RosterSchedule.Today(nowUtc);

        // 1. City Name
        string? cityName = null;
        if (site.CityId.HasValue)
        {
            cityName = await db.Cities.AsNoTracking()
                .Where(c => c.Id == site.CityId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Supervisor Info
        SiteSupervisorInfo? supervisorInfo = null;
        if (site.SupervisorEmployeeId.HasValue)
        {
            var sup = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == site.SupervisorEmployeeId.Value, ct);

            if (sup is not null)
            {
                var supAssignments = await db.EmployeeAssignments.AsNoTracking()
                    .Include(a => a.Designation)
                    .Where(a => a.EmployeeId == sup.Id && a.EffectiveFrom <= today &&
                        (!a.EffectiveTo.HasValue || today < a.EffectiveTo.Value))
                    .OrderByDescending(a => a.EffectiveFrom).ThenBy(a => a.Id).Take(2)
                    .ToArrayAsync(ct);
                if (supAssignments.Length > 1)
                    throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
                var supAssignment = supAssignments.SingleOrDefault();

                supervisorInfo = new SiteSupervisorInfo(
                    sup.Id,
                    sup.EmployeeNumber,
                    sup.FullLegalName ?? "Supervisor",
                    sup.PersonalPhone,
                    sup.OfficialEmail,
                    supAssignment?.Designation?.Name
                );
            }
        }

        // 3. Geofence Info
        var geofence = await geofences.EffectiveAsync(siteId, today, ct);

        SiteGeofenceMapInfo? geofenceMapInfo = null;
        if (geofence is not null)
        {
            geofenceMapInfo = new SiteGeofenceMapInfo(
                geofence.Id,
                geofence.VersionNumber,
                geofence.EffectiveFrom,
                geofence.Latitude,
                geofence.Longitude,
                geofence.RadiusMeters,
                geofence.MaximumAccuracyMeters
            );
        }

        // 4. Shifts assigned to this site
        var rosterState = await rosters.ReadAsync(siteId, ct);
        var roster = SiteRosterSchedule.Resolve(Guid.Empty, today, rosterState);
        var shiftsList = new List<SiteAssignedShiftInfo>();
        if (roster.ShiftVersionId is Guid versionId)
        {
            var version = rosterState.Versions.Single(v => v.Id == versionId);
            var sh = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == version.ShiftId, ct)
                ?? throw SchedulingOperation.Conflict("shift_schedule_missing");
            shiftsList.Add(new SiteAssignedShiftInfo(
                sh.Id,
                sh.Code,
                sh.Name,
                version.StartTime,
                version.EndTime,
                version.ScheduledMinutes
            ));
        }

        // 5. Assigned Workers
        var assignments = await db.EmployeeAssignments.AsNoTracking()
            .Include(a => a.Employee)
            .Include(a => a.Designation)
            .Include(a => a.Department)
            .Where(a => a.SiteId == siteId && a.EffectiveFrom <= today &&
                (!a.EffectiveTo.HasValue || today < a.EffectiveTo.Value))
            .OrderBy(a => a.Employee.FullLegalName).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var assignedEmployeeIds = assignments.Select(a => a.EmployeeId).ToArray();
        if (assignedEmployeeIds.Distinct().Count() != assignedEmployeeIds.Length ||
            await db.EmployeeAssignments.AsNoTracking().AnyAsync(a => assignedEmployeeIds.Contains(a.EmployeeId) &&
                a.SiteId != siteId && a.EffectiveFrom <= today &&
                (!a.EffectiveTo.HasValue || today < a.EffectiveTo.Value), ct))
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");

        var assignedWorkers = assignments.Select(a => new SiteWorkerInfo(
            a.EmployeeId,
            a.Employee.EmployeeNumber,
            a.Employee.FullLegalName ?? "Worker",
            a.Designation?.Name ?? "Unassigned",
            a.Department?.Name ?? "Unassigned",
            a.Employee.PersonalPhone,
            a.Employee.Status.ToString(),
            a.EffectiveFrom
        )).ToList();

        // Retain the existing 24-hour report window; an open session is not proof of live presence.
        var activeSessions = await db.WorkSessions.AsNoTracking()
            .Where(w => w.SiteId == siteId && !w.EndsAtUtc.HasValue &&
                w.StartsAtUtc >= nowUtc.AddHours(-24) && w.StartsAtUtc <= nowUtc &&
                !db.WorkSessions.Any(next => next.SessionKey == w.SessionKey && next.Revision > w.Revision))
            .OrderBy(w => w.StartsAtUtc).ThenBy(w => w.Id)
            .ToListAsync(ct);

        var activeWorkerIds = activeSessions.Select(w => w.EmployeeId).Distinct().ToList();
        var activeEmployees = await db.Employees.AsNoTracking()
            .Where(e => activeWorkerIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var activeWorkersDesignations = assignments
            .Where(a => activeWorkerIds.Contains(a.EmployeeId))
            .ToDictionary(a => a.EmployeeId, a => a.Designation?.Name ?? "Unassigned");

        // Fetch check-in punches for location
        var punchIds = activeSessions.Where(s => s.CheckInPunchId.HasValue).Select(s => s.CheckInPunchId!.Value).ToList();
        var punches = await db.RawPunchEvents.AsNoTracking()
            .Where(p => punchIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var boundaryIds = punches.Values.Where(p => p.GeofenceVersionId.HasValue)
            .Select(p => p.GeofenceVersionId!.Value).Distinct().ToArray();
        var punchBoundaries = await db.SiteGeofenceVersions.AsNoTracking()
            .Where(g => g.SiteId == siteId && boundaryIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);

        var activeWorkersOnDuty = new List<SiteActiveWorkerLocationInfo>();

        foreach (var session in activeSessions)
        {
            var punch = session.CheckInPunchId.HasValue && punches.TryGetValue(session.CheckInPunchId.Value, out var p) ? p : null;
            if (punch is not null && (punch.EmployeeId != session.EmployeeId || punch.SiteId != siteId ||
                punch.Action != PunchAction.CheckIn))
                throw SchedulingOperation.Conflict("site_operational_punch_history_conflict");
            decimal? lat = punch?.Latitude;
            decimal? lon = punch?.Longitude;
            decimal? accuracy = punch?.AccuracyMeters;
            decimal? distance = null;
            bool? isInside = null;

            // Only the immutable boundary retained by this punch can classify its raw location.
            if (punch?.GeofenceVersionId is Guid boundaryId && punchBoundaries.TryGetValue(boundaryId, out var boundary) &&
                lat.HasValue && lon.HasValue && accuracy.HasValue)
            {
                var evaluation = SiteGeofenceEvaluator.Evaluate(boundary, lat.Value, lon.Value, accuracy.Value);
                distance = evaluation.DistanceMeters is double meters ? (decimal)Math.Round(meters, 2) : null;
                isInside = evaluation.Status switch { "inside" => true, "outside" => false, _ => null };
            }

            int workedMinutes = (int)(nowUtc - session.StartsAtUtc).TotalMinutes;
            activeWorkersDesignations.TryGetValue(session.EmployeeId, out var designation);
            activeEmployees.TryGetValue(session.EmployeeId, out var emp);

            activeWorkersOnDuty.Add(new SiteActiveWorkerLocationInfo(
                session.EmployeeId,
                emp?.EmployeeNumber ?? 0,
                emp?.FullLegalName ?? "Active Worker",
                designation ?? "Worker",
                session.StartsAtUtc,
                lat,
                lon,
                accuracy,
                distance,
                isInside,
                workedMinutes,
                punch?.RegisteredDeviceId
            ));
        }

        return new SiteOperationalDetailsResponse(
            site.Id,
            site.Code,
            site.Name,
            site.Address,
            cityName,
            site.ClientName,
            site.IsActive,
            site.StartsOn,
            site.EndsOn,
            supervisorInfo,
            geofenceMapInfo,
            shiftsList,
            assignedWorkers.Count,
            activeWorkerIds.Count,
            assignedWorkers,
            activeWorkersOnDuty
        );
    }
}
