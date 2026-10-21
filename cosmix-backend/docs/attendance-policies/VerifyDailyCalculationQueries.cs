#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application;
using Cosmix.Infrastructure;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repo = new AttendanceCalculationRepository(db, null!);
IQueryable<T> Query<T>(string name, params object?[] args) => (IQueryable<T>)typeof(AttendanceCalculationRepository)
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, args)!;
var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 12); var checks = 0;
void Check(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
Check(typeof(Cosmix.Application.Attendance.Calculation.IAttendanceAlternativeCorrectionApprovalReader).IsAssignableFrom(typeof(AttendanceCalculationRepository)),
    "selected production repository must implement the schema5 authority reader");
var punch = Query<AttendanceCalculationPunch>("PunchQuery", employee, new Guid[] { Guid.NewGuid(), Guid.NewGuid() }).ToQueryString();
Check(punch.Contains("EmployeeId") && punch.Contains(" IN "), "Raw IDs constrained by employee and exact bounded ID set");
Check(punch.Contains("EmployeeAssignmentId") && punch.Contains("BusinessDate") && punch.Contains("ShiftVersionId"), "Raw business-date/membership/shift proof projected");
Check(punch.Contains("TemporaryShiftChangeId") && punch.Contains("HolidayCalendarId"), "Raw pinned scheduling evidence retained");
Check(punch.Contains("BranchId") && punch.Contains("OfficeRosterAssignmentId") && punch.Contains("OfficeGeofenceVersionId"),
    "Office raw history projects exact branch/roster/boundary, not an inferred Site");
Check(!punch.Contains("Latitude") && !punch.Contains("DeviceCapturedAtUtc"), "Calculator projection does not fetch private location/device evidence");
Check(punch.Contains("LEFT JOIN") && punch.Contains("NOT EXISTS") && punch.Contains("Revision"), "Latest assessment bound without hiding missing or duplicate proof");
var ot = Query<AttendanceCalculationOvertimeReference>("OvertimeQuery", employee, date).ToQueryString();
Check(ot.Contains("TOP") && ot.Contains("513") && ot.Contains("ORDER BY"), "Daily OT query has deterministic overflow bound");
Check(ot.Contains("WorkSessions") && ot.Contains("AttendanceDate") && ot.Contains("EmployeeId"), "OT requests bind retained business date and employee");
Check(ot.Contains("COUNT(*)") && ot.Contains("ApprovalDecisions"), "Exact decision cardinality is verified");
Check(!ot.Contains("[State] =") && !ot.Contains("[RequestType] ="), "Malformed approval metadata cannot hide OT from validation");
var authorityRepo = new AlternativeCorrectionApprovalRepository(db);
IQueryable<T> AuthorityQuery<T>(string name, params object?[] args) => (IQueryable<T>)typeof(AlternativeCorrectionApprovalRepository)
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(authorityRepo, args)!;
var sourceSql = AuthorityQuery<AlternativeCorrectionApprovalSource>("SourceQuery", employee, Guid.NewGuid(), Guid.NewGuid()).Take(2).ToQueryString();
Check(sourceSql.Contains("AttendanceCorrections") && sourceSql.Contains("ApprovalRequests") && sourceSql.Contains("ApprovalDecisions") && sourceSql.Contains("WorkSessions"),
    "actual authority source joins existing correction, approval, decision and session rows without new EF fields");
Check(sourceSql.Contains("COUNT(*)") && sourceSql.Contains("CorrectionId") && sourceSql.Contains("AttendanceDate") && sourceSql.Contains("EmployeeId"),
    "source query pins employee/date/correction and rejects duplicate decisions");
Check(sourceSql.Contains("TOP") && sourceSql.Contains("2"), "duplicate initial manual sessions are bounded, not silently selected");
foreach (var query in new[] { "AuthorityQuery", "DecisionQuery" }) {
    var sql = AuthorityQuery<Cosmix.Domain.Common.Audit.AuditEvent>(query, employee, Guid.NewGuid()).ToQueryString();
    Check(sql.Contains("AuditEvents") && sql.Contains("EmployeeScopeId") && sql.Contains("TargetId") && sql.Contains("TargetType") && sql.Contains("TOP"),
        "exact bounded existing-table audit query: " + query);
}
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["ConnectionStrings:CosmixDatabase"] = "Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true",
    ["MobileAuthentication:Enabled"] = "false", ["WebAuthentication:Enabled"] = "false"
}).Build();
var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
services.AddApplication(); services.AddInfrastructure(configuration);
services.AddScoped<ICurrentAuthentication, NoAuthentication>(); services.AddSingleton<IAttendanceCursorProtector, NoCursor>();
services.AddSingleton<IAccessTokenIssuer, NoTokens>();
services.Replace(ServiceDescriptor.Singleton<IPrivateFileStore, NoFiles>());
services.Replace(ServiceDescriptor.Scoped<CosmixDbContext>(_ => new(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").AddInterceptors(guard).Options)));
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope(); using var otherScope = provider.CreateScope();
var selected = scope.ServiceProvider.GetRequiredService<IAttendanceCalculationRepository>();
var proofReader = scope.ServiceProvider.GetRequiredService<IAttendanceAlternativeCorrectionApprovalReader>();
Check(selected is AttendanceCalculationRepository && ReferenceEquals(selected, proofReader), "actual AddApplication/AddInfrastructure proof-reader alias is the selected repository, not a fallback");
var journal = scope.ServiceProvider.GetRequiredService<AlternativeCorrectionApprovalJournal>();
var review = scope.ServiceProvider.GetRequiredService<CorrectionReviewService>();
object JournalField(object instance) => instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
    .Single(x => x.FieldType == typeof(AlternativeCorrectionApprovalJournal)).GetValue(instance)!;
Check(ReferenceEquals(JournalField(selected), journal) && ReferenceEquals(JournalField(review), journal),
    "real review writer and selected repository reader share the SAME scoped trusted journal");
Check(!ReferenceEquals(journal, otherScope.ServiceProvider.GetRequiredService<AlternativeCorrectionApprovalJournal>()),
    "trusted stages cannot leak across DI scopes");
Check(scope.ServiceProvider.GetRequiredService<IAlternativeCorrectionApprovalRepository>() is AlternativeCorrectionApprovalRepository &&
    authorityRepo.TransactionId is null, "real authority projection is registered and no fake transaction is assumed");
Check(await proofReader.ReadAsync(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), default) is null,
    "selected repository delegates malformed identity to real fail-closed reader without opening SQL");
Check(guard.Attempts == 0, "Translation only; no SQL connection attempted");
Console.WriteLine($"PASS: {checks} daily calculator production SQL translation checks; zero SQL connections.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData d, InterceptionResult r)
    { Attempts++; throw new InvalidOperationException("No database access permitted."); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData d, InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("No database access permitted."); }
}

sealed class NoAuthentication : ICurrentAuthentication { public AuthenticatedAccount Require() => throw new Exception("No authenticated action permitted."); }
sealed class NoTokens : IAccessTokenIssuer {
    public bool IsConfigured => false;
    public string Issue(Cosmix.Domain.Authentication.UserAccount account, Cosmix.Domain.Authentication.UserSession session, DateTimeOffset now, DateTimeOffset expiresAt)
        => throw new Exception("No token issuance permitted.");
}
sealed class NoFiles : IPrivateFileStore {
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct) => throw new Exception("No file IO permitted.");
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => throw new Exception("No file IO permitted.");
}
sealed class NoCursor : IAttendanceCursorProtector {
    public string Protect(Guid accountId, string purpose, string fingerprint, AttendanceCursorPosition position) => throw new Exception("No cursor IO permitted.");
    public AttendanceCursorPosition Unprotect(string value, Guid accountId, string purpose, string fingerprint) => throw new Exception("No cursor IO permitted.");
}
