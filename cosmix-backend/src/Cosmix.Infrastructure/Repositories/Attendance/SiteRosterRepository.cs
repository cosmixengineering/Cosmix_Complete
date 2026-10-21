using System.Data;
using System.Data.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>
/// Site scheduling persistence used inside SchedulingOperation's authorized transaction.
/// Historical membership and immutable schedules do not depend on a master's current active flag.
/// </summary>
public sealed class SiteRosterRepository(CosmixDbContext db) : ISiteRosterRepository
{
    private const int MaximumTimelineRows = 2048;
    private const int MaximumStaff = 2000;
    private readonly Dictionary<Guid, Guid[]> newTemporaryManifests = [];

    public Task<Site?> FindSiteAsync(Guid siteId, CancellationToken ct) =>
        db.Sites.AsNoTracking().SingleOrDefaultAsync(x => x.Id == siteId, ct);

    public async Task<SiteRosterState> ReadAsync(Guid siteId, CancellationToken ct)
    {
        // An old deployment lacks HolidayCalendarId. Inspect metadata before EF selects that column.
        await RequireDirectSchemaAsync(ct);
        var assignments = Bounded(await db.ShiftPopulationAssignments.AsNoTracking()
            .Where(x => x.PopulationKind == ShiftPopulationKind.Site && x.SiteId == siteId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        var assignmentIds = assignments.Select(x => x.Id).ToArray();
        var ends = Bounded(await db.ShiftPopulationAssignmentEnds.AsNoTracking()
            .Where(x => assignmentIds.Contains(x.ShiftPopulationAssignmentId))
            .OrderBy(x => x.EffectiveTo).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        var changes = Bounded(await db.TemporaryShiftChanges.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.AttendanceDate).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1)
            .Include(x => x.Employees.OrderBy(e => e.EmployeeId).ThenBy(e => e.Id).Take(MaximumStaff + 1))
            .AsSplitQuery().ToArrayAsync(ct));
        if (changes.Any(x => x.Employees.Count > MaximumStaff))
            throw SchedulingOperation.Conflict("site_roster_staff_limit");
        var changeIds = changes.Select(x => x.Id).ToArray();
        var amendments = Bounded(await db.Set<TemporaryShiftChangeAmendment>().AsNoTracking()
            .Where(x => changeIds.Contains(x.OriginalChangeId) ||
                (x.ReplacementChangeId.HasValue && changeIds.Contains(x.ReplacementChangeId.Value)))
            .OrderBy(x => x.RecordedAtUtc).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

        var shiftIds = assignments.Select(x => x.ShiftId).Distinct().ToArray();
        var versionIds = changes.SelectMany(x => new[] { x.OriginalShiftVersionId, x.ReplacementShiftVersionId })
            .Distinct().ToArray();
        var versions = Bounded(await db.ShiftVersions.AsNoTracking()
            .Where(x => shiftIds.Contains(x.ShiftId) || versionIds.Contains(x.Id))
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.VersionNumber).ThenBy(x => x.Id)
            .Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        return new(siteId, assignments, ends, changes, versions) { TemporaryAmendments = amendments };
    }

    public async Task<IReadOnlyList<EmployeeAssignment>> MembershipsAsync(Guid employeeId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        if (from == default || to <= from) throw SchedulingOperation.Invalid("invalid_roster_interval");
        return Bounded(await db.EmployeeAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EffectiveFrom < to &&
                (x.EffectiveTo == null || from < x.EffectiveTo))
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
    }

    public async Task<IReadOnlyList<EmployeeAssignment>> StaffAsync(Guid siteId, DateOnly date, CancellationToken ct)
    {
        if (date == default) throw SchedulingOperation.Invalid("invalid_business_date");
        // Resolve all historical assignments on this date, including employees whose current site changed.
        // Duplicate/overlapping employee rows remain visible for the application to reject explicitly.
        var staff = await db.EmployeeAssignments.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.EffectiveFrom <= date &&
                (x.EffectiveTo == null || date < x.EffectiveTo))
            .OrderBy(x => x.EmployeeId).ThenBy(x => x.Id).Take(MaximumStaff + 1).ToArrayAsync(ct);
        if (staff.Length > MaximumStaff) throw SchedulingOperation.Conflict("site_roster_staff_limit");
        return staff;
    }

    public Task<ShiftVersion?> FindVersionAsync(Guid versionId, CancellationToken ct) =>
        db.ShiftVersions.AsNoTracking().Include(x => x.Shift).SingleOrDefaultAsync(x => x.Id == versionId, ct);

    public async Task<HolidayCalendar?> FindCalendarAsync(Guid calendarId, CancellationToken ct)
    {
        // Read the pinned revision, never silently substitute the latest calendar with the same code.
        var calendar = await db.HolidayCalendars.AsNoTracking()
            .Include(x => x.Holidays.OrderBy(h => h.HolidayDate).ThenBy(h => h.Id).Take(MaximumTimelineRows + 1))
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == calendarId, ct);
        if (calendar?.Holidays.Count > MaximumTimelineRows)
            throw SchedulingOperation.Conflict("site_roster_calendar_limit");
        return calendar;
    }

