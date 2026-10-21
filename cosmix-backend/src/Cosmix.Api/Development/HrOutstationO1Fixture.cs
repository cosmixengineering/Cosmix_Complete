using System.Data;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

// Explicit historical test prerequisites, not a hiring/publication/approval
// workflow. Only the separately guarded O1 startup may invoke this method.
internal static class HrOutstationO1Fixture
{
    private static readonly Guid HrAccount = new("8475187f-bf3b-4f9c-a3ed-4bac32253005");
    private static readonly Guid HrEmployee = new("35d19922-70a8-4c80-8578-e1029360e9fc");
    private static readonly Guid AdminAccount = new("94a83b62-5b56-4450-9175-2a7a9e3261bf");
    private static readonly Guid AdminEmployee = new("7c505086-87ea-4fd2-980e-823ecccbe36a");
    private static readonly DateTimeOffset GrantFrom = new(2026, 10, 20, 11, 9, 40, TimeSpan.Zero);
    private static readonly DateTimeOffset GrantStart = GrantFrom.AddTicks(589427);
    private static readonly DateTimeOffset GrantEnd = GrantStart.AddDays(1);
    private static readonly DateOnly EmploymentFrom = new(2026, 9, 1), EmploymentTo = new(2026, 10, 21);
    private static readonly DateOnly PolicyFrom = new(2026, 10, 18), PolicyTo = new(2026, 10, 20);
    private const string Reason = "TEST ONLY O1 outstation business acceptance";
    private static readonly Guid[] Ids =
    [
        new("20102001-0001-4000-8000-000000000001"), new("20102001-0002-4000-8000-000000000002"),
        new("20102001-0003-4000-8000-000000000003"), new("20102001-0004-4000-8000-000000000004"),
        new("20102001-0005-4000-8000-000000000005"), new("20102001-0006-4000-8000-000000000006"),
        new("20102001-0007-4000-8000-000000000007"), new("20102001-0008-4000-8000-000000000008"),
        new("20102001-0009-4000-8000-000000000009")
    ];

    private static readonly (string Table, string Hash)[] ExistingSources =
    [
        ("[hr].[Departments]", "2CC1B277B355D92479CFDCA23611F3AF9971EAEE83A9CDB0F0F3041858D9E042"),
        ("[hr].[Designations]", "3D09C4E80C7FFD197FC1FB63F85D77ECE55B1CAE7FBC016222CEF1572DB2ADDE"),
        ("[hr].[StaffCategories]", "B5C3619EA599659616EFF915E39EC1E3EBA1A6B03464A31D6BE674354DCF86A8"),
        ("[hr].[EmploymentTypes]", "388D381C112E0EE833E5A36A5DA121FF4DB5278C6312C956D9F11E1E2C45927A"),
        ("[hr].[Branches]", "0AB37A4302B564DA7765AE01D3907AE8CE0CF652B06A0BCCF8CD672309B6E834"),
        ("[hr].[EmployeeAssignments]", "F1207E68FB91C8DEC55B905FAB6D81846692BF63C574179B29791EA608F70484"),
        ("[hr].[Policies]", "96D59F8DED854CFF9B1A96CC8301607D98090F0FA84670058D9C3D095D053110"),
        ("[hr].[PolicyVersions]", "2435317479B89643674F335B25D740A873BF083CEC7DCC16FC34FAC4BFCA14FF"),
        ("[hr].[PolicyAssignments]", "4A9F8539491CDACA6F71703EEEB19B43CC273A977545AA6E55C474C6999AA831")
    ];

