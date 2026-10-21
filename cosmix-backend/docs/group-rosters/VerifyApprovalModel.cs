#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyApprovalModel.packages.lock.json

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

// Offline model/domain checks only. No authentication, permission authority, SQL execution,
// encrypted storage or completed group/temporary/holiday workflow is implied.
// dotnet run --file docs/group-rosters/VerifyApprovalModel.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAILED: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}
void Reject<T>(Action action, string name) where T : Exception
{
    var rejected = false;
    try { action(); }
    catch (T) { rejected = true; }
    Check(rejected, name);
}
var connections = new RejectConnections();
var options = new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=cosmix-group-model-only.invalid;Database=GroupModelVerificationOnly;Integrated Security=true;Encrypt=true;Connect Timeout=1")
    .AddInterceptors(connections).Options;
using var db = new CosmixDbContext(options);
var model = db.GetService<IDesignTimeModel>().Model;
Check(!db.Database.HasPendingModelChanges(), "current model matches the locally generated migration snapshot; database application is not implied");
var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model
    ?? throw new Exception("Existing model snapshot is required for the exact gap check.");
var initializedSnapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
var pendingDifferences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
    initializedSnapshot.GetRelationalModel(), model.GetRelationalModel());
Check(pendingDifferences.Count == 0, "no unrepresented model changes remain after local migration generation");
var migrations = db.GetService<IMigrationsAssembly>();
var preparedMigration = migrations.CreateMigration(migrations.Migrations.Single(x =>
    x.Key == "20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments").Value, db.Database.ProviderName!);
var differences = preparedMigration.UpOperations;
Check(preparedMigration.DownOperations.FirstOrDefault() is SqlOperation { SuppressTransaction: false } guard &&
    guard.Sql.Contains("THROW 51002") && guard.Sql.Contains("TABLOCKX, HOLDLOCK"),
    "rollback refuses to discard direct scheduling evidence while holding transaction-owned write locks");
Check(preparedMigration.DownOperations.OfType<AlterColumnOperation>().All(x =>
    x.DefaultValue is null && x.DefaultValueSql is null), "rollback never manufactures approval IDs for direct rows");
string Describe(MigrationOperation operation) => operation switch
{
    CreateTableOperation x => $"table:{x.Schema}.{x.Name}",
    AlterColumnOperation x => $"alter:{x.Schema}.{x.Table}.{x.Name}",
    AddColumnOperation x => $"add:{x.Schema}.{x.Table}.{x.Name}",
    CreateIndexOperation x => $"index:{x.Schema}.{x.Table}.{x.Name}",
    AddForeignKeyOperation x => $"fk:{x.Schema}.{x.Table}.{x.Name}",
    _ => operation.GetType().Name
};
var expectedDifferences = new[]
{
    "alter:attendance.ShiftPopulationAssignments.ApprovalRequestId",
    "alter:attendance.ShiftPopulationAssignmentEnds.ApprovalRequestId",
    "alter:attendance.TemporaryShiftChanges.ApprovalRequestId",
    "add:attendance.ShiftPopulationAssignments.HolidayCalendarId",
    "index:attendance.ShiftPopulationAssignments.IX_ShiftPopulationAssignments_HolidayCalendarId",
    "fk:attendance.ShiftPopulationAssignments.FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId",
    "table:attendance.TemporaryShiftChangeAmendments",
    "index:attendance.TemporaryShiftChangeAmendments.IX_TemporaryShiftChangeAmendments_OriginalChangeId",
    "index:attendance.TemporaryShiftChangeAmendments.IX_TemporaryShiftChangeAmendments_ReplacementChangeId"
}.Order().ToArray();
Check(differences.Select(Describe).Order().SequenceEqual(expectedDifferences),
    "model differs by exactly the intended columns, holiday index and holiday FK; actual=" +
    string.Join(",", differences.Select(Describe)));
