#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyDelegation.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

// Runs real AccessManagementService, AccessPermissionEvaluator and Domain access entities.
// All identities, persisted account states, grants, repository results and version changes are
// synthetic in-memory fixtures. No login, SQL, private files, rollback or competing SQL writes
// are tested. Current fixed Admin/SuperAdmin authority explicitly configures catalog permissions;
// owning a business permission is neither required nor sufficient for role administration.
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
async Task Denied(Func<Task> action, string code = "access_denied", int status = 403)
{
    try { await action(); throw new Exception($"Expected {code}/{status}"); }
    catch (AuthenticationFailure failure)
    { Check(failure.Code == code && failure.StatusCode == status, $"Expected {code}/{status}, got {failure.Code}/{failure.StatusCode}"); }
}
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
void MissingJsonReasonRejected<T>(string json)
{
    try { JsonSerializer.Deserialize<T>(json, jsonOptions); throw new Exception($"{typeof(T).Name} accepted JSON without a required reason"); }
    catch (JsonException) { Check(true, $"{typeof(T).Name} rejects an omitted JSON reason"); }
}
MissingJsonReasonRejected<GrantRolePermissionsRequest>("""
    {"permissions":["attendance.rosters.read"],"expectedRoleVersion":"AQAAAAAAAAA="}
    """);
MissingJsonReasonRejected<AssignAccessRoleRequest>("""
    {"roleId":"10000000-0000-0000-0000-000000000001","scopes":[{"kind":1}],"effectiveFrom":"2026-10-09T00:00:00+00:00","expectedAccountVersion":"AQAAAAAAAAA=","expectedRoleVersion":"AQAAAAAAAAA="}
    """);
var grantJson = JsonSerializer.Deserialize<GrantRolePermissionsRequest>("""
    {"permissions":["attendance.rosters.read"],"expectedRoleVersion":"AQAAAAAAAAA=","reason":"Read site roster for assigned duties"}
    """, jsonOptions);
Check(grantJson?.Reason == "Read site roster for assigned duties", "explicit permission-grant JSON reason is preserved by deserialization");
var assignmentJson = JsonSerializer.Deserialize<AssignAccessRoleRequest>("""
    {"roleId":"10000000-0000-0000-0000-000000000001","scopes":[{"kind":1}],"effectiveFrom":"2026-10-09T00:00:00+00:00","expectedAccountVersion":"AQAAAAAAAAA=","expectedRoleVersion":"AQAAAAAAAAA=","reason":"Provide employee self-service for assigned duties"}
    """, jsonOptions);