    internal static async Task ValidateExistingAsync(CosmixDbContext db, CancellationToken ct)
    {
        Require(HrOutstationO1Guard.IsSelected && OperatingSystem.IsWindows());
        Require(db.Database.CurrentTransaction is null && !db.ChangeTracker.Entries().Any());
        Require(db.Database.GetDbConnection().State == ConnectionState.Open);
        HrOutstationO1Guard.ValidateConnection(db.Database.GetConnectionString());
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await HrOutstationO1Database.VerifyAsync(db, new ConfigurationBuilder().Build(), ct,
            allowUnencryptedLocalPipe: true);
        await RequireExistingHashesAsync(db, ct);
        await RequireAuthoritiesAsync(db, DateTimeOffset.UtcNow, ct);
        Require(await db.EmployeeAssignments.CountAsync(ct) == 1 && await db.Policies.CountAsync(ct) == 1 &&
            await db.PolicyVersions.CountAsync(ct) == 1 && await db.PolicyAssignments.CountAsync(ct) == 1);
        Require(!await db.PolicyAssignmentEnds.AnyAsync(ct) &&
            !await db.Set<OutstationDeployment>().AnyAsync(ct) &&
            !await db.Set<DeploymentHomeBreakRequest>().AnyAsync(ct) &&
            !await db.AllowanceClaims.AnyAsync(ct) && !await db.AllowanceClaimSubmissions.AnyAsync(ct) &&
            !await db.AllowanceSettlementRecords.AnyAsync(ct) &&
            !await db.ApprovalRequests.AnyAsync(x => x.RequestType == DeploymentService.RequestType ||
                x.RequestType == DeploymentService.BreakType || x.RequestType == DeploymentService.ClaimType, ct));
        object[] rows =
        [
            await db.Set<Department>().AsNoTracking().SingleAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct),
            await db.Set<Designation>().AsNoTracking().SingleAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct),
            await db.Set<StaffCategory>().AsNoTracking().SingleAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct),
            await db.Set<EmploymentType>().AsNoTracking().SingleAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct),
            await db.Set<Branch>().AsNoTracking().SingleAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct),
            await db.EmployeeAssignments.AsNoTracking().SingleAsync(ct),
            await db.Policies.AsNoTracking().SingleAsync(ct),
            await db.PolicyVersions.AsNoTracking().SingleAsync(ct),
            await db.PolicyAssignments.AsNoTracking().SingleAsync(ct)
        ];
        // The static retained SQL hashes above bind every column, including the
        // original timestamps and rowversions. Only then inspect domain values.
        RequireExistingRows(db, rows, ((EmployeeAssignment)rows[5]).RecordedAt);
        var parent = await db.Set<Employee>().AsNoTracking().SingleAsync(x => x.Id == HrEmployee, ct);
        RequireExistingParent(parent);
        await RequireAuthoritiesAsync(db, DateTimeOffset.UtcNow, ct);
        Require(DateTimeOffset.UtcNow < GrantEnd && HrOutstationO1Guard.IsSelected &&
            !db.ChangeTracker.Entries().Any());
        // Read-only transaction: no SaveChanges, state acceptance, revision
        // advancement, source repair, or reuse of the seed write path.
        await transaction.CommitAsync(ct);
    }

    private static string ExistingHashSql(int index)
    {
        Require(index >= 0 && index < ExistingSources.Length);
        var table = ExistingSources[index].Table;
        return $"""
            SELECT COUNT(*), CONVERT(varchar(64), HASHBYTES('SHA2_256',
                (SELECT * FROM {table} WHERE Id=@id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
            FROM {table} WHERE Id=@id;
            """;
    }

    private static void RequireExistingHash(int index, int count, string hash)
    {
        Require(index >= 0 && index < ExistingSources.Length && count == 1 &&
            string.Equals(hash, ExistingSources[index].Hash, StringComparison.Ordinal));
    }

    private static async Task RequireExistingHashesAsync(CosmixDbContext db, CancellationToken ct)
    {
        for (var index = 0; index < ExistingSources.Length; index++)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = ExistingHashSql(index);
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@id"; parameter.DbType = DbType.Guid; parameter.Value = Ids[index];
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct) && !reader.IsDBNull(0) && !reader.IsDBNull(1));
            RequireExistingHash(index, reader.GetInt32(0), reader.GetString(1));
            Require(!await reader.ReadAsync(ct) && !await reader.NextResultAsync(ct));
        }
    }

    private static void RequireExistingRows(CosmixDbContext db, object[] rows, DateTimeOffset seedUtc)
    {
        Require(seedUtc.Offset == TimeSpan.Zero && seedUtc >= GrantStart && seedUtc < GrantEnd &&
            rows.Length == 9 && !db.ChangeTracker.Entries().Any());
        var expected = CreateRows(db, seedUtc);
        for (var index = 0; index < rows.Length; index++)
        {
            Require(rows[index].GetType() == expected[index].GetType());
            var actualEntry = db.Entry(rows[index]);
            var expectedEntry = db.Entry(expected[index]);
            Require(actualEntry.State == EntityState.Detached);
            foreach (var property in actualEntry.Properties)
            {
                if (property.Metadata.Name == "RowVersion")
                {
                    // Exact bytes are bound by RequireExistingHashesAsync, not
                    // inferred or accepted as a fresh baseline from this row.
                    Require(property.CurrentValue is byte[] { Length: 8 });
                    continue;
                }
                var expectedValue = expectedEntry.Property(property.Metadata.Name).CurrentValue;
                Require(property.Metadata.GetValueComparer()!.Equals(property.CurrentValue, expectedValue));
            }
        }
        Require(!db.ChangeTracker.Entries().Any());
    }

    private static void RequireExistingParent(Employee parent)
    {
        Require(parent.Id == HrEmployee && parent.Status == EmployeeStatus.Active &&
            parent.OnboardingStatus == OnboardingStatus.Approved &&
            parent.JoinedOn == new DateOnly(2026, 1, 15) && parent.DraftRevision == 6 &&
            parent.RowVersion.SequenceEqual(Convert.FromHexString("0000000000338B54")));
    }

    internal static async Task SeedAsync(CosmixDbContext db, CancellationToken ct)
    {
        Require(HrOutstationO1Guard.IsSelected && OperatingSystem.IsWindows());
        Require(db.Database.CurrentTransaction is null && !db.ChangeTracker.Entries().Any());
        Require(db.Database.GetDbConnection().State == ConnectionState.Open);
        HrOutstationO1Guard.ValidateConnection(db.Database.GetConnectionString());
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // VerifyAsync must bind its command to CurrentTransaction. It checks
            // the real pinned target, schema, P1 catalog, accounts and O1 markers.
            // Its configuration argument is unused; do not read any private file.
            await HrOutstationO1Database.VerifyAsync(db, new ConfigurationBuilder().Build(), ct,
                allowUnencryptedLocalPipe: true);
            await RequireEmptySourcesAsync(db, ct);
            await RequireAuthoritiesAsync(db, DateTimeOffset.UtcNow, ct);
            var parent = await db.Set<Employee>().SingleAsync(x => x.Id == HrEmployee, ct);
            var originalParent = db.Entry(parent).OriginalValues.Clone();
            foreach (var property in originalParent.Properties)
                if (originalParent[property.Name] is byte[] bytes)
                    originalParent[property.Name] = bytes.ToArray();
            var recordedAt = DateTimeOffset.UtcNow;
            PrepareRows(db, parent, recordedAt);
            // The normal persistence guard advances DraftRevision once. Retain
            // originals until the exact nine-insert/one-parent-update check passes.
            Require(await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct) == 10);
            RequireSavedWriteSet(db, parent, originalParent);
            var persistedParent = await db.Set<Employee>().AsNoTracking().SingleAsync(x => x.Id == HrEmployee, ct);
            RequireParentPreserved(db, persistedParent, originalParent, saved: true);
            Require(db.Entry(persistedParent).Property(x => x.RowVersion).CurrentValue
                .SequenceEqual(db.Entry(parent).Property(x => x.RowVersion).CurrentValue));
            db.ChangeTracker.AcceptAllChanges();
            await RequireSeedReadbackAsync(db, recordedAt, ct);
            await RequireAuthoritiesAsync(db, DateTimeOffset.UtcNow, ct);
            Require(HrOutstationO1Guard.IsSelected && !db.ChangeTracker.HasChanges());
            await transaction.CommitAsync(ct);
        }
        finally
        {
            // This context was required to be empty on entry. No caller-owned
            // tracked entity or staged mutation can be discarded here.
            db.ChangeTracker.Clear();
        }
    }

    private static void PrepareRows(CosmixDbContext db, Employee parent, DateTimeOffset recordedAt)
    {
        Require(db.ChangeTracker.Entries().Count() == 1 && db.Entry(parent).State == EntityState.Unchanged);
        RequireParentPreserved(db, parent, db.Entry(parent).OriginalValues, saved: false);
        db.AddRange(CreateRows(db, recordedAt));
        Require(db.ChangeTracker.Entries().Count() == 10 &&
            db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added &&
                Ids.Contains((Guid)e.Property("Id").CurrentValue!)) == 9 &&
            db.Entry(parent).State == EntityState.Unchanged);
    }

    private static void RequireSavedWriteSet(CosmixDbContext db, Employee parent, PropertyValues original)
    {
        RequireParentPreserved(db, parent, original, saved: true);
        Require(db.ChangeTracker.Entries().Count() == 10 &&
            db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added &&
                Ids.Contains((Guid)e.Property("Id").CurrentValue!)) == 9 &&
            db.Entry(parent).State == EntityState.Modified &&
            db.Entry(parent).Property(x => x.DraftRevision).IsModified &&
            db.Entry(parent).Properties.Where(p => p.IsModified)
                .All(p => p.Metadata.Name is nameof(Employee.DraftRevision) or nameof(Employee.RowVersion)));
    }

    private static void RequireParentPreserved(CosmixDbContext db, Employee parent, PropertyValues original, bool saved)
    {
        Require(parent.Id == HrEmployee &&
            (Guid)original[nameof(Employee.Id)]! == HrEmployee &&
            (int)original[nameof(Employee.DraftRevision)]! == 5 &&
            (EmployeeStatus)original[nameof(Employee.Status)]! == EmployeeStatus.Active &&
            (OnboardingStatus)original[nameof(Employee.OnboardingStatus)]! == OnboardingStatus.Approved &&
            (DateOnly?)original[nameof(Employee.JoinedOn)] == new DateOnly(2026, 1, 15) &&
            parent.DraftRevision == (saved ? 6 : 5));
        var entry = db.Entry(parent);
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.Name is nameof(Employee.DraftRevision) or nameof(Employee.RowVersion)) continue;
            Require(property.Metadata.GetValueComparer()!.Equals(property.CurrentValue, original[property.Metadata.Name]));
        }
        var oldVersion = (byte[])original[nameof(Employee.RowVersion)]!;
        var version = entry.Property(x => x.RowVersion);
        // SaveChanges(false) retains the provider-generated value in EF until
        // acceptance; the CLR property can still contain the original version.
        Require(oldVersion.Length == 8 && version.CurrentValue is { Length: 8 } currentVersion &&
            version.Metadata.IsConcurrencyToken &&
            version.Metadata.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate &&
            !version.IsTemporary &&
            (saved
                ? !currentVersion.SequenceEqual(oldVersion) &&
                    ((entry.State == EntityState.Modified && parent.RowVersion.SequenceEqual(oldVersion) &&
                        version.OriginalValue.SequenceEqual(oldVersion)) ||
                     (entry.State == EntityState.Detached && !version.IsModified &&
                        parent.RowVersion.SequenceEqual(currentVersion)))
                : !version.IsModified && currentVersion.SequenceEqual(oldVersion) &&
                    parent.RowVersion.SequenceEqual(oldVersion)));
    }

    private static object[] CreateRows(CosmixDbContext db, DateTimeOffset recordedAt)
    {
        var rules = new AllowancePolicyRules(2, "PKR", "09:00", 0,
            new(8m, 0m), new(8m, 0m), new(8m, 0m), new(8m, 0m),
            new(null, 100m), null,
            new(DeploymentCountingBasis.LocalCalendarDays, DeploymentPartialTreatment.FullDay,
                DeploymentPartialTreatment.FullDay, DeploymentPartialTreatment.FullDay,
                DeploymentPartialTreatment.FullDay, DeploymentPartialTreatment.NotApplicable,
                2, MidpointRounding.ToEven, DeploymentRoundingStage.FinalClaim, ["TEST_VO"],
                DeploymentPolicyTransition.LocalCalendarDayEffectivePolicy));
        object[] rows =
        [
            new Department("TEST-VO-D", "Synthetic VO department"),
            new Designation("TEST-VO-J", "Synthetic VO designation"),
            new StaffCategory("TEST-VO-C", "Synthetic VO staff category"),
            new EmploymentType("TEST-VO-E", "Synthetic VO employment type"),
            new Branch("TEST-VO-B", "Synthetic VO branch", null, null),
            new EmployeeAssignment(HrEmployee, Ids[0], Ids[1], Ids[2], Ids[3], Ids[4],
                null, null, EmploymentFrom, EmploymentTo, HrAccount.ToString("D"), recordedAt),
            new Policy("TEST-VO-ALLOW", "Synthetic VO outstation allowance", PolicyCategory.Allowance),
            new PolicyVersion(Ids[6], 1, PolicyFrom, PolicyTo, "allowance.v2",
                AllowancePolicyRulesCodec.Canonicalize(rules), null, null, HrAccount.ToString("D"),
                recordedAt, PolicyAuthority.DirectHr),
            new PolicyAssignment(Ids[7], null, HrEmployee, null, null, null, PolicyFrom, PolicyTo,
                Reason, null, null, HrAccount.ToString("D"), recordedAt, PolicyAuthority.DirectHr)
        ];
        // Domain constructors validate every business field. Only the explicitly
        // approved fixture IDs are replaced through EF while entities are detached.
        for (var index = 0; index < rows.Length; index++)
        {
            var entry = db.Entry(rows[index]);
            Require(entry.State == EntityState.Detached);
            entry.Property("Id").CurrentValue = Ids[index];
        }
        return rows;
    }

    private static async Task RequireEmptySourcesAsync(CosmixDbContext db, CancellationToken ct)
    {
        // These globally empty source tables are the reviewed baseline, not an
        // invitation to merge with a partially executed or unrelated fixture.
        Require(!await db.EmployeeAssignments.AnyAsync(ct) && !await db.Policies.AnyAsync(ct) &&
            !await db.PolicyVersions.AnyAsync(ct) && !await db.PolicyAssignments.AnyAsync(ct) &&
            !await db.PolicyAssignmentEnds.AnyAsync(ct) &&
            !await db.Set<OutstationDeployment>().AnyAsync(ct) &&
            !await db.Set<DeploymentHomeBreakRequest>().AnyAsync(ct) &&
            !await db.AllowanceClaims.AnyAsync(ct) && !await db.AllowanceClaimSubmissions.AnyAsync(ct) &&
            !await db.AllowanceSettlementRecords.AnyAsync(ct));
        Require(!await db.Set<Department>().AnyAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct) &&
            !await db.Set<Designation>().AnyAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct) &&
            !await db.Set<StaffCategory>().AnyAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct) &&
            !await db.Set<EmploymentType>().AnyAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct) &&
            !await db.Set<Branch>().AnyAsync(x => Ids.Contains(x.Id) || x.Code.StartsWith("TEST-VO-"), ct));
    }

    private static async Task RequireAuthoritiesAsync(CosmixDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        Require(now >= GrantStart && now < GrantEnd && HrAccount != AdminAccount && HrEmployee != AdminEmployee);
        var accounts = await db.UserAccounts.AsNoTracking().Include(x => x.Employee)
            .Where(x => x.Id == HrAccount || x.Id == AdminAccount).ToArrayAsync(ct);
        Require(accounts.Length == 2 && accounts.All(x => x.Status == AccountStatus.Active &&
            x.WebAccessEnabled && x.MfaRequired && (x.LockedUntil is null || x.LockedUntil <= now) &&
            x.Employee.Status == EmployeeStatus.Active && x.Employee.JoinedOn.HasValue &&
            x.Employee.JoinedOn <= EmploymentFrom));
        Require(accounts.Single(x => x.Id == HrAccount).EmployeeId == HrEmployee &&
            accounts.Single(x => x.Id == AdminAccount).EmployeeId == AdminEmployee);
        Require(await db.Set<UserRoleAssignment>().AsNoTracking().AnyAsync(x => x.UserAccountId == AdminAccount &&
            x.EmployeeId == AdminEmployee && x.Role.SystemRole == SystemRoleKind.Admin && x.Role.Code == "ADMIN" &&
            x.Role.IsActive && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo) &&
            (x.RevokedAt == null || now < x.RevokedAt), ct));
        await DelegatedAsync("TEST-HR-P1-20261020-ADMIN", AdminAccount, AdminEmployee, AccessScopeKind.All,
            ["attendance.leaves.benefits.review", "attendance.vehicle.policy.manage", "attendance.deployments.read", "attendance.deployments.decide"]);
        await DelegatedAsync("TEST-HR-P1-20261020-REQUESTER", HrAccount, HrEmployee, AccessScopeKind.Employee,
            ["attendance.deployments.read", "attendance.deployments.request"]);

        async Task DelegatedAsync(string code, Guid account, Guid employee, AccessScopeKind kind, string[] expected)
        {
            var roles = await db.Set<Role>().AsNoTracking().Where(x => x.Code == code).ToArrayAsync(ct);
            Require(roles.Length == 1 && roles[0].Code == code && roles[0].IsActive &&
                roles[0].SystemRole == SystemRoleKind.None);
            var roleId = roles[0].Id;
            var assignments = await db.Set<UserRoleAssignment>().AsNoTracking().Where(x => x.RoleId == roleId).ToArrayAsync(ct);
            Require(assignments.Length == 1);
            var assignment = assignments[0];
            Require(assignment.UserAccountId == account && assignment.EmployeeId == employee &&
                assignment.EffectiveFrom == GrantStart && assignment.EffectiveTo == GrantEnd &&
                assignment.RevokedAt is null && assignment.IsEffectiveAt(now));
            var scopes = await db.Set<UserAccessScope>().AsNoTracking()
                .Where(x => x.UserRoleAssignmentId == assignment.Id).ToArrayAsync(ct);
            Require(scopes.Length == 1 && scopes[0].Kind == kind &&
                scopes[0].EmployeeId == (kind == AccessScopeKind.Employee ? employee : (Guid?)null) &&
                scopes[0].DepartmentId is null && scopes[0].TeamId is null &&
                scopes[0].SiteId is null && scopes[0].ProjectId is null);
            var grants = await db.Set<RolePermission>().AsNoTracking().Include(x => x.Permission)
                .Where(x => x.RoleId == roleId).ToArrayAsync(ct);
            Require(grants.Length == expected.Length && grants.All(x => x.RevokedAt is null && x.GrantedAt <= now) &&
                grants.Select(x => x.Permission.Code).Order(StringComparer.Ordinal)
                    .SequenceEqual(expected.Order(StringComparer.Ordinal)));
        }
    }

    private static async Task RequireSeedReadbackAsync(CosmixDbContext db, DateTimeOffset recordedAt, CancellationToken ct)
    {
        Require(await db.EmployeeAssignments.CountAsync(ct) == 1 && await db.Policies.CountAsync(ct) == 1 &&
            await db.PolicyVersions.CountAsync(ct) == 1 && await db.PolicyAssignments.CountAsync(ct) == 1);
        var employment = await db.EmployeeAssignments.AsNoTracking().SingleAsync(ct);
        var version = await db.PolicyVersions.AsNoTracking().Include(x => x.Policy).SingleAsync(ct);
        var assignment = await db.PolicyAssignments.AsNoTracking().SingleAsync(ct);
        Require(employment.Id == Ids[5] && employment.EmployeeId == HrEmployee &&
            employment.DepartmentId == Ids[0] && employment.DesignationId == Ids[1] &&
            employment.StaffCategoryId == Ids[2] && employment.EmploymentTypeId == Ids[3] &&
            employment.BranchId == Ids[4] && employment.SiteId is null && employment.ReportingManagerId is null &&
            employment.EffectiveFrom == EmploymentFrom && employment.EffectiveTo == EmploymentTo &&
            employment.RecordedByUserId == HrAccount.ToString("D") && employment.RecordedAt == recordedAt);
        var rules = AllowancePolicyRulesCodec.ValidateAndCanonicalize(version.RulesJson);
        Require(version.Id == Ids[7] && version.PolicyId == Ids[6] && version.Policy.IsActive &&
            version.Policy.Category == PolicyCategory.Allowance && version.Policy.Code == "TEST-VO-ALLOW" &&
            version.VersionNumber == 1 && version.EffectiveFrom == PolicyFrom && version.EffectiveTo == PolicyTo &&
            version.RulesSchemaVersion == "allowance.v2" && version.Authority == PolicyAuthority.DirectHr &&
            version.ApprovalRequestId is null && version.PolicyDocumentPrivateReference is null &&
            version.PublishedByUserId == HrAccount.ToString("D") && version.PublishedAt == recordedAt &&
            rules.IsValid && rules.CanonicalJson == version.RulesJson);
        Require(assignment.Id == Ids[8] && assignment.PolicyVersionId == version.Id &&
            assignment.EmployeeId == HrEmployee && assignment.RoleId is null && assignment.DepartmentId is null &&
            assignment.StaffCategoryId is null && assignment.SiteId is null &&
            assignment.EffectiveFrom == PolicyFrom && assignment.EffectiveTo == PolicyTo &&
            assignment.Authority == PolicyAuthority.DirectHr && assignment.ApprovalRequestId is null &&
            assignment.InstructionPrivateReference is null && assignment.Reason == Reason &&
            assignment.RecordedByUserId == HrAccount.ToString("D") && assignment.RecordedAt == recordedAt);
    }

    private static void Require(bool condition)
    {
        if (!condition) throw HrOutstationO1Guard.Refuse();
    }
}
