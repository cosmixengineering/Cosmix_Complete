#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Standalone fault injection around real SQL repositories. No production hook, fake session or permission grant.
// Requires an existing successfully authenticated synthetic Admin session and explicit HTTP scheduling grants.
const string root = @"C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009";
if (!args.SequenceEqual(new[] { "--execute" })) { Console.WriteLine("Use --execute only after approved fixture setup and authenticated access acceptance."); return; }
PrivateBootstrapSnapshotStore.RequirePrivatePath(root, true);
using var settings = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "settings.json")));
var config = new ConfigurationBuilder().AddInMemoryCollection(Flatten(settings.RootElement)).Build();
var target = new SqlConnectionStringBuilder(config.GetConnectionString("CosmixDatabase"));
if (target.DataSource != @"(localdb)\MSSQLLocalDB" || target.InitialCatalog != "Cosmix_Testing" || !target.IntegratedSecurity ||
    !config.GetValue<bool>("LocalTesting:Enabled") || config["LocalTesting:PrivateDirectory"] != root)
    throw new InvalidOperationException("Unexpected acceptance target.");
var services = new ServiceCollection();
services.AddLogging(); services.AddApplication(); services.AddInfrastructure(config);
services.Configure<JwtOptions>(config.GetSection("Jwt"));
services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
using var provider = services.BuildServiceProvider();
var checks = 0;
var retained = new List<PrivateFileReference>();
for (var stage = 0; stage < 2; stage++)
{
    using var scope = provider.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<CosmixDbContext>();
    if ((await db.UserAccounts.CountAsync()) != 3 || (await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Unexpected account count or schema.");
    var account = await db.UserAccounts.AsNoTracking().SingleAsync(a => a.LoginName == "admin@example.invalid" && a.Employee.FullLegalName == "TEST ONLY Admin");
    var now = DateTimeOffset.UtcNow;
    var session = await db.UserSessions.AsNoTracking().Where(s => s.UserAccountId == account.Id &&
        s.SecurityVersion == account.SecurityVersion && s.Client == SessionClient.Web && s.RevokedAt == null && s.ExpiresAt > now)
        .OrderByDescending(s => s.CreatedAt).FirstAsync();
    var identity = await sp.GetRequiredService<WebAuthenticationService>().ValidateSessionAsync(account.Id, session.Id, account.SecurityVersion, default)
        ?? throw new InvalidOperationException("A real current MFA session is required.");
    var files = new FaultFiles(sp.GetRequiredService<IPrivateFileStore>(), failWrites: stage == 0, retained);
    var journal = new FaultJournal(sp.GetRequiredService<ISchedulingJournalRepository>(), failAfterSave: stage == 1);
    var operation = new SchedulingOperation(new Current(identity), sp.GetRequiredService<IAccountAccessReader>(),
        sp.GetRequiredService<IAccessManagementRepository>(), journal, TimeProvider.System, files);
    var key = "rollback-" + Guid.NewGuid().ToString("N");
    var code = "RB" + Guid.NewGuid().ToString("N")[..20];
    var operationName = "acceptance.rollback." + stage;
    var entered = false;
    try
    {
        await operation.WriteAsync(SchedulingPermissions.ShiftsManage, operationName, null, key,
            new { code }, "Synthetic rollback verification", new("127.0.0.1", key),
            async (_, ct) =>
            {
                var shift = new Shift(code, "SYNTHETIC ROLLBACK ONLY");
                db.Shifts.Add(shift);
                await db.SaveChangesAsync(ct);
                entered = true;
                return new SchedulingChange<Guid>(shift.Id, null, shift.Id);
            }, default);
        throw new InvalidOperationException("Injected failure was not observed.");
    }
    catch (InjectedFailure) { }
    if (!entered) throw new InvalidOperationException("Actual authorization/business save was not reached.");
    checks++;
    // A separate context/connection verifies rollback after the actual transaction disposed.
    await using var read = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(target.ConnectionString).Options);
    Check(!await read.Shifts.AnyAsync(s => s.Code == code), "Business row survived rollback");
    Check(!await read.IdempotencyReceipts.AnyAsync(r => r.Key == key), "Receipt survived rollback");
    Check(!await read.AuditEvents.AnyAsync(a => a.CorrelationId == key), "Audit survived rollback");
}
Check(retained.Count == 2, "Expected result and audit objects before post-save failure");
var evidencePath = Path.Combine(root, "rollback-orphans-" + Guid.NewGuid().ToString("N") + ".json");
using (var file = new FileStream(evidencePath, FileMode.CreateNew)) JsonSerializer.Serialize(file, retained);
Console.WriteLine($"SQL transaction rollback: {checks} checks passed (failure before protected writes and after persisted journal save). {retained.Count} encrypted orphan objects retained and privately indexed; no cross-store deletion claimed.");
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
static IEnumerable<KeyValuePair<string, string?>> Flatten(JsonElement element, string prefix = "")
{
    if (element.ValueKind == JsonValueKind.Object)
        foreach (var property in element.EnumerateObject())
            foreach (var value in Flatten(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name)) yield return value;
    else if (element.ValueKind == JsonValueKind.Array)
    {
        var index = 0;
        foreach (var item in element.EnumerateArray())
            foreach (var value in Flatten(item, prefix + ":" + index++)) yield return value;
    }
    else yield return new(prefix, element.ValueKind == JsonValueKind.Null ? null : element.ToString());
}

sealed class Current(AuthenticatedAccount identity) : ICurrentAuthentication { public AuthenticatedAccount Require() => identity; }
sealed class InjectedFailure : Exception { }
sealed class FaultFiles(IPrivateFileStore real, bool failWrites, List<PrivateFileReference> retained) : IPrivateFileStore
{
    public async Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
    {
        if (failWrites) throw new InjectedFailure();
        var reference = await real.WriteAsync(bytes, purpose, ct); retained.Add(reference); return reference;
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => real.ReadAsync(reference, purpose, ct);
}
sealed class FaultJournal(ISchedulingJournalRepository real, bool failAfterSave) : ISchedulingJournalRepository
{
    public Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct) => real.IsCurrentSessionAsync(actor, ct);
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct) => real.FindReceiptAsync(actor, operation, key, ct);
    public void AddAudit(AuditEvent audit) => real.AddAudit(audit);
    public void AddReceipt(IdempotencyReceipt receipt) => real.AddReceipt(receipt);
    public async Task SaveChangesAsync(CancellationToken ct) { await real.SaveChangesAsync(ct); if (failAfterSave) throw new InjectedFailure(); }
}