foreach (var column in differences.OfType<AlterColumnOperation>())
    Check(column.ColumnType == "uniqueidentifier" && column.ClrType == typeof(Guid) && column.IsNullable &&
        column.OldColumn.ColumnType == "uniqueidentifier" && column.OldColumn.ClrType == typeof(Guid) && !column.OldColumn.IsNullable &&
        column.DefaultValue is null && column.DefaultValueSql is null && column.ComputedColumnSql is null,
        $"{column.Table}.ApprovalRequestId changes only to nullable Guid without fabricated default");
var holidayColumn = differences.OfType<AddColumnOperation>().Single();
Check(holidayColumn.ColumnType == "uniqueidentifier" && holidayColumn.ClrType == typeof(Guid) && holidayColumn.IsNullable &&
    holidayColumn.DefaultValue is null && holidayColumn.DefaultValueSql is null && holidayColumn.ComputedColumnSql is null,
    "new exact holiday revision reference is nullable with no automatic calendar default");
var holidayIndex = differences.OfType<CreateIndexOperation>().Single(x => x.Table == "ShiftPopulationAssignments");
Check(!holidayIndex.IsUnique && holidayIndex.Columns.SequenceEqual(["HolidayCalendarId"]) && holidayIndex.Filter is null,
    "new holiday reference index covers only its exact nullable foreign key");
var holidayForeignKey = differences.OfType<AddForeignKeyOperation>().Single();
Check(holidayForeignKey.PrincipalSchema == "hr" && holidayForeignKey.PrincipalTable == "HolidayCalendars" &&
    holidayForeignKey.Columns.SequenceEqual(["HolidayCalendarId"]) && holidayForeignKey.PrincipalColumns.SequenceEqual(["Id"]) &&
    holidayForeignKey.OnDelete == ReferentialAction.Restrict && holidayForeignKey.OnUpdate == ReferentialAction.NoAction,
    "new holiday foreign key pins an exact existing immutable revision without cascade deletion");
IEntityType Entity<T>() => model.FindEntityType(typeof(T)) ?? throw new Exception($"{typeof(T).Name} missing from model");
var population = Entity<ShiftPopulationAssignment>();
var ending = Entity<ShiftPopulationAssignmentEnd>();
var temporary = Entity<TemporaryShiftChange>();
var member = Entity<TemporaryShiftChangeEmployee>();
var amendment = Entity<TemporaryShiftChangeAmendment>();
var requestModel = Entity<ApprovalRequest>();
var decisionModel = Entity<ApprovalDecision>();
var calendarModel = Entity<HolidayCalendar>();

foreach (var (entity, table) in new[]
{
    (population, "ShiftPopulationAssignments"), (ending, "ShiftPopulationAssignmentEnds"),
    (temporary, "TemporaryShiftChanges"), (member, "TemporaryShiftChangeEmployees"),
    (amendment, "TemporaryShiftChangeAmendments")
})
{
    Check(entity.GetSchema() == "attendance" && entity.GetTableName() == table,
        $"{entity.ClrType.Name} uses its existing attendance table");
    Check(typeof(IAppendOnlyEntity).IsAssignableFrom(entity.ClrType),
        $"{entity.ClrType.Name} retains its append-only marker");
    Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict),
        $"{entity.ClrType.Name} foreign keys prohibit cascade history deletion");
}
bool Unique(IEntityType type, params string[] names) => type.GetIndexes().Any(x => x.IsUnique &&
    x.Properties.Select(p => p.Name).SequenceEqual(names));
Check(Unique(ending, nameof(ShiftPopulationAssignmentEnd.ShiftPopulationAssignmentId)),
    "population ending has a unique one-time parent index");
Check(Unique(amendment, nameof(TemporaryShiftChangeAmendment.OriginalChangeId)) &&
    Unique(amendment, nameof(TemporaryShiftChangeAmendment.ReplacementChangeId)),
    "temporary amendments prevent branching and reusing a successor");
