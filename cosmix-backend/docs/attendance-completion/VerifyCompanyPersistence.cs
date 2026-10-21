#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

var checks = 0; var guard = new NoCompanyConnections();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
CosmixDbContext Db() => new(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=company-model.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options);
void Validate(CosmixDbContext db) => typeof(CosmixDbContext).Assembly.GetType("Cosmix.Infrastructure.Database.CompanySettingsWriteGuard")!
    .GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [db]);
void Fails(Action action, string message)
{ try { action(); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; return; } throw new Exception(message); }
void SaveFails(CosmixDbContext db, string message)
{ try { db.SaveChanges(); } catch (InvalidOperationException e) when (e.Message.Contains(message, StringComparison.Ordinal)) { checks++; return; } throw new Exception("Actual context failed to guard " + message); }
CompanyProfile Profile() => new("Synthetic", "Configurable business", Guid.NewGuid(), Guid.NewGuid(), "Synthetic address", "PK-PB", null, null);
CompanySiteJurisdictionVersion SiteVersion(CompanyProfile p) => new(p.Id, Guid.NewGuid(), new(2027, 1, 1), "PK-SD", 2, null, new(2026, 10, 12, 5, 0, 0, TimeSpan.Zero));
using (var db = Db())
{
    var model = db.GetService<IDesignTimeModel>().Model;
    var profile = model.FindEntityType(typeof(CompanyProfile))!;
    Check(profile.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == nameof(CompanyProfile.SingletonKey)), "Unique singleton key prevents a second company profile");
    Check(profile.GetCheckConstraints().Any(x => x.Name == "CK_CompanyProfiles_Singleton"), "SQL singleton check binds the fixed identity and key");
    Check(profile.FindProperty(nameof(CompanyProfile.RowVersion))!.IsConcurrencyToken, "Profile updates use optimistic concurrency");
    Check(profile.GetForeignKeys().Count(x => x.DeleteBehavior == DeleteBehavior.Restrict) == 2, "Head-office Branch and City reuse restrictive existing master references");
    var version = model.FindEntityType(typeof(CompanySiteJurisdictionVersion))!;
    Check(version.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SiteId", "EffectiveFrom" }) && x.GetFilter() == "[SupersedesVersionId] IS NULL"), "Two initial mappings cannot claim the same site date");
    Check(version.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "SupersedesVersionId"), "A retained version has at most one explicit correction successor");
    Check(version.GetForeignKeys().Count(x => x.DeleteBehavior == DeleteBehavior.Restrict) == 3, "Company, Site and predecessor history remain restrictive references");
    var p = Profile(); db.Add(p); Validate(db); checks++;
}
using (var db = Db()) { var p = Profile(); db.Attach(p); db.Remove(p); Fails(() => Validate(db), "Singleton deletion must fail"); }
using (var db = Db())
{
    var p = Profile(); db.Attach(p); typeof(CompanyProfile).GetProperty(nameof(CompanyProfile.CompanyName))!.SetValue(p, "Unversioned mutation");
    Fails(() => Validate(db), "Profile changes without optimistic revision must fail");
}
using (var db = Db())
{
    var p = Profile(); db.Attach(p); p.Update("Changed", "Changed type", p.HeadOfficeBranchId, p.HeadOfficeCityId, p.Address, p.JurisdictionCode, null, null);
    Validate(db); checks++;
}
using (var db = Db()) { var p = Profile(); db.Attach(p); db.Add(SiteVersion(p)); Fails(() => Validate(db), "Jurisdiction append without matching company revision must fail"); }
using (var db = Db())
{
    var p = Profile(); db.Attach(p); p.AdvanceRevision(); db.Add(SiteVersion(p)); Validate(db); checks++;
}
using (var db = Db())
{
    var v = SiteVersion(Profile()); db.Attach(v); typeof(CompanySiteJurisdictionVersion).GetProperty(nameof(CompanySiteJurisdictionVersion.JurisdictionCode))!.SetValue(v, "PK-KP");
    Fails(() => Validate(db), "Historical province mutation must fail");
}
using (var db = Db()) { var v = SiteVersion(Profile()); db.Attach(v); db.Remove(v); Fails(() => Validate(db), "Historical jurisdiction deletion must fail"); }
using (var db = Db())
{
    var v = SiteVersion(Profile()); db.Attach(v); typeof(CompanySiteJurisdictionVersion).GetProperty(nameof(CompanySiteJurisdictionVersion.JurisdictionCode))!.SetValue(v, "PK-KP");
    SaveFails(db, "history is immutable");
}
using (var db = Db()) { var p = Profile(); db.Attach(p); db.Remove(p); SaveFails(db, "cannot be removed"); }
using (var db = Db())
{
    var repository = new CompanySettingsRepository(db); var site = Guid.Parse("73e949cf-bcba-4630-96c1-d4caeb6ca99e");
    var query = (IQueryable)typeof(CompanySettingsRepository).GetMethod("JurisdictionQuery", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(repository, [new[] { site }, new DateOnly(2027, 1, 1)])!;
    var sql = query.ToQueryString();
    Check(sql.Contains(site.ToString(), StringComparison.OrdinalIgnoreCase) && sql.Contains("[SiteId]"), "Actual EF query binds requested site IDs");
    Check(sql.Contains("[EffectiveFrom] <=") && sql.Contains("2027-01-01"), "Actual EF query bounds effective dates before materialization");
    Check(sql.Contains("TOP(") && sql.Contains("5001") && sql.Contains("ORDER BY"), "Actual EF query is deterministically bounded with overflow detection");
    Check(sql.Contains("[SupersedesVersionId]") && sql.Contains("[ConfigurationRevision]"), "Actual EF query retains correction provenance");
    Check(!sql.Contains("Compensation") && !sql.Contains("Employees"), "Jurisdiction reader does not load salary or employee data");
}
Check(guard.Attempts == 0, "No database connection was opened");
Console.WriteLine($"PASS: {checks} company model/write-guard/actual EF query checks; zero database connection attempts.");
public sealed class NoCompanyConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