Check(assignmentJson?.Reason == "Provide employee self-service for assigned duties", "explicit assignment JSON reason is preserved by deserialization");
var codes = SchedulingPermissions.Definitions.Select(x => x.Code).ToArray();
{
    var fixture = new DelegationFixture(failStorageInitialization: true);
    var catalog = await fixture.Service.GetCatalogAsync(fixture.Context, default);
    var ownPermissions = await fixture.Service.GetCurrentPermissionsAsync(fixture.Context, default);
    Check(catalog.Count > 0 && ownPermissions.Count > 0 && fixture.StorageFactoryCalls == 0,
        "catalog/current permission reads do not construct unavailable write storage");
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead), "private_file_storage_unavailable", 503);
    Check(fixture.StorageFactoryCalls == 1 && fixture.Permissions.Count == 0,
        "authorized audit write initializes storage and fails closed before permission mutation");
}
{
    var fixture = new DelegationFixture(failStorageInitialization: true) { Current = false };
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead));
    Check(fixture.StorageFactoryCalls == 0, "authorization is rejected before unavailable write storage is initialized");
}
{
    var fixture = new DelegationFixture();
    await Denied(() => fixture.Service.GrantRolePermissionsAsync(fixture.Role.Id,
        new([SchedulingPermissions.RostersRead], DelegationFixture.Version(fixture.Role)), Guid.NewGuid().ToString("N"), fixture.Context, default), "invalid_access_reason", 400);
    Check(fixture.Permissions.Count == 0 && fixture.Audits.Count == 0 && fixture.Saves == 0,
        "a permission grant without an explicit reason is rejected before staging changes");
}
foreach (var reason in new[] { "", "  ", "missing\nreason", "control\tcharacter", new string('x', 1001) })
{
    var grant = new DelegationFixture();
    await Denied(() => grant.Grant(SchedulingPermissions.RostersRead, reason: reason), "invalid_access_reason", 400);
    Check(grant.Permissions.Count == 0 && grant.Audits.Count == 0 && grant.Evidence.Count == 0 && grant.Saves == 0,
        "invalid grant reason cannot stage a permission, evidence, audit or save");
    var assignment = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    await Denied(() => assignment.Service.AssignRoleAsync(assignment.Target.Id,
        assignment.AssignmentRequest() with { Reason = reason }, Guid.NewGuid().ToString("N"), assignment.Context, default), "invalid_access_reason", 400);
    Check(assignment.Assignments.Count == 0 && assignment.Target.SecurityVersion == 1 && assignment.Evidence.Count == 0 && assignment.Saves == 0,
        "invalid assignment reason cannot change account security or stage writes");
}
{
    var fixture = new DelegationFixture();
    var roleVersion = DelegationFixture.Version(fixture.Role);
    var result = await fixture.Grant(SchedulingPermissions.RostersManage, reason: "  Synthetic site scheduling duties  ");
    var evidence = fixture.Evidence.Single(x => !x.Purpose.Contains(":receipt:", StringComparison.Ordinal));
    var saved = JsonDocument.Parse(evidence.Content).RootElement;
    Check(evidence.Purpose == $"authentication:access:{fixture.Actor.AccountId:N}:access.role.permissions.granted",
        "grant evidence uses a server-selected actor/operation purpose");
    Check(saved.GetProperty("Reason").GetString() == "Synthetic site scheduling duties" &&
          saved.GetProperty("Actor").GetProperty("AccountId").GetGuid() == fixture.Actor.AccountId &&
          saved.GetProperty("Actor").GetProperty("RoleAssignmentId").GetGuid() == fixture.AuthorityAssignmentId,
        "protected evidence binds normalized reason and actual fixed authority");
    Check(saved.GetProperty("Before").GetProperty("ExpectedRoleVersion").GetString() == roleVersion &&
          saved.GetProperty("Before").GetProperty("Permissions").GetArrayLength() == 0 &&
          saved.GetProperty("After").GetProperty("RoleId").GetGuid() == fixture.Role.Id &&
          saved.GetProperty("After").GetProperty("AddedPermissions")[0].GetString() == "attendance.rosters.manage",
        "grant evidence binds expected prior role state and precise intended added permission");
    Check(fixture.Audits.Single().ProtectedChangesReference == JsonSerializer.Serialize(evidence.Reference) &&
          !JsonSerializer.Serialize(result).Contains(evidence.Reference.StorageKey, StringComparison.Ordinal),
        "audit references private evidence without exposing the storage reference through the response");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    var site = Guid.NewGuid();
    var request = fixture.AssignmentRequest([new(AccessScopeKind.Site, site)]) with { Reason = "  Synthetic explicit site assignment  " };
    var result = await fixture.Service.AssignRoleAsync(fixture.Target.Id, request, Guid.NewGuid().ToString("N"), fixture.Context, default);
    var evidence = fixture.Evidence.Single(x => !x.Purpose.Contains(":receipt:", StringComparison.Ordinal));
    var saved = JsonDocument.Parse(evidence.Content).RootElement;
    var after = saved.GetProperty("After");
    Check(evidence.Purpose == $"authentication:access:{fixture.Actor.AccountId:N}:access.assignment.created" &&
          saved.GetProperty("Reason").GetString() == "Synthetic explicit site assignment", "assignment evidence binds operation and normalized reason");
    Check(saved.GetProperty("Before").GetProperty("ExpectedAccountVersion").GetString() == request.ExpectedAccountVersion &&
          saved.GetProperty("Before").GetProperty("ExpectedRoleVersion").GetString() == request.ExpectedRoleVersion &&
          saved.GetProperty("Before").GetProperty("SecurityVersion").GetInt32() == 1,
        "assignment evidence captures expected pre-change account/role state");
    Check(after.GetProperty("AccountId").GetGuid() == fixture.Target.Id && after.GetProperty("EmployeeId").GetGuid() == fixture.Target.EmployeeId &&
          after.GetProperty("AssignmentId").GetGuid() == result.Assignment.Id && after.GetProperty("RoleId").GetGuid() == fixture.Role.Id &&
          after.GetProperty("Permissions")[0].GetString() == "attendance.rosters.read" &&
          after.GetProperty("Scopes")[0].GetProperty("Kind").GetInt32() == 5 && after.GetProperty("Scopes")[0].GetProperty("TargetId").GetGuid() == site &&
          after.GetProperty("EffectiveFrom").GetDateTimeOffset() == request.EffectiveFrom && after.GetProperty("EffectiveTo").ValueKind == JsonValueKind.Null,
        "assignment evidence binds selected employee, role, permission, explicit scope and interval");
    Check(fixture.Audits.Single().ProtectedChangesReference == JsonSerializer.Serialize(evidence.Reference) &&
          !JsonSerializer.Serialize(result).Contains(evidence.Reference.StorageKey, StringComparison.Ordinal), "assignment audit retains private evidence without public disclosure");
}
foreach (var action in new[] { "grant", "assignment" })
{
    var fixture = action == "grant" ? new DelegationFixture() : DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    fixture.FailStorage = true;
    await Denied(() => action == "grant" ? fixture.Grant(SchedulingPermissions.RostersRead) : (Task)fixture.Assign(), "private_file_storage_unavailable", 503);
    Check(fixture.Assignments.Count == 0 && fixture.Audits.Count == 0 && fixture.Evidence.Count == 0 &&
          fixture.Target.SecurityVersion == 1 && fixture.Saves == 0 && fixture.Permissions.Count == (action == "grant" ? 0 : 1),
        "storage failure precedes business/account/audit staging; this fake does not prove SQL rollback");
}
Check(codes.Length == 5 && codes.Distinct(StringComparer.Ordinal).Count() == 5, "five unique scheduling permissions");
foreach (var definition in SchedulingPermissions.Definitions)
{
    Check(AccessPermissionCatalog.Require(definition.Code) == definition, $"catalog registration {definition.Code}");
    Check(!OwnerBootstrapService.FixedPermissionCodes.Contains(definition.Code, StringComparer.Ordinal), $"fixed bootstrap does not silently grant {definition.Code}");
    foreach (var authority in new[] { SystemRoleKind.Admin, SystemRoleKind.SuperAdmin })
    {
        var fixture = new DelegationFixture { Authority = authority };
        try { await fixture.Grant(definition.Code); }
        catch (AuthenticationFailure failure) { throw new Exception($"Current fixed {authority} must explicitly configure first catalog grant {definition.Code} without owning it; got {failure.Code}", failure); }
        Check(fixture.Permissions.Single().Permission.Code == definition.Code && fixture.Audits.Count == 1 && fixture.Saves == 2,
            $"fixed {authority} first grant is explicit and audited");
        Check(!fixture.Grants.Any(x => x.Permission == definition.Code), "configuring a role does not auto-grant business rights to the actor");
        await fixture.Assign([new(AccessScopeKind.Self)]);
        Check(fixture.Assignments.Count == 1 && fixture.Target.SecurityVersion == 2, "fixed authority assigns the explicit role and invalidates the recipient");
    }

    var scoped = new DelegationFixture();
    scoped.Grants.Add(scoped.GrantOf(definition.Code, AccessScopeKind.Employee, scoped.Actor.EmployeeId));
    var granted = await scoped.Grant(definition.Code);
    Check(granted.Permissions.SequenceEqual([definition.Code]) && scoped.Permissions.Single().Permission.Code == definition.Code,
        "fixed administrative authority configures explicit role permission independently of personal employee-scoped business rights");
    Check(scoped.ExpectedVersions.Single().Kind == "Role" && scoped.ExpectedVersions.Single().Bytes.SequenceEqual(DelegationFixture.InitialVersion),
        "expected role version is decoded and forwarded unchanged");
    Check(scoped.Audits.Single().EventType == "access.role.permission.granted" &&
          scoped.Audits.Single().ActorUserId == scoped.Actor.AccountId.ToString() &&
          scoped.Audits.Single().TargetId == $"{scoped.Role.Id}:{definition.Code}", "grant audit binds actor, target and permission");
    await Denied(() => scoped.Grant(definition.Code, granted.Version), "role_permission_exists", 409);
    Check(scoped.Permissions.Count == 1 && scoped.Audits.Count == 1, "duplicate grant has no second permission or audit");
}

