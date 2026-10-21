using System.Data;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class OfficeRosterRepository(CosmixDbContext db) : IOfficeRosterRepository
{
    private const int Limit = 2048;
    public Task<Branch?> FindBranchAsync(Guid id, CancellationToken ct) => db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<EmployeeAssignment?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await db.EmployeeAssignments.AsNoTracking().Include(x => x.StaffCategory)
            .Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date && (x.EffectiveTo == null || date < x.EffectiveTo))
            .Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        return rows.SingleOrDefault();
    }
    public async Task<OfficeRosterState> ReadAsync(Guid branchId, CancellationToken ct)
    {
        await RequireSchemaAsync(ct);
        var assignments = Bounded(await db.OfficeRosterAssignments.AsNoTracking().Where(x => x.BranchId == branchId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
        var ids = assignments.Select(x => x.Id).ToArray();
        var ends = Bounded(await db.OfficeRosterAssignmentEnds.AsNoTracking().Where(x => ids.Contains(x.OfficeRosterAssignmentId))
            .OrderBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
        var shifts = assignments.Select(x => x.ShiftId).Distinct().ToArray();
        var versions = Bounded(await db.ShiftVersions.AsNoTracking().Where(x => shifts.Contains(x.ShiftId))
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
        return new(branchId, assignments, ends, versions);
    }
    public async Task<IReadOnlyList<EmployeeAssignment>> MembershipImpactAsync(Guid branchId, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        var affected = db.EmployeeAssignments.Where(x => x.BranchId == branchId &&
            (x.EffectiveTo == null || from < x.EffectiveTo) && (!to.HasValue || x.EffectiveFrom < to)).Select(x => x.EmployeeId);
        return Bounded(await db.EmployeeAssignments.AsNoTracking().Include(x => x.StaffCategory)
            .Where(x => affected.Contains(x.EmployeeId) && (x.EffectiveTo == null || from < x.EffectiveTo) && (!to.HasValue || x.EffectiveFrom < to))
            .OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
    }
    public Task<HolidayCalendar?> FindCalendarAsync(Guid id, CancellationToken ct) => new SiteRosterRepository(db).FindCalendarAsync(id, ct);
    public async Task<IReadOnlyList<Guid>> PublicationBranchesAsync(Guid shiftId, DateOnly from, CancellationToken ct)
    {
        await RequireSchemaAsync(ct);
        var branches = await db.OfficeRosterAssignments.AsNoTracking().Where(x => x.ShiftId == shiftId &&
            (x.EffectiveTo == null || from < x.EffectiveTo) &&
            !db.OfficeRosterAssignmentEnds.Any(e => e.OfficeRosterAssignmentId == x.Id && e.EffectiveTo <= from))
            .Select(x => x.BranchId).Distinct().Order().Take(129).ToArrayAsync(ct);
        if (branches.Length > 128) throw SchedulingOperation.Conflict("shift_version_impact_too_large");
        return branches;
    }
    public async Task<bool> HasCurrentOrFutureUseAsync(Guid shiftId, DateOnly today, CancellationToken ct)
    {
        var branches = await PublicationBranchesAsync(shiftId, today.AddDays(-1), ct);
        foreach (var branchId in branches)
        {
            var state = await ReadAsync(branchId, ct);
            if (state.Assignments.Any(x => x.ShiftId == shiftId && (!OfficeRosterSchedule.End(state, x).HasValue || OfficeRosterSchedule.End(state, x) > today))) return true;
            var prior = OfficeRosterSchedule.ResolveSchedule(Guid.Empty, today.AddDays(-1), state);
            if (prior.ShiftId == shiftId && prior.EndsAt.HasValue && prior.EndsAt.Value.DateTime > today.ToDateTime(TimeOnly.MinValue)) return true;
        }
        return false;
    }
    public async Task RequireSchemaAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) throw Unavailable();
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            if (openedHere) await db.Database.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = db.Database.GetCommandTimeout() ?? 30;
            command.CommandText = """
                SELECT CAST(CASE WHEN
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'attendance.OfficeRosterAssignments', N'U')
                     AND is_computed = 0 AND name IN (N'Id', N'BranchId', N'ShiftId', N'EffectiveFrom', N'EffectiveTo', N'HolidayCalendarId', N'RecordedBy', N'RecordedAtUtc')) = 8
                    AND (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'attendance.OfficeRosterAssignmentEnds', N'U')
                     AND is_computed = 0 AND name IN (N'Id', N'OfficeRosterAssignmentId', N'EffectiveTo', N'Reason', N'RecordedBy', N'RecordedAtUtc')) = 6
                    AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'attendance.OfficeRosterAssignments', N'U')
                     AND is_disabled = 0 AND is_not_trusted = 0 AND delete_referential_action = 0 AND update_referential_action = 0) = 3
                    AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'attendance.OfficeRosterAssignmentEnds', N'U')
                     AND referenced_object_id = OBJECT_ID(N'attendance.OfficeRosterAssignments', N'U')
                     AND is_disabled = 0 AND is_not_trusted = 0 AND delete_referential_action = 0 AND update_referential_action = 0)
                    AND EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                     JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                     WHERE i.object_id = OBJECT_ID(N'attendance.OfficeRosterAssignmentEnds', N'U')
                     AND i.is_unique = 1 AND i.is_disabled = 0 AND i.has_filter = 0 AND ic.key_ordinal = 1 AND c.name = N'OfficeRosterAssignmentId'
                     AND NOT EXISTS (SELECT 1 FROM sys.index_columns extra WHERE extra.object_id = i.object_id AND extra.index_id = i.index_id AND extra.key_ordinal > 1))
                    THEN 1 ELSE 0 END AS int)
                """;
            if (await command.ExecuteScalarAsync(ct) is not int result || result != 1) throw Unavailable();
        }
        finally { if (openedHere) await db.Database.CloseConnectionAsync(); }
    }
    public void Add(OfficeRosterAssignment assignment) => db.OfficeRosterAssignments.Add(assignment);
    public void Add(OfficeRosterAssignmentEnd ending) => db.OfficeRosterAssignmentEnds.Add(ending);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static T[] Bounded<T>(T[] rows) => rows.Length <= Limit ? rows : throw SchedulingOperation.Conflict("office_roster_history_limit");
    private static AuthenticationFailure Unavailable() => new("office_roster_schema_required", 503);
}