var amendmentTable = differences.OfType<CreateTableOperation>().Single();
Check(amendmentTable.Columns.Count == 7 && amendmentTable.ForeignKeys.Count == 2 &&
    amendmentTable.ForeignKeys.All(x => x.PrincipalSchema == "attendance" && x.PrincipalTable == "TemporaryShiftChanges" &&
        x.OnDelete == ReferentialAction.Restrict), "amendment schema contains only the seven evidence/link fields and restrictive parent links");
Check(amendment.GetIndexes().Single(x => x.Properties.Select(p => p.Name)
    .SequenceEqual([nameof(TemporaryShiftChangeAmendment.ReplacementChangeId)])).GetFilter() == "[ReplacementChangeId] IS NOT NULL",
    "multiple cancellations are possible while each nonnull successor is unique");
Check(Unique(member, nameof(TemporaryShiftChangeEmployee.TemporaryShiftChangeId), nameof(TemporaryShiftChangeEmployee.EmployeeId)),
    "temporary employee manifest rejects duplicate employee membership by index");
Check(Unique(decisionModel, nameof(ApprovalDecision.ApprovalRequestId)),
    "generic approval request has at most one decision by index");
Check(Unique(calendarModel, nameof(HolidayCalendar.Code), nameof(HolidayCalendar.EffectiveFrom), nameof(HolidayCalendar.Revision)) &&
    Unique(calendarModel, nameof(HolidayCalendar.SupersedesCalendarId)),
    "holiday calendar revisions retain unique identities and a unique successor");
var requestVersion = requestModel.FindProperty(nameof(ApprovalRequest.RowVersion));
Check(requestVersion is not null && requestVersion.IsConcurrencyToken &&
    requestVersion.ValueGenerated == ValueGenerated.OnAddOrUpdate && requestVersion.GetColumnType() == "rowversion",
    "approval requests retain the actual SQL rowversion concurrency token");
Check(population.FindProperty("RowVersion") is null && ending.FindProperty("RowVersion") is null,
    "immutable population records do not pretend to expose rowversion");
Check(population.GetCheckConstraints().Any(x => x.Name == "CK_ShiftPopulationAssignments_Population") &&
    population.GetCheckConstraints().Any(x => x.Name == "CK_ShiftPopulationAssignments_Dates"),
    "population target and exclusive date checks exist in the SQL model");
Check(temporary.GetCheckConstraints().Any(x => x.Name == "CK_TemporaryShiftChanges_Versions"),
    "temporary original and replacement versions must differ in the SQL model");
Check(requestModel.GetCheckConstraints().Any(x => x.Name == "CK_ApprovalRequests_Closed") &&
    decisionModel.GetCheckConstraints().Any(x => x.Name == "CK_ApprovalDecisions_Reason"),
    "approval closure and decision-reason SQL constraints remain present");
Check(population.FindProperty(nameof(ShiftPopulationAssignment.ApprovalRequestId))?.IsNullable == true &&
    ending.FindProperty(nameof(ShiftPopulationAssignmentEnd.ApprovalRequestId))?.IsNullable == true &&
    temporary.FindProperty(nameof(TemporaryShiftChange.ApprovalRequestId))?.IsNullable == true,
    "direct population creation, ending and temporary rows allow absent approval without deleting legacy links");

var from = new DateOnly(2030, 1, 1);
var until = new DateOnly(2030, 2, 1);
var now = new DateTimeOffset(2029, 12, 1, 0, 0, 0, TimeSpan.Zero);
var shiftId = Guid.NewGuid();
var siteId = Guid.NewGuid();
var employeeId = Guid.NewGuid();
var approvalId = Guid.NewGuid();
ShiftPopulationAssignment Population() => new(shiftId, ShiftPopulationKind.Site, from, until, approvalId, siteId: siteId);
TemporaryShiftChange Temporary() => new(siteId, from, Guid.NewGuid(), Guid.NewGuid(), approvalId,
    "reviewer", now, "synthetic-written-evidence", "Synthetic model verification");