foreach (var scenario in new[] { "custom-role", "no-current-fixed-assignment", "operation-not-all", "missing-operation" })
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersManage);
    if (scenario == "custom-role") fixture.Authority = SystemRoleKind.None;
    if (scenario == "no-current-fixed-assignment") fixture.FixedRoleCurrent = false;
    if (scenario == "operation-not-all") fixture.Grants = fixture.Grants.Select(x => x with { ScopeKind = AccessScopeKind.Self }).ToList();
    if (scenario == "missing-operation") fixture.Grants.Clear();
    await Denied(() => fixture.Service.GetCatalogAsync(fixture.Context, default));
    await Denied(() => fixture.Service.CreateRoleAsync(new("SYNTHETIC_NEW", "Synthetic new role"), Guid.NewGuid().ToString("N"), fixture.Context, default));
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead));
    await Denied(() => fixture.Assign());
    Check(fixture.AddedRoles.Count == 0 && fixture.Assignments.Count == 0 && fixture.Audits.Count == 0 && fixture.Saves == 0,
        $"{scenario} cannot administer roles even with personal business permissions");
}
{
    var fixture = new DelegationFixture();
    var role = await fixture.Service.CreateRoleAsync(new("SYNTHETIC_DEPARTMENT", "Synthetic department permissions"), Guid.NewGuid().ToString("N"), fixture.Context, default);
    Check(role.Permissions.Count == 0 && !role.IsSystemRole && fixture.AddedRoles.Single().Id == role.Id,
        "fixed Admin creates an empty custom role without automatic rights or assignments");
    Check(fixture.Audits.Single().EventType == "access.role.created" && fixture.Assignments.Count == 0,
        "explicit role creation is audited without assigning any account");
}
{
    var fixture = new DelegationFixture();
    var customAssignment = Guid.NewGuid();
    fixture.Grants.Insert(0, fixture.GrantOf(AccessPermissionCatalog.RolePermissionsManage, AccessScopeKind.All) with { RoleAssignmentId = customAssignment });
    await fixture.Grant(SchedulingPermissions.RostersRead);
    Check(fixture.Audits.Single().ActorRoleCode == "Admin", "valid fixed assignment remains usable after a custom-role grant is encountered first");
    fixture.FixedRoleCurrent = false;
    await Denied(() => fixture.Grant(SchedulingPermissions.ShiftsRead));
    Check(fixture.Permissions.Count == 1, "fixed-role authority is reread and revocation blocks the next grant");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    fixture.Grants = fixture.Grants.Select(x => x.Permission == AccessPermissionCatalog.RolePermissionsManage
        ? x with { RoleAssignmentId = Guid.NewGuid() } : x).ToList();
    await Denied(() => fixture.Assign());
    Check(fixture.Assignments.Count == 0, "cannot borrow role-permission management from an unrelated assignment");
}
{
    var fixture = new DelegationFixture();
    await Denied(() => fixture.Grant("attendance.unapproved"), "unsupported_permission", 400);
    Check(fixture.Permissions.Count == 0 && fixture.Audits.Count == 0, "fixed authority cannot invent a permission outside the catalog");
    fixture.Permissions.Add(new(fixture.Role, new Permission("attendance.historical-unknown", "Synthetic unknown permission", false), fixture.Actor.AccountId, fixture.Now.AddDays(-1)));
    await Denied(() => fixture.Assign(), "unsupported_permission", 400);
    Check(fixture.Assignments.Count == 0, "unknown historical role permission cannot be activated by assignment");
}

foreach (var (roleKind, everAssigned, active, code) in new[]
{
    (SystemRoleKind.Admin, false, true, "system_role_is_immutable"),
    (SystemRoleKind.SuperAdmin, false, true, "system_role_is_immutable"),
    (SystemRoleKind.None, true, true, "assigned_role_is_immutable"),
    (SystemRoleKind.None, false, false, "role_inactive")
})
foreach (var actorAuthority in new[] { SystemRoleKind.Admin, SystemRoleKind.SuperAdmin })
{
    var fixture = new DelegationFixture { Authority = actorAuthority };
    fixture.Grants.Add(fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.All));
    if (roleKind != SystemRoleKind.None)
    {
        fixture.Role = Role.CreateSystem(roleKind, fixture.Actor.AccountId, fixture.Now.AddDays(-2));
        DelegationFixture.Set(fixture.Role, "RowVersion", DelegationFixture.InitialVersion.ToArray());
    }
    if (!active) fixture.Role.Deactivate();
    fixture.EverAssigned = everAssigned;
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead), code, 409);
    Check(fixture.Permissions.Count == 0 && fixture.Audits.Count == 0 && fixture.Saves == 0, "immutable/ineligible target has no staged writes");
}

foreach (var reserved in new[] { "Admin", "SUPERADMIN", "super-admin", "A d m i n" })
{
    var fixture = new DelegationFixture();
    await Denied(() => fixture.Service.CreateRoleAsync(new("LEGAL_CODE", reserved), Guid.NewGuid().ToString("N"), fixture.Context, default), "reserved_system_role", 400);
    Check(fixture.AddedRoles.Count == 0, "custom role cannot impersonate system role by name");
}

foreach (var kind in new[] { AccessScopeKind.All, AccessScopeKind.Self, AccessScopeKind.Employee, AccessScopeKind.Department,
    AccessScopeKind.Team, AccessScopeKind.Site, AccessScopeKind.Project })
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    Guid? target = kind is AccessScopeKind.All or AccessScopeKind.Self ? null : Guid.NewGuid();
    var accountVersion = DelegationFixture.Version(fixture.Target);
    var roleVersion = DelegationFixture.Version(fixture.Role);
    var result = await fixture.Assign([new(kind, target)]);
    Check(result.Assignment.Scopes.SequenceEqual([new AccessScopeRequest(kind, target)]), "explicit requested scope retained in response");
    var saved = fixture.Assignments.Single();
    Check(saved.Assignment.UserAccountId == fixture.Target.Id && saved.Assignment.EmployeeId == fixture.Target.EmployeeId &&
          saved.Assignment.AssignedByUserId == fixture.Actor.AccountId, "assignment binds actual recipient and authorizing actor");
    Check(fixture.ScopeLookups.Single() == new AccessScopeRequest(kind, target), "each target validated through repository");
    Check(fixture.ExpectedVersions.Any(x => x.Kind == "Account" && Convert.ToBase64String(x.Bytes) == accountVersion) &&
          fixture.ExpectedVersions.Any(x => x.Kind == "Role" && Convert.ToBase64String(x.Bytes) == roleVersion), "assignment forwards account and role versions");
    Check(fixture.Target.SecurityVersion == 2 && fixture.Target.MfaRequired, "management assignment requires MFA and invalidates target sessions");
    Check(fixture.Audits.Single().EventType == "access.assignment.created" && fixture.Audits.Single().EmployeeScopeId == fixture.Target.EmployeeId,
        "assignment audit includes recipient employee");
    if (kind == AccessScopeKind.Self)
        Check(saved.Scopes.Single().EmployeeId == fixture.Target.EmployeeId, "Self scope derives target employee from recipient account");
    fixture.Grants.RemoveAll(x => x.Permission == AccessPermissionCatalog.AccountsAssign);
    await Denied(() => fixture.Assign([new(kind, target)]));
    Check(fixture.Assignments.Count == 1, "revoked access-administration authority blocks later assignment");
}

