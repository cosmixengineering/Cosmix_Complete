using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Offline only: actual compiled CreateRows, SQL Server model and query compiler.
// No Open/SaveChanges/transaction/host; interceptors reject any accidental I/O.
var checks = 0;
var failures = new List<string>();
void Check(bool condition, string label)
{
    checks++;
    if (!condition) { failures.Add(label); Console.WriteLine("FAIL: " + label); }
}
var manifestPath = Path.Combine(Environment.CurrentDirectory, "docs", "hr-srs-remediation",
    "Outstation-O1-Fixture-Manifest-20261020.json");
using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
var manifest = manifestDocument.RootElement;
var source = manifest.GetProperty("sources").EnumerateArray().ToArray();
var ids = source.Select(x => x.GetProperty("id").GetGuid()).ToArray();
Check(source.Length == 9 && ids.Distinct().Count() == 9, "exact nine unique manifest identities");
Check(manifest.GetProperty("limits").GetProperty("sourceRows").GetInt32() == 9, "manifest source budget nine");
Check(manifest.GetProperty("actors").GetProperty("newGrants").GetInt32() == 0, "no new P1 grants");
var type = typeof(CurrentAuthentication).Assembly.GetType("Cosmix.Api.Development.HrOutstationO1Fixture", true)!;
var create = type.GetMethod("CreateRows", BindingFlags.Static | BindingFlags.NonPublic)!;
var seed = type.GetMethod("SeedAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
Check(seed.IsAssembly && seed.ReturnType == typeof(Task) &&
    seed.GetParameters().Select(x => x.ParameterType).SequenceEqual([typeof(CosmixDbContext), typeof(CancellationToken)]),
    "internal static Task SeedAsync(CosmixDbContext,CancellationToken)");
var connectionBlocker = new NoConnection();
var commandBlocker = new NoCommand();
var options = new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=offline.invalid;Database=Offline_O1;Integrated Security=true;Encrypt=true")
    .AddInterceptors(connectionBlocker, commandBlocker).Options;
var timestamps = new[] {
    DateTimeOffset.Parse("2026-10-20T12:34:56.1234567Z", CultureInfo.InvariantCulture),
    DateTimeOffset.Parse("2026-10-21T10:08:07.7654321Z", CultureInfo.InvariantCulture)
};
foreach (var timestamp in timestamps)
{
    using var db = new CosmixDbContext(options);
    var rows = (object[])create.Invoke(null, [db, timestamp])!;
    Check(rows.Length == 9, "actual CreateRows returns nine");
    Check(!db.ChangeTracker.Entries().Any(), "CreateRows leaves entities detached");
    for (var i = 0; i < rows.Length; i++)
    {
        var entry = db.Entry(rows[i]);
        Check(entry.State == EntityState.Detached, $"source {i} detached");
        Check(entry.Metadata.GetSchema() + "." + entry.Metadata.GetTableName() ==
            source[i].GetProperty("table").GetString(), $"source {i} mapped table");
        foreach (var field in source[i].EnumerateObject())
        {
            if (field.Name is "table" or "rulesJsonSource") continue;
            var property = rows[i].GetType().GetProperty(field.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            Check(property is not null, $"source {i} field {field.Name} exists");
            if (property is null) continue;
            var actual = property.GetValue(rows[i]);
            bool equal;
            if (field.Value.ValueKind == JsonValueKind.Null) equal = actual is null;
            else if (field.Value.ValueKind == JsonValueKind.String)
            {
                var expected = field.Value.GetString();
                equal = expected == "$seedUtc" ? actual is DateTimeOffset value && value == timestamp :
                    actual switch {
                        Guid guidValue => guidValue.ToString("D") == expected,
                        DateOnly dateValue => dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == expected,
                        _ => actual is string text && text == expected
                    };
            }
            else if (field.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                equal = actual is bool value && value == field.Value.GetBoolean();
            else equal = actual is not null && Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == field.Value.GetDecimal();
            Check(equal, $"source {i} exact {field.Name}");
        }
    }
    var version = (PolicyVersion)rows[7];
    var assignment = (PolicyAssignment)rows[8];
    var parsed = AllowancePolicyRulesCodec.ValidateAndCanonicalize(version.RulesJson);
    var expectedRules = AllowancePolicyRulesCodec.ValidateAndCanonicalize(manifest.GetProperty("allowanceRules").GetRawText());
    Check(parsed.IsValid && expectedRules.IsValid && parsed.CanonicalJson == expectedRules.CanonicalJson,
        "actual canonical rules exactly match approved manifest");
    Check(version.PublishedAt == timestamp && version.PublishedAt > new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
        "actual supplied seed timestamp, not historical publication");
    Check(version.Authority == PolicyAuthority.DirectHr && assignment.Authority == PolicyAuthority.DirectHr &&
        version.ApprovalRequestId is null && assignment.ApprovalRequestId is null,
        "synthetic DirectHr source without fabricated workflow approval");
    Check(assignment.EmployeeId == manifest.GetProperty("actors").GetProperty("requester").GetProperty("employeeId").GetGuid() &&
        assignment.RoleId is null && assignment.DepartmentId is null && assignment.SiteId is null &&
        assignment.StaffCategoryId is null, "employee-only outstation applicability");
    var request = manifest.GetProperty("http").GetProperty("create");
    var zone = TimeZoneInfo.CreateCustomTimeZone("Synthetic PKT", TimeSpan.FromHours(5), "Synthetic PKT", "Synthetic PKT");
    var valuation = DeploymentValuation.Calculate(request.GetProperty("startsAtUtc").GetDateTimeOffset(),
        request.GetProperty("endsAtUtc").GetDateTimeOffset(), [],
        new[] { new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 19) }
            .Select(d => new DeploymentValuationPolicyDay(d, version.Id, [assignment.Id], parsed.Rules!)).ToArray(), zone);
    Check(valuation.State == "ready", "real domain valuation ready");
    Check(valuation.DayUnits == 2m && valuation.Amount == 200m && valuation.Currency == "PKR",
        "exact two local calendar days PKR 200, not elapsed 40 hours");
    Check(valuation.Lines.Select(x => x.Date).SequenceEqual(
        new DateOnly?[] { new(2026, 10, 18), new(2026, 10, 19) }), "exact two historical valuation dates");
    db.AddRange(rows);
    Check(db.ChangeTracker.Entries().Count() == 9, "relationship fixup adds no unapproved entities");
    Check(db.ChangeTracker.Entries().All(x => x.State == EntityState.Added &&
        ids.Contains((Guid)x.Property("Id").CurrentValue!)), "exact approved Added identities");
    Check(ReferenceEquals(version.Policy, rows[6]) && ReferenceEquals(assignment.PolicyVersion, version),
        "actual EF relationship fixup preserves manifest policy chain");
    Check(db.Database.GetDbConnection().State == ConnectionState.Closed, "connection remains closed after domain composition");
}

// Translation-only mirrors of every query family in the compiled seed guards.
// This proves SQL Server translatability, not database contents or execution.
using (var db = new CosmixDbContext(options))
{
    void Translate(IQueryable query, string label, params string[] expectedSql)
    {
        try
        {
            var sql = query.ToQueryString();
            Check(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
                expectedSql.All(x => sql.Contains(x, StringComparison.OrdinalIgnoreCase)), label);
        }
        catch (Exception error) { Check(false, label + ": " + error.GetType().Name + " " + error.Message); }
    }
    Translate(db.EmployeeAssignments, "employment absence query", "[hr].[EmployeeAssignments]");
    Translate(db.Policies, "policy absence query", "[hr].[Policies]");
    Translate(db.PolicyVersions, "version absence query", "[hr].[PolicyVersions]");
    Translate(db.PolicyAssignments, "assignment absence query", "[hr].[PolicyAssignments]");
    Translate(db.PolicyAssignmentEnds, "assignment end absence query", "[hr].[PolicyAssignmentEnds]");
    Translate(db.Set<OutstationDeployment>(), "deployment absence query", "[hr].[OutstationDeployments]");
    Translate(db.Set<DeploymentHomeBreakRequest>(), "home break absence query", "SELECT");
    Translate(db.AllowanceClaims, "claim absence query", "SELECT");
    Translate(db.AllowanceClaimSubmissions, "submission absence query", "SELECT");
    Translate(db.AllowanceSettlementRecords, "settlement absence query", "SELECT");
    Translate(db.Set<Department>().Where(x => ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-")), "department collision predicate", "WHERE", "LIKE");
    Translate(db.Set<Designation>().Where(x => ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-")), "designation collision predicate", "WHERE", "LIKE");
    Translate(db.Set<StaffCategory>().Where(x => ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-")), "category collision predicate", "WHERE", "LIKE");
    Translate(db.Set<EmploymentType>().Where(x => ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-")), "type collision predicate", "WHERE", "LIKE");
    Translate(db.Set<Branch>().Where(x => ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-")), "branch collision predicate", "WHERE", "LIKE");
    var admin = manifest.GetProperty("actors").GetProperty("reviewer");
    var hr = manifest.GetProperty("actors").GetProperty("requester");
    var adminAccount = admin.GetProperty("accountId").GetGuid();
    var adminEmployee = admin.GetProperty("employeeId").GetGuid();
    var hrAccount = hr.GetProperty("accountId").GetGuid();
    var now = timestamps[0];
    Translate(db.UserAccounts.AsNoTracking().Include(x => x.Employee).Where(x => x.Id == hrAccount || x.Id == adminAccount),
        "bound account employee include", "JOIN", "WHERE");
    Translate(db.Set<UserRoleAssignment>().AsNoTracking().Where(x => x.UserAccountId == adminAccount &&
        x.EmployeeId == adminEmployee && x.Role.SystemRole == SystemRoleKind.Admin && x.Role.Code == "ADMIN" &&
        x.Role.IsActive && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo) &&
        (x.RevokedAt == null || now < x.RevokedAt)), "fixed genuine Admin interval predicate", "JOIN", "EffectiveTo", "RevokedAt");
    var code = "TEST-HR-P1-20261020-ADMIN";
    var syntheticId = ids[0];
    Translate(db.Set<Role>().AsNoTracking().Where(x => x.Code == code), "delegated role lookup", "WHERE");
    Translate(db.Set<UserRoleAssignment>().AsNoTracking().Where(x => x.RoleId == syntheticId), "delegated assignment lookup", "WHERE");
    Translate(db.Set<UserAccessScope>().AsNoTracking().Where(x => x.UserRoleAssignmentId == syntheticId), "scope lookup", "WHERE");
    Translate(db.Set<RolePermission>().AsNoTracking().Include(x => x.Permission).Where(x => x.RoleId == syntheticId),
        "grant permission include", "JOIN", "WHERE");
    Translate(db.PolicyVersions.AsNoTracking().Include(x => x.Policy), "policy readback include", "JOIN");
    Check(db.Database.GetDbConnection().State == ConnectionState.Closed, "all translation keeps connection closed");

    var key = "COSMIX_EXISTING_TESTING_HOST";
    var previous = Environment.GetEnvironmentVariable(key);
    try
    {
        foreach (var profile in new[] { "Not-O1", "HumaHrOutstation20261020O1" })
        {
            Environment.SetEnvironmentVariable(key, profile);
            try
            {
                await (Task)seed.Invoke(null, [db, CancellationToken.None])!;
                Check(false, profile + " must reject before I/O");
            }
            catch (InvalidOperationException) { Check(true, profile + " refuses before I/O"); }
            Check(!db.ChangeTracker.Entries().Any(), profile + " early rejection adds no rows");
        }
    }
    finally { Environment.SetEnvironmentVariable(key, previous); }
}
// Exercise the actual persistence guard, not merely domain construction.
var validateWrites = typeof(CosmixDbContext).GetMethod("ValidateTrackedWrites", BindingFlags.Instance | BindingFlags.NonPublic)!;
void ValidateWrites(CosmixDbContext db)
{
    try { validateWrites.Invoke(db, null); }
    catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
}
using (var db = new CosmixDbContext(options))
{
    db.AddRange((object[])create.Invoke(null, [db, timestamps[0]])!);
    try { ValidateWrites(db); Check(false, "nine-row legacy batch must reject missing tracked parent"); }
    catch (InvalidOperationException error)
    {
        Check(error.Message == "Load the employee aggregate before saving its profile records.",
            "reproduced actual missing-parent persistence rejection");
    }
}
var prepare = type.GetMethod("PrepareRows", BindingFlags.Static | BindingFlags.NonPublic);
Check(prepare is not null, "bounded tracked-parent preparer exists (RED on original build)");
if (prepare is not null)
{
    foreach (var pending in new[] { false, true })
    {
        using var db = new CosmixDbContext(options);
        var parent = new Employee(timestamps[0].AddYears(-1), "OFFLINE SYNTHETIC PARENT");
        var entry = db.Entry(parent);
        entry.Property(x => x.Id).CurrentValue = manifest.GetProperty("actors").GetProperty("requester").GetProperty("employeeId").GetGuid();
        entry.Property(x => x.Status).CurrentValue = EmployeeStatus.Active;
        entry.Property(x => x.OnboardingStatus).CurrentValue = pending ? OnboardingStatus.PendingApproval : OnboardingStatus.Approved;
        entry.Property(x => x.DraftRevision).CurrentValue = 5;
        entry.Property(x => x.JoinedOn).CurrentValue = new DateOnly(2026, 1, 15);
        entry.Property(x => x.RowVersion).CurrentValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        entry.State = EntityState.Unchanged;
        var original = entry.OriginalValues.Clone();
        try
        {
            prepare.Invoke(null, [db, parent, timestamps[0]]);
            ValidateWrites(db);
            Check(!pending, "pending parent cannot pass preparation and persistence");
            if (!pending)
            {
                Check(db.ChangeTracker.Entries().Count() == 10 &&
                    db.ChangeTracker.Entries().Count(x => x.State == EntityState.Added) == 9 &&
                    entry.State == EntityState.Modified, "actual persistence guard yields nine inserts and one parent update");
                Check(parent.DraftRevision == 6, "actual persistence guard increments parent exactly once from five to six");
                Check(entry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name)
                    .SequenceEqual(["DraftRevision"]), "only DraftRevision marked modified before SQL");
                foreach (var property in entry.Properties.Where(x => x.Metadata.Name != "DraftRevision"))
                    Check(property.Metadata.GetValueComparer()!.Equals(property.CurrentValue, original[property.Metadata.Name]),
                        "actual persistence preserves parent " + property.Metadata.Name);
            }
        }
        catch (TargetInvocationException error) when (pending && error.InnerException is InvalidOperationException)
        { Check(true, "pending parent denied by preparer"); }
        catch (InvalidOperationException) when (pending)
        { Check(true, "pending parent denied by persistence guard"); }
        if (pending) Check(parent.DraftRevision == 5, "pending denial never increments revision");
    }
    // Pending original and pending current states are independently denied by
    // the real persistence guard, even if the preparer is not called.
    foreach (var originallyPending in new[] { false, true })
    {
        using var db = new CosmixDbContext(options);
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        if (originallyPending) entry.Property(x => x.OnboardingStatus).OriginalValue = OnboardingStatus.PendingApproval;
        else entry.Property(x => x.OnboardingStatus).CurrentValue = OnboardingStatus.PendingApproval;
        db.AddRange((object[])create.Invoke(null, [db, timestamps[0]])!);
        try { ValidateWrites(db); Check(false, "actual persistence rejects pending current/original"); }
        catch (InvalidOperationException error)
        {
            Check(error.Message == "A submitted employee form cannot be changed while awaiting review.",
                "actual persistence pending rejection, originallyPending=" + originallyPending);
        }
        Check(parent.DraftRevision == 5, "persistence pending rejection leaves revision unchanged");
    }
    var preserve = type.GetMethod("RequireParentPreserved", BindingFlags.NonPublic | BindingFlags.Static)!;
    var savedWriteSet = type.GetMethod("RequireSavedWriteSet", BindingFlags.NonPublic | BindingFlags.Static);
    Check(savedWriteSet is not null, "actual post-save write-set guard is independently executable offline");
    foreach (var saved in new[] { false, true })
    {
        using var db = new CosmixDbContext(options);
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        var original = entry.OriginalValues.Clone();
        if (saved)
        {
            prepare.Invoke(null, [db, parent, timestamps[0]]);
            ValidateWrites(db);
        }
        var clientValue = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
        entry.Property(x => x.RowVersion).CurrentValue = clientValue;
        Check(parent.RowVersion.SequenceEqual(clientValue) &&
            entry.Property(x => x.RowVersion).CurrentValue.SequenceEqual(clientValue),
            "client-assigned rowversion precondition, saved=" + saved);
        try
        {
            preserve.Invoke(null, [db, parent, original, saved]);
            Check(false, "must reject client-assigned rowversion, saved=" + saved);
        }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
        { Check(true, "reject client-assigned rowversion, saved=" + saved); }
        if (saved && savedWriteSet is not null)
        {
            try
            {
                savedWriteSet.Invoke(null, [db, parent, original]);
                Check(false, "saved write-set must reject client-assigned rowversion");
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { Check(true, "saved write-set rejects client-assigned rowversion"); }
        }
    }
    using (var db = new CosmixDbContext(options))
    {
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        var original = entry.OriginalValues.Clone();
        prepare.Invoke(null, [db, parent, timestamps[0]]);
        ValidateWrites(db);
        var version = entry.Property(x => x.RowVersion);
        var generated = new byte[] { 2, 3, 4, 5, 6, 7, 8, 9 };
        // Emulate the provider returning a rowversion using EF's real
        // store-generated value mechanism, not a CLR setter or fake SaveChanges.
        // No database command is executed and no business evidence is created.
        entry.GetInfrastructure().SetStoreGeneratedValue(version.Metadata, generated);
        Check(version.CurrentValue.SequenceEqual(generated), "EF exposes generated rowversion through PropertyEntry.CurrentValue");
        Check(parent.RowVersion.SequenceEqual((byte[])original[nameof(Employee.RowVersion)]!),
            "EF retains original CLR rowversion before AcceptAllChanges");
        Check(version.IsModified && entry.State == EntityState.Modified,
            "provider-default generated rowversion is flagged modified despite unchanged CLR");
        try
        {
            preserve.Invoke(null, [db, parent, original, true]);
            Check(true, "saved:true accepts actual EF store-generated rowversion before acceptance");
        }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
        { Check(false, "saved:true rejects valid EF provider-default store-generated rowversion"); }
        if (savedWriteSet is not null)
        {
            try
            {
                savedWriteSet.Invoke(null, [db, parent, original]);
                Check(true, "actual saved write-set accepts nine inserts plus revision and provider-marked rowversion");
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { Check(false, "actual saved write-set rejects provider-marked rowversion"); }
        }
        Check(entry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name)
            .Order(StringComparer.Ordinal).SequenceEqual(new[] { "DraftRevision", "RowVersion" }),
            "provider-default post-save flags are exactly DraftRevision and RowVersion");
        var readback = new Employee(timestamps[0].AddYears(-1), "OFFLINE SYNTHETIC READBACK");
        var readbackEntry = db.Entry(readback);
        readbackEntry.CurrentValues.SetValues(entry.CurrentValues);
        try
        {
            preserve.Invoke(null, [db, readback, original, true]);
            Check(true, "saved:true accepts detached synthetic readback with generated value");
        }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
        { Check(false, "saved:true rejected detached synthetic readback"); }
        Check(readbackEntry.Property(x => x.RowVersion).CurrentValue.SequenceEqual(version.CurrentValue),
            "preaccept tracked/readback comparison uses matching EF current versions");
        foreach (var invalid in new[] { (byte[])original[nameof(Employee.RowVersion)]!, new byte[] { 1, 2, 3 } })
        {
            using var invalidDb = new CosmixDbContext(options);
            var invalidParent = SyntheticParent(invalidDb);
            var invalidEntry = invalidDb.Entry(invalidParent);
            var invalidOriginal = invalidEntry.OriginalValues.Clone();
            prepare.Invoke(null, [invalidDb, invalidParent, timestamps[0]]);
            ValidateWrites(invalidDb);
            // Start without an existing generated-value overlay: assigning the
            // CLR's existing value can be a no-op in EF's internal setter.
            invalidEntry.GetInfrastructure().SetStoreGeneratedValue(
                invalidEntry.Property(x => x.RowVersion).Metadata, invalid);
            Check(invalidEntry.Property(x => x.RowVersion).CurrentValue.SequenceEqual(invalid),
                "invalid rowversion precondition is the actual EF current value");
            Check(invalidParent.DraftRevision == 6 &&
                invalidEntry.Property(x => x.DraftRevision).OriginalValue == 5,
                "invalid rowversion case retains valid revision transition");
            try
            {
                preserve.Invoke(null, [invalidDb, invalidParent, invalidOriginal, true]);
                Check(false, "saved:true must reject unchanged or malformed generated version");
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { Check(true, "saved:true rejects unchanged or malformed generated version"); }
        }
        db.ChangeTracker.AcceptAllChanges();
        Check(parent.RowVersion.SequenceEqual(generated) && version.OriginalValue.SequenceEqual(generated) &&
            entry.State == EntityState.Unchanged, "AcceptAllChanges materializes generated rowversion into CLR and originals");
        Check(parent.DraftRevision == 6, "AcceptAllChanges does not repeat persistence revision increment");
    }
    using (var db = new CosmixDbContext(options))
    {
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        var snapshot = entry.OriginalValues.Clone();
        foreach (var property in entry.Properties)
        {
            var previous = property.CurrentValue;
            var clrType = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
            object? changed = clrType == typeof(Guid) ? Guid.NewGuid() :
                clrType == typeof(string) ? "OFFLINE DIFFERENT VALUE" :
                clrType == typeof(int) ? (previous is int integer ? integer + 1 : 1) :
                clrType == typeof(long) ? (previous is long number ? number + 1 : 1L) :
                clrType == typeof(bool) ? !(previous is true) :
                clrType == typeof(DateOnly) ? new DateOnly(2001, 2, 3) :
                clrType == typeof(DateTimeOffset) ? timestamps[0].AddDays(-9) :
                clrType == typeof(byte[]) ? new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 } :
                clrType.IsEnum ? Enum.ToObject(clrType, previous is null ? 0 : Convert.ToInt32(previous) + 1) :
                throw new InvalidOperationException("Add an explicit mutation for " + property.Metadata.Name);
            // Mutate a detached instance: changing a tracked key would itself
            // throw before the fixture preservation check is exercised.
            entry.State = EntityState.Detached;
            property.CurrentValue = changed;
            try
            {
                preserve.Invoke(null, [db, parent, snapshot, false]);
                Check(false, "preservation rejects changed " + property.Metadata.Name);
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { Check(true, "preservation rejects changed " + property.Metadata.Name); }
            property.CurrentValue = previous;
        }
    }
    foreach (var wrongRevision in new[] { 4, 6 })
    {
        using var db = new CosmixDbContext(options);
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        entry.State = EntityState.Detached;
        entry.Property(x => x.DraftRevision).CurrentValue = wrongRevision;
        entry.State = EntityState.Unchanged;
        entry.Property(x => x.DraftRevision).OriginalValue = wrongRevision;
        Check(parent.DraftRevision == wrongRevision &&
            entry.Property(x => x.DraftRevision).CurrentValue == wrongRevision &&
            entry.Property(x => x.DraftRevision).OriginalValue == wrongRevision &&
            entry.State == EntityState.Unchanged, "wrong revision baseline is materialized, not reverted to five");
        try { prepare.Invoke(null, [db, parent, timestamps[0]]); Check(false, "reject baseline revision " + wrongRevision); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
        { Check(true, "reject baseline revision " + wrongRevision); }
        Check(db.ChangeTracker.Entries().Count() == 1, "bad baseline creates no source rows");
    }
}
// Continue01 validates a retained snapshot; these are offline synthetic probes
// of the validators, not claims that synthetic rows possess the retained hashes.
var validateExisting = type.GetMethod("ValidateExistingAsync", BindingFlags.Static | BindingFlags.NonPublic);
Check(validateExisting is not null && validateExisting.IsAssembly && validateExisting.ReturnType == typeof(Task) &&
    validateExisting.GetParameters().Select(x => x.ParameterType).SequenceEqual([typeof(CosmixDbContext), typeof(CancellationToken)]),
    "read-only internal ValidateExistingAsync(db,ct) entry");
if (validateExisting is not null)
{
    object? Existing(string name, params object?[] args)
    {
        try { return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    }
    void RejectExisting(Action action, string label)
    {
        try { action(); Check(false, label); }
        catch (InvalidOperationException) { Check(true, label); }
    }
    string[] hashes =
    [
        "2CC1B277B355D92479CFDCA23611F3AF9971EAEE83A9CDB0F0F3041858D9E042",
        "3D09C4E80C7FFD197FC1FB63F85D77ECE55B1CAE7FBC016222CEF1572DB2ADDE",
        "B5C3619EA599659616EFF915E39EC1E3EBA1A6B03464A31D6BE674354DCF86A8",
        "388D381C112E0EE833E5A36A5DA121FF4DB5278C6312C956D9F11E1E2C45927A",
        "0AB37A4302B564DA7765AE01D3907AE8CE0CF652B06A0BCCF8CD672309B6E834",
        "F1207E68FB91C8DEC55B905FAB6D81846692BF63C574179B29791EA608F70484",
        "96D59F8DED854CFF9B1A96CC8301607D98090F0FA84670058D9C3D095D053110",
        "2435317479B89643674F335B25D740A873BF083CEC7DCC16FC34FAC4BFCA14FF",
        "4A9F8539491CDACA6F71703EEEB19B43CC273A977545AA6E55C474C6999AA831"
    ];
    for (var i = 0; i < hashes.Length; i++)
    {
        Existing("RequireExistingHash", i, 1, hashes[i]);
        Check(true, "exact retained Retry02 source hash accepted " + i);
        RejectExisting(() => Existing("RequireExistingHash", i, 0, hashes[i]), "missing retained source rejected " + i);
        RejectExisting(() => Existing("RequireExistingHash", i, 2, hashes[i]), "duplicate retained source rejected " + i);
        RejectExisting(() => Existing("RequireExistingHash", i, 1, hashes[(i + 1) % hashes.Length]), "changed retained source rejected " + i);
        var sql = (string)Existing("ExistingHashSql", i)!;
        var table = "[" + source[i].GetProperty("table").GetString()!.Replace(".", "].[", StringComparison.Ordinal) + "]";
        Check(sql.Contains(table, StringComparison.Ordinal) && sql.Contains("COUNT(*)", StringComparison.Ordinal) &&
            sql.Contains("SELECT * FROM " + table + " WHERE Id=@id FOR JSON PATH, INCLUDE_NULL_VALUES", StringComparison.Ordinal) &&
            sql.Contains("HASHBYTES('SHA2_256'", StringComparison.Ordinal) && sql.Contains("CONVERT(varchar(64)", StringComparison.Ordinal),
            "exact retained SQL hash algorithm and parameterized identity " + i);
        Check(!System.Text.RegularExpressions.Regex.IsMatch(sql, @"\b(INSERT|UPDATE|DELETE|MERGE|ALTER|DROP|EXEC)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase), "hash query is SELECT-only " + i);
    }
    using (var db = new CosmixDbContext(options))
    {
        var rows = (object[])create.Invoke(null, [db, timestamps[0]])!;
        foreach (var row in rows)
        {
            var entry = db.Entry(row);
            if (entry.Metadata.FindProperty("RowVersion") is not null)
                entry.Property("RowVersion").CurrentValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        }
        Existing("RequireExistingRows", db, rows, timestamps[0]);
        Check(!db.ChangeTracker.Entries().Any(), "materialized retained-field check keeps all nine rows detached");
        for (var i = 0; i < rows.Length; i++)
        {
            foreach (var property in db.Entry(rows[i]).Properties)
            {
                var previous = property.CurrentValue;
                var clr = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
                property.CurrentValue = clr == typeof(Guid) ? Guid.NewGuid() :
                    clr == typeof(string) ? "OFFLINE CHANGED SOURCE" :
                    clr == typeof(int) ? (previous is int integer ? integer + 1 : 1) :
                    clr == typeof(bool) ? !(previous is true) :
                    clr == typeof(DateOnly) ? new DateOnly(2030, 1, 2) :
                    clr == typeof(DateTimeOffset) ? timestamps[0].AddHours(1) :
                    clr == typeof(byte[]) ? new byte[] { 1, 2, 3 } :
                    clr.IsEnum ? Enum.ToObject(clr, previous is null ? 0 : Convert.ToInt32(previous) + 1) :
                    throw new InvalidOperationException("Missing mutation for " + property.Metadata.Name);
                RejectExisting(() => Existing("RequireExistingRows", db, rows, timestamps[0]),
                    "reject changed source " + i + "." + property.Metadata.Name);
                property.CurrentValue = previous;
            }
        }
        RejectExisting(() => Existing("RequireExistingRows", db, rows.Take(8).ToArray(), timestamps[0]), "reject eight materialized sources");
        RejectExisting(() => Existing("RequireExistingRows", db, rows, timestamps[1]), "reject changed original seed timestamp");
        Check(!db.ChangeTracker.Entries().Any(), "source rejection probes never stage mutations");
    }
    using (var db = new CosmixDbContext(options))
    {
        var parent = SyntheticParent(db);
        var entry = db.Entry(parent);
        entry.State = EntityState.Detached;
        entry.Property(x => x.DraftRevision).CurrentValue = 6;
        entry.Property(x => x.RowVersion).CurrentValue = Convert.FromHexString("0000000000338B54");
        Existing("RequireExistingParent", parent);
        Check(true, "Continue01 exact retained parent revision six and rowversion accepted");
        foreach (var name in new[] { "Id", "Status", "OnboardingStatus", "JoinedOn", "DraftRevision", "RowVersion" })
        {
            var property = entry.Property(name);
            var previous = property.CurrentValue;
            property.CurrentValue = name switch {
                "Id" => Guid.NewGuid(), "Status" => EmployeeStatus.Draft,
                "OnboardingStatus" => OnboardingStatus.PendingApproval,
                "JoinedOn" => new DateOnly(2026, 1, 16), "DraftRevision" => 5,
                "RowVersion" => Convert.FromHexString("0000000000338B55"),
                _ => throw new InvalidOperationException()
            };
            RejectExisting(() => Existing("RequireExistingParent", parent), "reject changed retained parent " + name);
            property.CurrentValue = previous;
        }
        Check(parent.DraftRevision == 6 && !db.ChangeTracker.Entries().Any(), "reuse parent validation never advances or tracks revision");
        var key = "COSMIX_EXISTING_TESTING_HOST";
        var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            foreach (var profile in new[] { "Not-O1", "HumaHrOutstation20261020O1" })
            {
                Environment.SetEnvironmentVariable(key, profile);
                try
                {
                    await (Task)validateExisting.Invoke(null, [db, CancellationToken.None])!;
                    Check(false, "reuse must refuse profile or closed connection " + profile);
                }
                catch (InvalidOperationException) { Check(true, "reuse refuses before I/O " + profile); }
            }
        }
        finally { Environment.SetEnvironmentVariable(key, prior); }
    }
}
Check(connectionBlocker.Attempts == 0, "zero attempted connection opens");
Check(commandBlocker.Attempts == 0, "zero attempted SQL commands");
Console.WriteLine($"VerifyHrOutstationO1Fixture: {checks - failures.Count}/{checks} checks passed; {failures.Count} failed.");
Console.WriteLine("Actual CreateRows, bounded preparer and ValidateTrackedWrites; exact manifest and parent preservation; real valuation; EF translation only. No SQL, host, HTTP, credentials, migrations or SaveChanges.");
if (failures.Count != 0) Environment.Exit(1);

Employee SyntheticParent(CosmixDbContext db)
{
    var parent = new Employee(timestamps[0].AddYears(-1), "OFFLINE SYNTHETIC PARENT");
    var entry = db.Entry(parent);
    entry.Property(x => x.Id).CurrentValue = manifest.GetProperty("actors").GetProperty("requester").GetProperty("employeeId").GetGuid();
    entry.Property(x => x.Status).CurrentValue = EmployeeStatus.Active;
    entry.Property(x => x.OnboardingStatus).CurrentValue = OnboardingStatus.Approved;
    entry.Property(x => x.DraftRevision).CurrentValue = 5;
    entry.Property(x => x.JoinedOn).CurrentValue = new DateOnly(2026, 1, 15);
    entry.Property(x => x.RowVersion).CurrentValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    entry.State = EntityState.Unchanged;
    return parent;
}

sealed class NoConnection : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new InvalidOperationException("Offline verifier forbids opening connections."); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new InvalidOperationException("Offline verifier forbids opening connections."); }
}
sealed class NoCommand : DbCommandInterceptor
{
    public int Attempts { get; private set; }
    private void Deny() { Attempts++; throw new InvalidOperationException("Offline verifier forbids SQL execution."); }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
    { Deny(); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    { Deny(); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result)
    { Deny(); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<object> result, CancellationToken ct = default)
    { Deny(); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result)
    { Deny(); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken ct = default)
    { Deny(); return ValueTask.FromResult(result); }
}