ApprovalRequest Request() => new("SyntheticRosterRequest", siteId, "requester", "synthetic-protected-reference", 1, now);

foreach (var kind in Enum.GetValues<ShiftPopulationKind>())
{
    var target = Guid.NewGuid();
    var row = new ShiftPopulationAssignment(shiftId, kind, from, until, approvalId,
        teamId: kind == ShiftPopulationKind.Team ? target : null,
        siteId: kind == ShiftPopulationKind.Site ? target : null,
        staffCategoryId: kind == ShiftPopulationKind.StaffCategory ? target : null,
        departmentId: kind == ShiftPopulationKind.Department ? target : null);
    Check(row.PopulationKind == kind && row.EffectiveFrom == from && row.EffectiveTo == until,
        $"{kind} model retains its explicit target and date interval");
}
Reject<ArgumentException>(() => new ShiftPopulationAssignment(shiftId, ShiftPopulationKind.Site, from, until,
    approvalId, teamId: Guid.NewGuid()), "mismatched population target is rejected");
Reject<ArgumentException>(() => new ShiftPopulationAssignment(shiftId, ShiftPopulationKind.Site, from, until,
    approvalId, siteId: siteId, teamId: Guid.NewGuid()), "multiple population targets are rejected");
Reject<ArgumentException>(() => new ShiftPopulationAssignment(shiftId, ShiftPopulationKind.Site, from, from,
    approvalId, siteId: siteId), "zero-length population assignment is rejected");
Reject<ArgumentException>(() => new ShiftPopulationAssignment(shiftId, ShiftPopulationKind.Site, from, until,
    Guid.Empty, siteId: siteId), "population assignment requires an approval identity");
var original = Population();
var end = new ShiftPopulationAssignmentEnd(original, from.AddDays(10), "Approved future ending", approvalId, "reviewer", now);
Check(end.EffectiveTo == from.AddDays(10) && original.EffectiveTo == until,
    "appending an ending preserves the original population interval");
Reject<ArgumentException>(() => new ShiftPopulationAssignmentEnd(original, until.AddDays(1), "Reason", approvalId, "reviewer", now),
    "population ending cannot extend the original interval");
Reject<ArgumentException>(() => new ShiftPopulationAssignmentEnd(original, from, "Reason", approvalId, "reviewer", now),
    "population ending cannot erase its entire original interval");
var sameVersion = Guid.NewGuid();
Reject<ArgumentException>(() => new TemporaryShiftChange(siteId, from, sameVersion, sameVersion, approvalId,
    "reviewer", now, "evidence", "reason"), "temporary change rejects identical original/replacement versions");
Reject<ArgumentException>(() => new TemporaryShiftChange(siteId, from, Guid.NewGuid(), Guid.NewGuid(), approvalId,
    "reviewer", now, " ", "reason"), "temporary change requires written evidence");

var exactHoliday = new HolidayCalendar("SYNTHETIC-EXACT", "Synthetic calendar", from, until, "evidence");
var revisedHoliday = exactHoliday.CreateRevision("Synthetic calendar revision", "evidence", true);
var direct = ShiftPopulationAssignment.CreateDirect(shiftId, ShiftPopulationKind.Site, from, until,
    siteId: siteId, holidayCalendarId: exactHoliday.Id);
Check(direct.PopulationKind == ShiftPopulationKind.Site && direct.SiteId == siteId && direct.ApprovalRequestId is null,
    "direct site assignment does not fabricate an approval request");
Check(direct.HolidayCalendarId == exactHoliday.Id && direct.HolidayCalendarId != revisedHoliday.Id,
    "direct site assignment pins the explicit original calendar revision, not its successor");