{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.MyShift);
    var result = await fixture.Assign([new(AccessScopeKind.Self)]);
    Check(!fixture.Target.MfaRequired && fixture.Target.SecurityVersion == 2, "self-only permission does not require management MFA but invalidates target sessions");
    var scope = fixture.Assignments.Single().Scopes.Single();
    Check(scope.EmployeeId == fixture.Target.EmployeeId && result.Assignment.Scopes.Single().TargetId is null,
        "self target cannot be selected by caller");
}

{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersManage);
    await Denied(() => fixture.Service.AssignRoleAsync(fixture.Actor.AccountId, fixture.AssignmentRequest(), Guid.NewGuid().ToString("N"), fixture.Context, default));
    await Denied(() => fixture.Service.RevokeAssignmentAsync(fixture.Actor.AccountId, Guid.NewGuid(),
        new("Synthetic revoke", DelegationFixture.Version(fixture.ActorAccount), DelegationFixture.Version(fixture.Role)), fixture.Context, default));
    Check(fixture.Assignments.Count == 0 && fixture.ExpectedVersions.Count == 0, "self account assignment/revocation denied before target mutation");
}

foreach (var property in new[] { "Account", "Role" })
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersManage);
    var request = fixture.AssignmentRequest();
    request = property == "Account" ? request with { ExpectedAccountVersion = DelegationFixture.StaleVersion }
        : request with { ExpectedRoleVersion = DelegationFixture.StaleVersion };
    await Denied(() => fixture.Service.AssignRoleAsync(fixture.Target.Id, request, Guid.NewGuid().ToString("N"), fixture.Context, default), "access_version_conflict", 409);
    Check(fixture.ExpectedVersions.Last().Kind == property &&
          Convert.ToBase64String(fixture.ExpectedVersions.Last().Bytes) == DelegationFixture.StaleVersion,
        "stale expected version reached fake repository unchanged");
    Check(fixture.Assignments.Count == 0 && fixture.Audits.Count == 0 && fixture.Saves == 0, "stale assignment fails before writes");
}
{
    var fixture = new DelegationFixture();
    fixture.Grants.Add(fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.All));
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead, DelegationFixture.StaleVersion), "access_version_conflict", 409);
    Check(fixture.ExpectedVersions.Single().Kind == "Role" && fixture.Permissions.Count == 0 && fixture.Audits.Count == 0,
        "stale grant version cannot add a role permission");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    var original = fixture.AssignmentRequest();
    await fixture.Service.AssignRoleAsync(fixture.Target.Id, original, Guid.NewGuid().ToString("N"), fixture.Context, default);
    await Denied(() => fixture.Service.AssignRoleAsync(fixture.Target.Id, original, Guid.NewGuid().ToString("N"), fixture.Context, default), "access_version_conflict", 409);
    Check(fixture.Assignments.Count == 1, "two sequential requests with one snapshot: fake stale version prevents second assignment (not concurrent SQL proof)");
}

foreach (var invalidScopes in new AccessScopeRequest[][]
{
    [new(AccessScopeKind.All), new(AccessScopeKind.Site, Guid.NewGuid())],
    [new(AccessScopeKind.Self, Guid.NewGuid())],
    [new(AccessScopeKind.Employee)],
    [new(AccessScopeKind.Site, Guid.Empty)],
    [new((AccessScopeKind)999, Guid.NewGuid())],
    [new(AccessScopeKind.Self), new(AccessScopeKind.Self)]
})
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersManage);
    await Denied(() => fixture.Assign(invalidScopes), "invalid_access_request", 400);
    Check(fixture.Assignments.Count == 0 && fixture.Audits.Count == 0, "malformed scope rejected without writes");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    fixture.ScopeExists = false;
    await Denied(() => fixture.Assign([new(AccessScopeKind.Site, Guid.NewGuid())]), "scope_target_not_found", 400);
    Check(fixture.Assignments.Count == 0, "missing scope target cannot be assigned");
}
foreach (var identity in new[] { "mobile", "no-mfa", "not-current" })
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    if (identity == "mobile") fixture.Actor = fixture.Actor with { Client = SessionClient.Mobile };
    if (identity == "no-mfa") fixture.Actor = fixture.Actor with { MfaSatisfied = false };
    if (identity == "not-current") fixture.Current = false;
    await Denied(() => fixture.Assign());
    await Denied(() => fixture.Grant(SchedulingPermissions.RostersRead));
    Check(fixture.Assignments.Count == 0 && fixture.Audits.Count == 0, "wrong channel/MFA/current state denies access mutations");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    fixture.ProtectedTarget = true;
    await Denied(() => fixture.Assign());
    Check(fixture.Assignments.Count == 0, "ordinary administrator cannot mutate protected system-role account");
    fixture.SuperAdmin = true;
    await fixture.Assign();
    Check(fixture.Assignments.Count == 1, "current SuperAdmin can administer the protected account with explicit operation authority");
}
{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    await fixture.Assign();
    var assignment = fixture.Assignments.Single().Assignment;
    fixture.ProtectedTarget = true;
    var initialAuditCount = fixture.Audits.Count;
    await Denied(() => fixture.Service.RevokeAssignmentAsync(fixture.Target.Id, assignment.Id,
        new("Synthetic protected target", DelegationFixture.Version(fixture.Target), DelegationFixture.Version(assignment)), fixture.Context, default));
    await Denied(() => fixture.Service.SetMobileAccessAsync(fixture.Target.Id,
        new(false, DelegationFixture.Version(fixture.Target)), fixture.Context, default));
    await Denied(() => fixture.Service.SetWebAccessAsync(fixture.Target.Id,
        new(false, DelegationFixture.Version(fixture.Target)), fixture.Context, default));
    Check(assignment.RevokedAt is null && fixture.Target.SecurityVersion == 2 && fixture.Audits.Count == initialAuditCount,
        "Admin cannot revoke or change either channel of an Admin/SuperAdmin-protected target");
    fixture.SuperAdmin = true;
    await fixture.Service.RevokeAssignmentAsync(fixture.Target.Id, assignment.Id,
        new("Synthetic SuperAdmin reviewed revocation", DelegationFixture.Version(fixture.Target), DelegationFixture.Version(assignment)), fixture.Context, default);
    await fixture.Service.SetWebAccessAsync(fixture.Target.Id,
        new(false, DelegationFixture.Version(fixture.Target)), fixture.Context, default);
    await fixture.Service.SetMobileAccessAsync(fixture.Target.Id,
        new(false, DelegationFixture.Version(fixture.Target)), fixture.Context, default);
    Check(assignment.RevokedAt.HasValue && fixture.Target.SecurityVersion == 5 && fixture.Audits.Count == initialAuditCount + 3,
        "SuperAdmin can explicitly revoke protected target access and disable channels with separate audits and invalidation");
    await Denied(() => fixture.Service.SetWebAccessAsync(fixture.Actor.AccountId,
        new(false, DelegationFixture.Version(fixture.ActorAccount)), fixture.Context, default));
    await Denied(() => fixture.Service.SetMobileAccessAsync(fixture.Actor.AccountId,
        new(false, DelegationFixture.Version(fixture.ActorAccount)), fixture.Context, default));
    Check(fixture.Audits.Count == initialAuditCount + 3, "even SuperAdmin cannot change their own channel entitlements");
}