    public async Task RequireDirectSchemaAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) throw SchemaUnavailable();
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            if (openedHere) await db.Database.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = db.Database.GetCommandTimeout() ?? 30;
            // Read SQL Server catalog views only. No fixture, DDL or migration is executed here.
            command.CommandText = """
                SELECT CAST(CASE WHEN
                    (SELECT COUNT(*) FROM sys.columns c
                     INNER JOIN sys.tables t ON t.object_id = c.object_id
                     INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                     WHERE s.name = N'attendance' AND c.name = N'ApprovalRequestId'
                       AND t.name IN (N'ShiftPopulationAssignments', N'ShiftPopulationAssignmentEnds', N'TemporaryShiftChanges')
                       AND c.is_nullable = 1 AND c.is_computed = 0
                       AND c.system_type_id = TYPE_ID(N'uniqueidentifier')) = 3
                    AND EXISTS (
                        SELECT 1 FROM sys.columns c
                        WHERE c.object_id = OBJECT_ID(N'attendance.ShiftPopulationAssignments', N'U')
                          AND c.name = N'HolidayCalendarId' AND c.is_nullable = 1 AND c.is_computed = 0
                          AND c.system_type_id = TYPE_ID(N'uniqueidentifier'))
                    AND EXISTS (
                        SELECT 1 FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                        INNER JOIN sys.columns parentColumn ON parentColumn.object_id = fkc.parent_object_id
                            AND parentColumn.column_id = fkc.parent_column_id
                        INNER JOIN sys.columns referencedColumn ON referencedColumn.object_id = fkc.referenced_object_id
                            AND referencedColumn.column_id = fkc.referenced_column_id
                        WHERE fk.parent_object_id = OBJECT_ID(N'attendance.ShiftPopulationAssignments', N'U')
                          AND fk.referenced_object_id = OBJECT_ID(N'hr.HolidayCalendars', N'U')
                          AND parentColumn.name = N'HolidayCalendarId' AND referencedColumn.name = N'Id'
                          AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
                          AND fk.delete_referential_action = 0 AND fk.update_referential_action = 0)
                    AND (SELECT COUNT(*) FROM sys.columns c
                         WHERE c.object_id = OBJECT_ID(N'attendance.TemporaryShiftChangeAmendments', N'U')
                           AND c.is_computed = 0 AND (
                             (c.name IN (N'Id', N'OriginalChangeId') AND c.is_nullable = 0 AND c.system_type_id = TYPE_ID(N'uniqueidentifier')) OR
                             (c.name = N'ReplacementChangeId' AND c.is_nullable = 1 AND c.system_type_id = TYPE_ID(N'uniqueidentifier')) OR
                             (c.name = N'RecordedAtUtc' AND c.is_nullable = 0 AND c.system_type_id = TYPE_ID(N'datetimeoffset') AND c.scale = 7) OR
                             (c.name = N'RecordedBy' AND c.is_nullable = 0 AND c.system_type_id = TYPE_ID(N'nvarchar') AND c.max_length = 400) OR
                             (c.name = N'WrittenEvidenceReference' AND c.is_nullable = 0 AND c.system_type_id = TYPE_ID(N'nvarchar') AND c.max_length = 1000) OR
                             (c.name = N'Reason' AND c.is_nullable = 0 AND c.system_type_id = TYPE_ID(N'nvarchar') AND c.max_length = 4000))) = 7
                    AND (SELECT COUNT(DISTINCT pc.name) FROM sys.foreign_keys fk
                         INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                         INNER JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                         INNER JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
                         WHERE fk.parent_object_id = OBJECT_ID(N'attendance.TemporaryShiftChangeAmendments', N'U')
                           AND fk.referenced_object_id = OBJECT_ID(N'attendance.TemporaryShiftChanges', N'U')
                           AND pc.name IN (N'OriginalChangeId', N'ReplacementChangeId') AND rc.name = N'Id'
                           AND NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns more
                               WHERE more.constraint_object_id = fk.object_id AND more.constraint_column_id > 1)
                           AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
                           AND fk.delete_referential_action = 0 AND fk.update_referential_action = 0) = 2
                    AND (SELECT COUNT(DISTINCT c.name) FROM sys.indexes i
                         INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                         INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                         WHERE i.object_id = OBJECT_ID(N'attendance.TemporaryShiftChangeAmendments', N'U')
                           AND i.is_unique = 1 AND i.is_disabled = 0 AND ic.key_ordinal = 1
                           AND NOT EXISTS (SELECT 1 FROM sys.index_columns more WHERE more.object_id = i.object_id
                               AND more.index_id = i.index_id AND more.key_ordinal > 1)
                           AND ((c.name = N'Id' AND i.is_primary_key = 1 AND i.has_filter = 0) OR
                                (c.name = N'OriginalChangeId' AND i.has_filter = 0) OR
                                (c.name = N'ReplacementChangeId' AND i.has_filter = 1
                                 AND REPLACE(REPLACE(REPLACE(i.filter_definition, N' ', N''), N'(', N''), N')', N'') = N'[ReplacementChangeId]ISNOTNULL'))) = 3
                    AND (SELECT COUNT(*) FROM sys.check_constraints cc
                         WHERE cc.parent_object_id = OBJECT_ID(N'attendance.TemporaryShiftChangeAmendments', N'U')
                           AND cc.is_disabled = 0 AND cc.is_not_trusted = 0 AND cc.name IN (
                             N'CK_TemporaryShiftChangeAmendments_DifferentChanges',
                             N'CK_TemporaryShiftChangeAmendments_Evidence',
                             N'CK_TemporaryShiftChangeAmendments_RecordedAtUtc_Utc')) = 3
                    THEN 1 ELSE 0 END AS bit)
                """;
            if (await command.ExecuteScalarAsync(ct) is not true) throw SchemaUnavailable();
        }
        catch (DbException)
        {
            // Insufficient metadata permission and incompatible deployment both fail closed.
            throw SchemaUnavailable();
        }
        finally
        {
            if (openedHere) await db.Database.CloseConnectionAsync();
        }
    }

    public void Add(ShiftPopulationAssignment assignment) => db.ShiftPopulationAssignments.Add(assignment);

    public void Add(ShiftPopulationAssignmentEnd ending) => db.ShiftPopulationAssignmentEnds.Add(ending);

    public void Add(TemporaryShiftChange change, IReadOnlyList<Guid> employeeIds)
    {
        if (employeeIds.Count > MaximumStaff) throw SchedulingOperation.Conflict("site_roster_staff_limit");
        if (employeeIds.Count == 0) throw SchedulingOperation.Conflict("temporary_change_has_no_affected_staff");
        if (employeeIds.Any(x => x == Guid.Empty) || employeeIds.Distinct().Count() != employeeIds.Count)
            throw SchedulingOperation.Invalid("invalid_site_roster_staff");
        // The existing context guard requires a new parent and every manifest child in the same save.
        db.TemporaryShiftChanges.Add(change);
        db.TemporaryShiftChangeEmployees.AddRange(employeeIds.Select(id => new TemporaryShiftChangeEmployee(change.Id, id)));
        newTemporaryManifests.Add(change.Id, employeeIds.Order().ToArray());
    }

    public void Add(TemporaryShiftChangeAmendment amendment) => db.Set<TemporaryShiftChangeAmendment>().Add(amendment);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        // A successor cannot already exist or acquire its manifest after this amendment is saved.
        db.ChangeTracker.DetectChanges();
        foreach (var amendment in db.ChangeTracker.Entries<TemporaryShiftChangeAmendment>()
                     .Where(x => x.State == EntityState.Added).Select(x => x.Entity))
        {
            if (amendment.ReplacementChangeId is not Guid successorId) continue;
            var successor = db.ChangeTracker.Entries<TemporaryShiftChange>()
                .SingleOrDefault(x => x.Entity.Id == successorId);
            var manifest = db.ChangeTracker.Entries<TemporaryShiftChangeEmployee>()
                .Where(x => x.Entity.TemporaryShiftChangeId == successorId).ToArray();
            if (successor?.State != EntityState.Added || !newTemporaryManifests.TryGetValue(successorId, out var expected) ||
                expected.Length == 0 || manifest.Any(x => x.State != EntityState.Added) ||
                !manifest.Select(x => x.Entity.EmployeeId).Order().SequenceEqual(expected))
                throw new InvalidOperationException("Save a superseding change, its exact new staff manifest and amendment together.");
        }
        return db.SaveChangesAsync(ct);
    }

    private static T[] Bounded<T>(T[] rows) => rows.Length <= MaximumTimelineRows ? rows :
        throw SchedulingOperation.Conflict("site_roster_timeline_limit");

    private static AuthenticationFailure SchemaUnavailable() => new("site_roster_schema_not_ready", 503);
}