Check(ShiftPopulationAssignment.CreateDirect(shiftId, ShiftPopulationKind.Site, from, until, siteId: siteId).HolidayCalendarId is null,
    "direct site assignment may explicitly leave holiday context unconfigured");
foreach (var kind in new[] { ShiftPopulationKind.Team, ShiftPopulationKind.Department, ShiftPopulationKind.StaffCategory })
    Reject<ArgumentException>(() => ShiftPopulationAssignment.CreateDirect(shiftId, kind, from, until, siteId: siteId),
        $"direct {kind} assignment is rejected by the confirmed site-only factory");
Reject<ArgumentException>(() => ShiftPopulationAssignment.CreateDirect(shiftId, ShiftPopulationKind.Site, from, until),
    "direct site assignment requires its explicit site");
Reject<ArgumentException>(() => ShiftPopulationAssignment.CreateDirect(shiftId, ShiftPopulationKind.Site, from, until,
    siteId: siteId, teamId: Guid.NewGuid()), "direct site assignment cannot include a second population");
Reject<ArgumentException>(() => ShiftPopulationAssignment.CreateDirect(shiftId, ShiftPopulationKind.Site, from, until,
    siteId: siteId, holidayCalendarId: Guid.Empty), "direct site calendar identity cannot be empty");
var directEnd = ShiftPopulationAssignmentEnd.CreateDirect(direct, from.AddDays(3), "Direct reason", "direct-actor", now);
Check(directEnd.ApprovalRequestId is null && directEnd.RecordedByUserId == "direct-actor" && directEnd.RecordedAt == now &&
    directEnd.ShiftPopulationAssignmentId == direct.Id && direct.EffectiveTo == until,
    "direct site ending records actor/time and original assignment without an approval request");
var teamAssignment = new ShiftPopulationAssignment(shiftId, ShiftPopulationKind.Team, from, until, approvalId, teamId: Guid.NewGuid());
Reject<ArgumentException>(() => ShiftPopulationAssignmentEnd.CreateDirect(teamAssignment, from.AddDays(3), "Reason", "direct-actor", now),
    "direct ending cannot be used for a legacy non-site population");
Reject<ArgumentException>(() => new ShiftPopulationAssignmentEnd(original, from.AddDays(3), "Reason", Guid.Empty, "reviewer", now),
    "legacy approved ending still rejects an empty approval identity");
Reject<ArgumentException>(() => new TemporaryShiftChange(siteId, from, Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
    "reviewer", now, "evidence", "reason"), "legacy approved temporary change still rejects an empty approval identity");
var directTemporary = TemporaryShiftChange.CreateDirect(siteId, from, Guid.NewGuid(), Guid.NewGuid(),
    "direct-actor", now, "written-direct-evidence", "Direct site change");
Reject<ArgumentException>(() => new TemporaryShiftChangeAmendment(directTemporary, directTemporary,
    "actor", now, "evidence", "reason"), "temporary amendment cannot supersede itself");
foreach (var invalidSuccessor in new[]
{
    TemporaryShiftChange.CreateDirect(Guid.NewGuid(), from, Guid.NewGuid(), Guid.NewGuid(), "actor", now, "evidence", "reason"),
    TemporaryShiftChange.CreateDirect(siteId, from.AddDays(1), Guid.NewGuid(), Guid.NewGuid(), "actor", now, "evidence", "reason"),
    Temporary()
})
    Reject<ArgumentException>(() => new TemporaryShiftChangeAmendment(directTemporary, invalidSuccessor,
        "actor", now, "evidence", "reason"), "temporary amendment rejects another site/date or legacy approved successor");
Reject<ArgumentException>(() => new TemporaryShiftChangeAmendment(Temporary(), null,
    "actor", now, "evidence", "reason"), "temporary amendment cannot silently reinterpret legacy approval");