{
    var fixture = DelegationFixture.Delegator(SchedulingPermissions.RostersRead);
    await fixture.Assign();
    var assignment = fixture.Assignments.Single().Assignment;
    var accountVersion = DelegationFixture.Version(fixture.Target);
    await Denied(() => fixture.Service.RevokeAssignmentAsync(fixture.Target.Id, assignment.Id,
        new("Synthetic explicit reason", accountVersion, DelegationFixture.StaleVersion), fixture.Context, default), "access_version_conflict", 409);
    Check(assignment.RevokedAt is null && fixture.Target.SecurityVersion == 2, "stale assignment cannot revoke or invalidate sessions");
    await fixture.Service.RevokeAssignmentAsync(fixture.Target.Id, assignment.Id,
        new("Synthetic explicit reason", accountVersion, DelegationFixture.Version(assignment)), fixture.Context, default);
    Check(assignment.RevokedByUserId == fixture.Actor.AccountId && assignment.RevocationReason == "Synthetic explicit reason" &&
          !assignment.IsEffectiveAt(fixture.Now), "revocation persists actor/reason and immediately ends effectiveness");
    Check(fixture.Target.SecurityVersion == 3 && fixture.Audits.Last().EventType == "access.assignment.revoked", "revocation invalidates target sessions and audits");
}

foreach (var client in new[] { SessionClient.Web, SessionClient.Mobile })
{
    var fixture = new DelegationFixture();
    var actor = fixture.Actor with { Client = client };
    var self = fixture.GrantOf(SchedulingPermissions.MyShift, AccessScopeKind.All);
    var filtered = AccessPermissionEvaluator.ForSession(actor, [self]);
    Check(filtered.Single().ScopeKind == AccessScopeKind.Self && filtered.Single().TargetId == actor.EmployeeId,
        $"{client} All MyShift capped to authenticated employee");
    Check(AccessPermissionEvaluator.Allows(actor, [self], SchedulingPermissions.MyShift, new(EmployeeId: actor.EmployeeId)), "own MyShift resource allowed");
    Check(!AccessPermissionEvaluator.Allows(actor, [self], SchedulingPermissions.MyShift, new(EmployeeId: Guid.NewGuid())), "another employee MyShift denied");
    Check(AccessPermissionEvaluator.ForSession(actor, [self with { ScopeKind = AccessScopeKind.Employee, TargetId = Guid.NewGuid() }]).Count == 0,
        "self grant aimed at another employee denied");
    foreach (var kind in new[] { AccessScopeKind.Department, AccessScopeKind.Team, AccessScopeKind.Site, AccessScopeKind.Project })
        Check(AccessPermissionEvaluator.ForSession(actor, [self with { ScopeKind = kind, TargetId = Guid.NewGuid() }]).Count == 0,
            "population scope does not infer self entitlement");
    foreach (var code in codes.Where(x => x != SchedulingPermissions.MyShift))
    {
        var management = fixture.GrantOf(code, AccessScopeKind.All);
        Check(AccessPermissionEvaluator.HasAllScope(actor, [management], code) == (client == SessionClient.Web), "mobile cannot receive scheduling management even with All");
        Check(!AccessPermissionEvaluator.HasAllScope(actor with { MfaSatisfied = false }, [management], code), "web/mobile without MFA cannot receive management");
    }
}
{
    var fixture = new DelegationFixture();
    fixture.Actor = fixture.Actor with { Client = SessionClient.Mobile };
    fixture.Authority = SystemRoleKind.None;
    fixture.Grants.Add(fixture.GrantOf(SchedulingPermissions.MyShift, AccessScopeKind.All));
    fixture.Grants.Add(fixture.GrantOf(SchedulingPermissions.RostersManage, AccessScopeKind.All));
    var presented = await fixture.Service.GetCurrentPermissionsAsync(fixture.Context, default);
    Check(presented.Count == 1 && presented.Single().Permission == SchedulingPermissions.MyShift &&
          presented.Single().TargetId == fixture.Actor.EmployeeId, "permission service presents only authenticated employee's self rights to mobile");
    Check(fixture.Audits.Single().EventType == "access.current_permissions.read" &&
          fixture.Audits.Single().EmployeeScopeId == fixture.Actor.EmployeeId, "permission view audit binds employee");
    fixture.Grants.RemoveAll(x => x.Permission == SchedulingPermissions.MyShift);
    Check((await fixture.Service.GetCurrentPermissionsAsync(fixture.Context, default)).Count == 0, "permission service re-reads revoked grants");
}
{
    var fixture = new DelegationFixture { Authority = SystemRoleKind.None };
    fixture.Grants = [fixture.GrantOf(SchedulingPermissions.MyShift, AccessScopeKind.All)];
    var presented = await fixture.Service.GetCurrentPermissionsAsync(fixture.Context, default);
    Check(presented.Single().TargetId == fixture.Actor.EmployeeId, "ordinary web employee keeps current self-permission access without fixed administration authority");
}
foreach (var invalidAccount in new[] { "employee", "version", "status", "session" })
{
    var fixture = new DelegationFixture();
    if (invalidAccount == "employee") fixture.Actor = fixture.Actor with { EmployeeId = Guid.NewGuid() };
    if (invalidAccount == "version") fixture.Actor = fixture.Actor with { SecurityVersion = 2 };
    if (invalidAccount == "status") DelegationFixture.Set(fixture.ActorAccount, "Status", AccountStatus.Suspended);
    if (invalidAccount == "session") fixture.Current = false;
    await Denied(() => fixture.Service.GetCurrentPermissionsAsync(fixture.Context, default), "invalid_authentication", 401);
    Check(fixture.Audits.Count == 0 && fixture.Saves == 0, "invalid current account does not disclose permissions");
}
{
    var fixture = new DelegationFixture();
    var ownDepartment = Guid.NewGuid();
    var otherDepartment = Guid.NewGuid();
    var paired = new[]
    {
        fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.Department, ownDepartment),
        fixture.GrantOf(SchedulingPermissions.ShiftsRead, AccessScopeKind.All),
        fixture.GrantOf(SchedulingPermissions.RostersManage, AccessScopeKind.Department, otherDepartment)
    };
    Check(AccessPermissionEvaluator.Allows(fixture.Actor, paired, SchedulingPermissions.RostersRead, new(DepartmentId: ownDepartment)), "matching paired scope allows roster read");
    Check(!AccessPermissionEvaluator.Allows(fixture.Actor, paired, SchedulingPermissions.RostersRead, new(DepartmentId: otherDepartment)), "cannot borrow another permission's matching department");
    Check(!AccessPermissionEvaluator.HasAllScope(fixture.Actor, paired, SchedulingPermissions.RostersRead), "cannot borrow All from unrelated shift read");
    Check(!AccessPermissionEvaluator.Allows(fixture.Actor, paired, SchedulingPermissions.RostersManage, new(DepartmentId: ownDepartment)), "read scope cannot become management scope");
    foreach (var invalid in new[]
    {
        fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.All, Guid.NewGuid()),
        fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.Employee),
        fixture.GrantOf(SchedulingPermissions.RostersRead, AccessScopeKind.All) with { RoleAssignmentId = Guid.Empty },
        fixture.GrantOf("attendance.nonexistent", AccessScopeKind.All)
    })
        Check(AccessPermissionEvaluator.ForSession(fixture.Actor, [invalid]).Count == 0, "invalid/unknown grant cannot authorize");
}