Check(directTemporary.ApprovalRequestId is null && directTemporary.ApprovedBy == "direct-actor" && directTemporary.ApprovedAtUtc == now &&
    directTemporary.WrittenEvidenceReference == "written-direct-evidence" && directTemporary.Reason == "Direct site change",
    "direct temporary change retains recording actor/time/evidence without claiming separate approval");
Reject<ArgumentException>(() => TemporaryShiftChange.CreateDirect(siteId, from, Guid.NewGuid(), Guid.NewGuid(),
    " ", now, "evidence", "reason"), "direct temporary change still requires a recording actor");
Reject<ArgumentException>(() => TemporaryShiftChange.CreateDirect(siteId, from, Guid.NewGuid(), Guid.NewGuid(),
    "direct-actor", now, " ", "reason"), "direct temporary change still requires written evidence");
Check(Population().ApprovalRequestId == approvalId && Temporary().ApprovalRequestId == approvalId &&
    end.ApprovalRequestId == approvalId, "legacy approved population, ending and temporary paths retain their original approval identities");

foreach (var state in new[] { ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected })
{
    var selfRequest = Request();
    Reject<InvalidOperationException>(() => selfRequest.Decide(state, "requester", "AnySyntheticRole", "reason", now.AddMinutes(1)),
        $"requester cannot self-decide {state}");
    Check(selfRequest.State == ApprovalState.Pending && selfRequest.ClosedAt is null,
        $"rejected self-{state} leaves the request pending");
    var request = Request();
    var decision = request.Decide(state, "reviewer", "AnySyntheticRole", "reason", now.AddMinutes(1));
    Check(request.State == state && request.ClosedAt == decision.DecidedAt &&
        decision.ApprovalRequestId == request.Id && decision.ActorUserId == "reviewer",
        $"generic {state} retains request/actor/closure evidence, without inferring role authority");
    Reject<InvalidOperationException>(() => request.Decide(ApprovalState.Approved, "another-reviewer", "AnySyntheticRole", null, now.AddMinutes(2)),
        $"closed {state} request cannot be decided again");
    Reject<InvalidOperationException>(() => request.Cancel("requester", now.AddMinutes(2)),
        $"closed {state} request cannot be cancelled");
}
foreach (var state in new[] { ApprovalState.Returned, ApprovalState.Rejected })
    Reject<ArgumentException>(() => Request().Decide(state, "reviewer", "AnySyntheticRole", " ", now.AddMinutes(1)),
        $"{state} requires a reason");
Reject<ArgumentException>(() => Request().Decide(ApprovalState.Approved, "reviewer", "AnySyntheticRole", null, now.AddMinutes(-1)),
    "decision cannot precede its request");
Reject<ArgumentOutOfRangeException>(() => Request().Decide(ApprovalState.Cancelled, "reviewer", "AnySyntheticRole", null, now.AddMinutes(1)),
    "cancellation cannot be forged as an approval decision");
var cancelled = Request();
Reject<InvalidOperationException>(() => cancelled.Cancel("other-user", now.AddMinutes(1)),
    "only requester can cancel a pending request");
Reject<InvalidOperationException>(() => cancelled.Cancel("requester", now.AddMinutes(-1)),
    "request cancellation cannot precede submission");
cancelled.Cancel("requester", now.AddMinutes(1));
Check(cancelled.State == ApprovalState.Cancelled && cancelled.ClosedAt == now.AddMinutes(1),
    "requester cancellation records terminal state and time");
Reject<InvalidOperationException>(() => cancelled.Decide(ApprovalState.Approved, "reviewer", "AnySyntheticRole", null, now.AddMinutes(2)),
    "cancelled request cannot later be approved");

// All tracked append-only records reject edits/deletes at every SaveChanges overload,
// including with automatic change detection disabled. No connection may be attempted.
var factories = new (Func<object> Create, string Property, Func<object> Changed)[]
{
    (() => Population(), nameof(ShiftPopulationAssignment.ShiftId), () => Guid.NewGuid()),
    (() => new ShiftPopulationAssignmentEnd(Population(), from.AddDays(10), "Reason", approvalId, "reviewer", now),
        nameof(ShiftPopulationAssignmentEnd.Reason), () => "Changed reason"),
    (() => Temporary(), nameof(TemporaryShiftChange.Reason), () => "Changed reason"),
    (() => new TemporaryShiftChangeAmendment(directTemporary, null, "actor", now, "evidence", "reason"),
        nameof(TemporaryShiftChangeAmendment.Reason), () => "Changed reason"),
    (() => new TemporaryShiftChangeEmployee(Guid.NewGuid(), employeeId),
        nameof(TemporaryShiftChangeEmployee.EmployeeId), () => Guid.NewGuid()),
    (() => Request().Decide(ApprovalState.Approved, "reviewer", "AnySyntheticRole", "Reason", now.AddMinutes(1)),
        nameof(ApprovalDecision.Reason), () => "Changed reason")
};
foreach (var item in factories)
foreach (var delete in new[] { false, true })
foreach (var asynchronous in new[] { false, true })
foreach (var explicitAccept in new[] { false, true })
{
    db.ChangeTracker.Clear();
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    var record = item.Create();
    var entry = db.Attach(record);
    if (delete) db.Remove(record);
    else entry.Property(item.Property).CurrentValue = item.Changed();
    var rejected = false;
    try
    {
        if (asynchronous)
        {
            if (explicitAccept) await db.SaveChangesAsync(acceptAllChangesOnSuccess: false);
            else await db.SaveChangesAsync();
        }
        else if (explicitAccept) db.SaveChanges(acceptAllChangesOnSuccess: false);
        else db.SaveChanges();
    }
    catch (InvalidOperationException error) when (error.Message.Contains("append-only", StringComparison.Ordinal) &&
        error.Message.Contains(record.GetType().Name, StringComparison.Ordinal)) { rejected = true; }
    Check(rejected, $"{record.GetType().Name} {(delete ? "delete" : "edit")} rejected by " +
        $"{(asynchronous ? "async" : "sync")} save{(explicitAccept ? "(false)" : "()")}");
}
foreach (var asynchronous in new[] { false, true })
foreach (var explicitAccept in new[] { false, true })
{
    db.ChangeTracker.Clear();
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    var savedChange = Temporary();
    db.Attach(savedChange);
    db.Add(new TemporaryShiftChangeEmployee(savedChange.Id, employeeId));
    var rejected = false;
    try
    {
        if (asynchronous)
        {
            if (explicitAccept) await db.SaveChangesAsync(acceptAllChangesOnSuccess: false);
            else await db.SaveChangesAsync();
        }
        else if (explicitAccept) db.SaveChanges(acceptAllChangesOnSuccess: false);
        else db.SaveChanges();
    }
    catch (InvalidOperationException error) when (error.Message.Contains("Temporary shift employees must be saved", StringComparison.Ordinal))
    { rejected = true; }
    Check(rejected, $"late temporary manifest addition rejected by {(asynchronous ? "async" : "sync")} save{(explicitAccept ? "(false)" : "()")}");
}
db.ChangeTracker.Clear();
db.ChangeTracker.AutoDetectChangesEnabled = true;