// Removing receipt-before-version checks, current reauthorization or payload hashing breaks these checks.
{
    var f = DelegationFixture.Delegator(AccessPermissionCatalog.SelfRead);
    DelegationFixture.Set(f.Target, "Status", AccountStatus.PendingAccess);
    var input = f.AssignmentRequest([new(AccessScopeKind.Self)]) with { ConfigureAsInitialAccess = true };
    var saved = await f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default);
    var replay = await f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default);
    Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(replay) && f.Assignments.Count == 1 &&
        f.Target.InitialRoleAssignmentId == saved.Assignment.Id && f.Receipts.Count == 1,
        "identical initial Self assignment retry returns original assignment/versions without another assignment");
    Check(!f.Receipts.Single().ProtectedResultReference!.Contains(input.Reason, StringComparison.Ordinal) &&
        f.Evidence.Any(x => x.Purpose.Contains(":receipt:", StringComparison.Ordinal)), "receipt response is protected separately from SQL");
    Check(f.Audits.Last().EventType == "access.assignment.created.replay", "authorized replay is audited");
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input with { Reason = "Different access purpose" },
        "a01-initial-0001", f.Context, default), "idempotency_key_reused", 409);
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input with { Scopes = [new(AccessScopeKind.All)] },
        "a01-initial-0001", f.Context, default), "idempotency_key_reused", 409);
    f.Current = false;
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default));
    f.Current = true;
    f.Grants.RemoveAll(x => x.Permission == AccessPermissionCatalog.RolePermissionsManage);
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default));
    f.Grants.Add(f.GrantOf(AccessPermissionCatalog.RolePermissionsManage, AccessScopeKind.All));
    f.ProtectedTarget = true;
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default));
    f.ProtectedTarget = false;
    f.EmployeeActive = false;
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default), "account_ineligible", 409);
    f.EmployeeActive = true;
    f.Role.Deactivate();
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, input, "a01-initial-0001", f.Context, default), "role_inactive", 409);
    Check(f.Assignments.Count == 1 && f.Receipts.Count == 1, "denied replays never duplicate access");
}
{
    var f = new DelegationFixture();
    var input = new CreateAccessRoleRequest("SYNTHETIC_RECEIPT", "Synthetic receipt role");
    var saved = await f.Service.CreateRoleAsync(input, "a01-role-create-0001", f.Context, default);
    var replay = await f.Service.CreateRoleAsync(input, "a01-role-create-0001", f.Context, default);
    Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(replay) && f.AddedRoles.Count == 1,
        "empty-role creation retry creates one role");
    await Denied(() => f.Service.CreateRoleAsync(input with { Name = "Different role" }, "a01-role-create-0001",
        f.Context, default), "idempotency_key_reused", 409);
    f.Actor = f.Actor with { AccountId = Guid.NewGuid() };
    await f.Service.CreateRoleAsync(input, "a01-role-create-0001", f.Context, default);
    Check(f.AddedRoles.Count == 2 && f.Receipts.Count == 2, "another actor's same key cannot replay the first actor's receipt");
}
{
    var f = new DelegationFixture();
    var input = new GrantRolePermissionsRequest([AccessPermissionCatalog.SelfRead], DelegationFixture.Version(f.Role), "Synthetic permission review");
    var saved = await f.Service.GrantRolePermissionsAsync(f.Role.Id, input, "a01-role-grant-0001", f.Context, default);
    var replay = await f.Service.GrantRolePermissionsAsync(f.Role.Id, input, "a01-role-grant-0001", f.Context, default);
    Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(replay) && f.Permissions.Count == 1,
        "permission-grant retry returns original role version without duplicating a grant");
    await Denied(() => f.Service.GrantRolePermissionsAsync(f.Role.Id, input with { Permissions = [AccessPermissionCatalog.SelfSessionsManage] },
        "a01-role-grant-0001", f.Context, default), "idempotency_key_reused", 409);
    await f.Service.CreateRoleAsync(new("SYNTHETIC_OTHER_ACTION", "Synthetic other action"), "a01-role-grant-0001", f.Context, default);
    Check(f.Receipts.Count == 2, "same actor/key on distinct actions has separate receipts");
    DelegationFixture.Set(f.Receipts.First(), "ExpiresAt", f.Now.AddSeconds(-1));
    await Denied(() => f.Service.GrantRolePermissionsAsync(f.Role.Id, input, "a01-role-grant-0001", f.Context, default),
        "idempotency_result_unavailable", 409);
}
foreach (var key in new[] { "", "short", "key with spaces", "key\ncontrol", new string('x', 101) })
{
    var f = DelegationFixture.Delegator(AccessPermissionCatalog.SelfRead);
    await Denied(() => f.Service.AssignRoleAsync(f.Target.Id, f.AssignmentRequest(), key, f.Context, default), "invalid_idempotency_key", 400);
    Check(f.Assignments.Count == 0 && f.Receipts.Count == 0, "invalid key cannot stage an assignment");
}
Console.WriteLine($"PASS {checks} real access-service/evaluator checks with explicit synthetic in-memory collaborators. No SQL, authenticated HTTP, private storage, transaction rollback or concurrent SQL-write acceptance claimed.");