// Translation of representative bounded query shapes using the real SQL Server provider.
// ToQueryString does not execute SQL or prove deployed repository behavior.
var date = from.AddDays(2);
var candidates = db.ShiftPopulationAssignments.AsNoTracking().Where(group =>
    group.EffectiveFrom <= date && (group.EffectiveTo == null || date < group.EffectiveTo) &&
    !db.ShiftPopulationAssignmentEnds.Any(stop => stop.ShiftPopulationAssignmentId == group.Id && stop.EffectiveTo <= date) &&
    ((group.PopulationKind == ShiftPopulationKind.Team && db.Set<TeamMember>().Any(team =>
        team.EmployeeId == employeeId && team.TeamId == group.TeamId && team.EffectiveFrom <= date &&
        (team.EffectiveTo == null || date < team.EffectiveTo) &&
        (team.TerminatedEffectiveTo == null || date < team.TerminatedEffectiveTo))) ||
     db.EmployeeAssignments.Any(job => job.EmployeeId == employeeId && job.EffectiveFrom <= date &&
        (job.EffectiveTo == null || date < job.EffectiveTo) &&
        ((group.PopulationKind == ShiftPopulationKind.Site && job.SiteId == group.SiteId) ||
         (group.PopulationKind == ShiftPopulationKind.Department && job.DepartmentId == group.DepartmentId) ||
         (group.PopulationKind == ShiftPopulationKind.StaffCategory && job.StaffCategoryId == group.StaffCategoryId)))))
    .OrderBy(x => x.Id).Take(2049).ToQueryString();
Check(candidates.Contains("ShiftPopulationAssignments", StringComparison.Ordinal) &&
    candidates.Contains("ShiftPopulationAssignmentEnds", StringComparison.Ordinal) &&
    candidates.Contains("TeamMembers", StringComparison.Ordinal) && candidates.Contains("TerminatedEffectiveTo", StringComparison.Ordinal) &&
    candidates.Contains("EmployeeAssignments", StringComparison.Ordinal) && candidates.Contains("NOT EXISTS", StringComparison.Ordinal),
    "SQL Server translates dated population, withdrawal and historical membership query shape");
var temporaryQuery = (from affected in db.TemporaryShiftChangeEmployees.AsNoTracking()
                      join approval in db.ApprovalRequests on affected.Change.ApprovalRequestId equals approval.Id
                      where affected.EmployeeId == employeeId && affected.Change.AttendanceDate == date &&
                          approval.State == ApprovalState.Approved && approval.ClosedAt != null &&
                          db.ApprovalDecisions.Any(decision => decision.ApprovalRequestId == approval.Id &&
                              decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt &&
                              decision.ActorUserId != approval.RequesterUserId)
                      orderby affected.Id
                      select new { affected.Change.Id, affected.Change.OriginalShiftVersionId,
                          affected.Change.ReplacementShiftVersionId, ApprovalId = approval.Id }).Take(2049).ToQueryString();
Check(temporaryQuery.Contains("TemporaryShiftChangeEmployees", StringComparison.Ordinal) &&
    temporaryQuery.Contains("ApprovalRequests", StringComparison.Ordinal) &&
    temporaryQuery.Contains("ApprovalDecisions", StringComparison.Ordinal),
    "SQL Server translates temporary employee/approval evidence query shape");
var exactCalendar = Guid.NewGuid();
var calendarQuery = db.HolidayCalendars.AsNoTracking().Where(x => x.Id == exactCalendar &&
        x.EffectiveFrom <= date && (x.EffectiveTo == null || date < x.EffectiveTo))
    .Select(x => new { x.Id, x.Code, x.Revision, x.IsActive,
        HolidayIds = x.Holidays.Where(h => h.HolidayDate == date).Select(h => h.Id).ToArray() }).ToQueryString();
Check(calendarQuery.Contains("HolidayCalendars", StringComparison.Ordinal) && calendarQuery.Contains("Holidays", StringComparison.Ordinal) &&
    calendarQuery.Contains("Revision", StringComparison.Ordinal),
    "SQL Server translates exact calendar revision/date query without inventing applicability");
Check(connections.Attempts == 0 && db.Database.GetDbConnection().State == ConnectionState.Closed,
    "zero database connection attempts; all storage stayed untouched");
Console.WriteLine($"Verified {checks} offline model, guard, approval-domain and query-translation checks. " +
    "No SQL execution, production authentication, approval authority, encrypted storage or complete workflow is claimed.");

sealed class RejectConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this offline harness.");
    }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new InvalidOperationException("Database connection attempts are prohibited in this offline harness.");
    }
}