public class DelegationProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, DelegationProxy>(); ((DelegationProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class DelegationClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero); }
public sealed record ExpectedVersion(string Kind, object Entity, byte[] Bytes);
public sealed record SavedAssignment(UserRoleAssignment Assignment, IReadOnlyList<UserAccessScope> Scopes);
public sealed record SavedAccessEvidence(byte[] Content, string Purpose, PrivateFileReference Reference);
public sealed class DelegationFixture
{
    public static readonly byte[] InitialVersion = BitConverter.GetBytes(1L);
    public const string StaleVersion = "//////////8=";
    public DateTimeOffset Now => new DelegationClock().GetUtcNow();
    public AuthenticatedAccount Actor;
    public UserAccount ActorAccount, Target;
    public Role Role;
    public Guid AuthorityAssignmentId = Guid.NewGuid();
    public SystemRoleKind Authority = SystemRoleKind.Admin;
    public bool FixedRoleCurrent = true;
    public List<PermissionGrant> Grants = [];
    public List<RolePermission> Permissions = [];
    public List<Role> AddedRoles = [];
    public List<SavedAssignment> Assignments = [];
    public List<AuditEvent> Audits = [];
    public List<ExpectedVersion> ExpectedVersions = [];
    public List<AccessScopeRequest> ScopeLookups = [];
    public List<SavedAccessEvidence> Evidence = [];
    public List<IdempotencyReceipt> Receipts = [];
    public bool EmployeeActive = true;
    public bool FailResultStorage;
    public bool FailStorage;
    public int StorageFactoryCalls;
    public bool Current = true, ProtectedTarget, EverAssigned, ScopeExists = true;
    public bool SuperAdmin { get => Authority == SystemRoleKind.SuperAdmin; set => Authority = value ? SystemRoleKind.SuperAdmin : SystemRoleKind.Admin; }
    public int Saves;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-access-delegation-verification");
    public AccessManagementService Service;
    private long versionCounter = 10;
    private readonly HashSet<object> pendingVersionChanges = [];
    public DelegationFixture(bool failStorageInitialization = false)
    {
        ActorAccount = SyntheticAccount(); Target = SyntheticAccount();
        Actor = new(ActorAccount.Id, ActorAccount.EmployeeId, Guid.NewGuid(), 1, SessionClient.Web, true);
        Role = new("SYNTHETIC_ROSTER", "Synthetic roster role", Actor.AccountId, Now.AddDays(-2));
        Set(Role, "RowVersion", InitialVersion.ToArray());
        Grants = new[] { AccessPermissionCatalog.RolesRead, AccessPermissionCatalog.RolesCreate,
            AccessPermissionCatalog.RolePermissionsManage, AccessPermissionCatalog.AccountsRead,
            AccessPermissionCatalog.AccountsAssign, AccessPermissionCatalog.AccountsMobileManage,
            AccessPermissionCatalog.AccountsWebManage }.Select(x => GrantOf(x, AccessScopeKind.All)).ToList();
        var repository = DelegationProxy.Make<IAccessManagementRepository>(Call);
        var reader = DelegationProxy.Make<IAccountAccessReader>((method, _) => method.Name == "GetGrantsAsync"
            ? Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()) : throw new Exception(method.Name));
        var current = DelegationProxy.Make<ICurrentAuthentication>((method, _) => method.Name == "Require" ? Actor : throw new Exception(method.Name));
        IPrivateFileStore CreateFiles()
        {
            StorageFactoryCalls++;
            if (failStorageInitialization) throw new AuthenticationFailure("private_file_storage_unavailable", 503);
            return DelegationProxy.Make<IPrivateFileStore>((method, args) =>
            {
                if (method.Name == "ReadAsync")
                {
                    var readReference = (PrivateFileReference)args[0]!;
                    var saved = Evidence.Single(x => x.Reference == readReference && x.Purpose == (string)args[1]!);
                    return Task.FromResult(saved.Content.ToArray());
                }
                if (method.Name != "WriteAsync") throw new Exception("Unexpected access file operation");
                if (FailStorage || (FailResultStorage && ((string)args[1]!).Contains(":receipt:", StringComparison.Ordinal))) return Task.FromException<PrivateFileReference>(new AuthenticationFailure("private_file_storage_unavailable", 503));
                var bytes = ((byte[])args[0]!).ToArray();
                var reference = new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"),
                    Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-access-key", bytes.Length);
                Evidence.Add(new(bytes, (string)args[1]!, reference));
                return Task.FromResult(reference);
            });
        }
        Service = new(repository, reader, current, new DelegationClock(), new Lazy<IPrivateFileStore>(CreateFiles));
    }
    public static DelegationFixture Delegator(string permission)
    {
        var result = new DelegationFixture();
        result.Grants.Add(result.GrantOf(permission, AccessScopeKind.All));
        result.Permissions.Add(result.Permission(permission));
        return result;
    }
    public PermissionGrant GrantOf(string code, AccessScopeKind kind, Guid? target = null) =>
        new(code, AccessPermissionCatalog.Find(code)?.Sensitive ?? false, AuthorityAssignmentId, kind, target);
    public RolePermission Permission(string code)
    {
        var definition = AccessPermissionCatalog.Require(code);
        return new(Role, new Permission(code, definition.Description, definition.Sensitive), Actor.AccountId, Now.AddDays(-1));
    }
    public Task<AccessRoleResponse> Grant(string code, string? expectedVersion = null, string reason = "Synthetic explicit module access") =>
        Service.GrantRolePermissionsAsync(Role.Id, new([code], expectedVersion ?? Version(Role), reason), Guid.NewGuid().ToString("N"), Context, default);
    public AssignAccessRoleRequest AssignmentRequest(AccessScopeRequest[]? scopes = null) =>
        new(Role.Id, scopes ?? [new(AccessScopeKind.All)], Now, null, Version(Target), Version(Role), Reason: "Synthetic explicit role assignment");
    public Task<AccessAssignmentResult> Assign(AccessScopeRequest[]? scopes = null) =>
        Service.AssignRoleAsync(Target.Id, AssignmentRequest(scopes), Guid.NewGuid().ToString("N"), Context, default);
    public static string Version(object entity) => Convert.ToBase64String((byte[])entity.GetType().GetProperty("RowVersion")!.GetValue(entity)!);
    public static void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
    private UserAccount SyntheticAccount()
    {
        // Rehydrates only the fields this application service reads, deliberately bypassing onboarding
        // for a fake persisted fixture. This is not production activation or lifecycle verification.
        var account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), nonPublic: true)!;
        Set(account, "Id", Guid.NewGuid()); Set(account, "EmployeeId", Guid.NewGuid());
        Set(account, "CreatedAt", Now.AddDays(-10)); Set(account, "Status", AccountStatus.Active);
        Set(account, "SecurityVersion", 1); Set(account, "RowVersion", InitialVersion.ToArray());
        return account;
    }
    private object? Call(MethodInfo method, object?[] args)
    {
        switch (method.Name)
        {
            case "InTransactionAsync": return ((Delegate)args[0]!).DynamicInvoke(args[1]); // No transaction/rollback simulation.
            case "IsCurrentAdministratorAsync": return Task.FromResult(Current);
            case "IsCurrentSuperAdminAsync": return Task.FromResult(SuperAdmin);
            case "IsCurrentSystemRoleAssignmentAsync": return Task.FromResult(Current && FixedRoleCurrent &&
                Authority is SystemRoleKind.Admin or SystemRoleKind.SuperAdmin &&
                (Guid)args[0]! == Actor.AccountId && (Guid)args[1]! == AuthorityAssignmentId);
            case "GetAssignmentRoleCodeAsync": return Task.FromResult<string?>((Guid)args[0]! == AuthorityAssignmentId
                ? Authority == SystemRoleKind.None ? "Admin" : Authority.ToString() : "SYNTHETIC_CUSTOM");
            case "HasSystemRoleAsync": return Task.FromResult(ProtectedTarget);
            case "FindRoleAsync": return Task.FromResult((Guid)args[0]! == Role.Id ? Role : null);
            case "RoleHasEverBeenAssignedAsync": return Task.FromResult(EverAssigned || Assignments.Any(x => x.Assignment.RoleId == (Guid)args[0]!));
            case "IsEmployeeActiveForAccessAsync": return Task.FromResult(EmployeeActive);
            case "IsEmployeeEligibleForSystemRoleGrantAsync": return Task.FromResult(EmployeeActive);
            case "FindReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)args[0]! && r.Operation == (string)args[1]! && r.Key == (string)args[2]!));
            case "AddReceipt": Receipts.Add((IdempotencyReceipt)args[0]!); return null;
            case "RoleCodeExistsAsync": return Task.FromResult(false);
            case "GetRolePermissionsAsync": return Task.FromResult<IReadOnlyList<RolePermission>>(Permissions.ToArray());
            case "GetOrAddPermissionAsync":
                var definition = (AccessPermissionDefinition)args[0]!;
                return Task.FromResult(new Permission(definition.Code, definition.Description, definition.Sensitive));
            case "FindAccountAsync": return Task.FromResult((Guid)args[0]! == Target.Id ? Target : (Guid)args[0]! == ActorAccount.Id ? ActorAccount : null);
            case "FindAssignmentAsync": return Task.FromResult(Assignments.SingleOrDefault(x => x.Assignment.UserAccountId == (Guid)args[0]! && x.Assignment.Id == (Guid)args[1]!)?.Assignment);
            case "ScopeTargetExistsAsync": ScopeLookups.Add((AccessScopeRequest)args[0]!); return Task.FromResult(ScopeExists);
            case "ExpectRoleVersion": case "ExpectAccountVersion": case "ExpectAssignmentVersion":
                var entry = new ExpectedVersion(method.Name[6..^7], args[0]!, ((byte[])args[1]!).ToArray());
                ExpectedVersions.Add(entry);
                pendingVersionChanges.Add(entry.Entity);
                if (Convert.ToBase64String(entry.Bytes) != Version(entry.Entity)) throw new AuthenticationFailure("access_version_conflict", 409);
                return null;
            case "AddRole": AddedRoles.Add((Role)args[0]!); return null;
            case "AddRolePermission": Permissions.Add((RolePermission)args[0]!); return null;
            case "AddAssignment":
                var assignment = (UserRoleAssignment)args[0]!;
                Set(assignment, "RowVersion", BitConverter.GetBytes(++versionCounter));
                Assignments.Add(new(assignment, (IReadOnlyList<UserAccessScope>)args[1]!)); return null;
            case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
            case "SaveChangesAsync":
                Saves++;
                foreach (var changed in pendingVersionChanges) Set(changed, "RowVersion", BitConverter.GetBytes(++versionCounter));
                pendingVersionChanges.Clear();
                return Task.CompletedTask;
            default: throw new Exception($"Unexpected fake repository call {method.Name}; add an explicit fixture behavior if needed.");
        }
    }
}
