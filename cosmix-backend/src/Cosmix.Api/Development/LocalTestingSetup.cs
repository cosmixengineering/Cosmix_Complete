using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Api.Development;

/// <summary>Explicit local fixture command, never registered as an HTTP service or automatic startup seeder.</summary>
internal static class LocalTestingSetup
{
    internal static async Task<int> RunAsync(IConfiguration config)
    {
        var folder = PrivateBootstrapSnapshotStore.RequirePrivatePath(config["LocalTesting:PrivateDirectory"]!, true);
        var storage = new BootstrapStorageOptions(Path.Combine(folder, "snapshots"), Path.Combine(folder, "keys", "snapshot.key"), "local-testing-v1");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddInfrastructure(config);
        services.AddOwnerBootstrap(config, storage);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Review missing migrations before test setup.");
        var fresh = LocalTesting.FreshTarget(config);
        if (fresh is not null)
        {
            // The fresh profile never creates/migrates a catalog on connection failure.
            await LocalTesting.VerifyFreshDatabaseAsync(db, fresh, CancellationToken.None);
        }
        else if (await db.Database.CanConnectAsync())
        {
            // Existing data, credentials and migration history are never reset by this command.
            await LocalTesting.VerifyDatabaseAsync(provider);
            Console.WriteLine("Cosmix_Testing already initialized; no database or credential changes made.");
            return 0;
        }

        if (fresh is null)
        {
            Console.WriteLine("Creating Cosmix_Testing using the existing migration chain:");
            foreach (var migration in db.Database.GetMigrations()) Console.WriteLine(migration);
            await db.Database.MigrateAsync();
        }
        var repository = scope.ServiceProvider.GetRequiredService<IBootstrapRepository>();
        var target = await repository.GetTargetAsync(CancellationToken.None);
        if (target.Database != LocalTesting.Database) throw new InvalidOperationException("Unexpected test database target.");
        var passwords = LocalTesting.Accounts.Select(_ => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))).ToArray();
        var credentialsPath = Path.Combine(folder, "Test-Accounts.json");
        async Task RetainCredentials()
        {
            await using (var file = new FileStream(credentialsPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(file, LocalTesting.Accounts.Select((a, i) => new { a.Email, a.Name, a.Phone, Password = passwords[i] }), new JsonSerializerOptions { WriteIndented = true });
            PrivateBootstrapSnapshotStore.RequirePrivatePath(credentialsPath, false);
        }
        if (fresh is null) await RetainCredentials();
        var hasher = scope.ServiceProvider.GetRequiredService<IWebPasswordHasher>();
        var snapshots = scope.ServiceProvider.GetRequiredService<IBootstrapSnapshotStore>();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var now = DateTimeOffset.UtcNow;
        var actor = "local-testing:synthetic-fixture-not-real-owner";
        var hrId = Guid.NewGuid();

        await repository.InEmptyDatabaseTransactionAsync(target, async ct =>
        {
            if (fresh is not null)
            {
                // Existing bootstrap code locks and verifies every business table before this callback.
                await LocalTesting.VerifyFreshDatabaseAsync(db, fresh, ct);
                await RetainCredentials();
            }
            var owner = CreateEmployee(0, false);
            db.Add(owner);
            await db.SaveChangesAsync(ct);
            var request = new BootstrapRequest(Details(0), LocalTesting.Accounts[0].Phone, LocalTesting.Accounts[0].Name,
                "TEST-PASSPORT-1", "PM-authorized synthetic test fixture; NOT real CEO authorization", now, true, true,
                Encoding.UTF8.GetBytes("Synthetic authentication fixture in Cosmix_Testing only. No real owner is represented."));
            var snapshot = await snapshots.WriteAsync(new(owner.Id, target, request, actor, now, OwnerBootstrapService.FixedPermissionCodes), ct);
            var submission = owner.SubmitForOwnerBootstrap(actor, now, snapshot.StorageKey, snapshot.ObjectVersion, snapshot.Hash, snapshot.KeyReference, 1);
            db.Add(submission);
            await db.SaveChangesAsync(ct);
            var approval = owner.ApproveOwnerBootstrap(submission, actor, request.OwnerApprovalReference, now);
            db.Add(approval);
            await db.SaveChangesAsync(ct);
            var ownerAccount = new UserAccount(owner, approval, LocalTesting.Accounts[0].Email, now);
            db.Add(ownerAccount);
            await db.SaveChangesAsync(ct);
            var superAdmin = Role.CreateSystem(SystemRoleKind.SuperAdmin, ownerAccount.Id, now);
            var admin = Role.CreateSystem(SystemRoleKind.Admin, ownerAccount.Id, now);
            var hr = new Role("HR", "HR", ownerAccount.Id, now);
            db.AddRange(superAdmin, admin, hr);
            var permissions = OwnerBootstrapService.FixedPermissionCodes.Select(code =>
            {
                var p = AccessPermissionCatalog.Require(code);
                return new Permission(p.Code, p.Description, p.Sensitive);
            }).ToArray();
            db.AddRange(permissions);
            var hrCodes = new HashSet<string>(StringComparer.Ordinal)
            {
                "auth.self.read", "auth.self.sessions.manage", "hr.organization.read", "hr.holidays.read", "hr.sites.read",
                "hr.employees.read", "hr.employees.drafts.manage", "hr.employees.submit", "hr.employees.basic.manage",
                "hr.employees.financial.read", "hr.employees.financial.manage",
                "hr.employees.medical.read", "hr.employees.medical.manage",
                "hr.employees.bank.read", "hr.employees.bank.request",
                "hr.employees.changes.request", "hr.employees.documents.read", "hr.employees.documents.manage",
                "hr.employees.lifecycle.read", "hr.employees.lifecycle.request"
            };
            foreach (var role in new[] { superAdmin, admin, hr })
                foreach (var permission in permissions.Where(p => role != hr || hrCodes.Contains(p.Code)))
                    db.Add(new RolePermission(role, permission, ownerAccount.Id, now));
            await db.SaveChangesAsync(ct);
            await Provision(ownerAccount, submission, superAdmin, 0, ownerAccount.Id, ct);

            // Preallocate the HR fixture identity so the synthetic submitter and reviewer are distinct,
            // with both actors present when the single guarded transaction commits.
            var adminAccount = await AddEmployeeAccount(1, hrId, ownerAccount.Id, superAdmin, admin, null, ct);
            await AddEmployeeAccount(2, ownerAccount.Id, adminAccount.Id, admin, hr, hrId, ct);
            db.Add(new AuditEvent("development.fixture.seeded", actor, "local-testing", "Database", LocalTesting.Database,
                now, Guid.NewGuid().ToString("N"), protectedChangesReference: snapshot.StorageKey));
            await db.SaveChangesAsync(ct);
            return true;

            async Task<UserAccount> AddEmployeeAccount(int index, Guid submitter, Guid approver, Role approvalRole,
                Role accessRole, Guid? fixedId, CancellationToken token)
            {
                var employee = CreateEmployee(index, index == 2);
                db.Add(employee);
                await db.SaveChangesAsync(token);
                var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    Kind = "Synthetic authentication fixture; not a completed hiring form", Employee = Details(index),
                    Submitter = submitter, Approver = approver, CreatedAt = now
                }), $"local-testing/{employee.Id:N}/submission", token);
                var submitted = employee.SubmitForApproval(submitter, now, evidence.StorageKey, evidence.ObjectVersion,
                    evidence.Sha256, evidence.KeyReference, 1);
                db.Add(submitted);
                await db.SaveChangesAsync(token);
                var approved = employee.DecideSubmission(submitted, OnboardingDecision.Approve, approver, approvalRole.Id,
                    "Synthetic test fixture only", now);
                db.Add(approved);
                await db.SaveChangesAsync(token);
                var account = new UserAccount(employee, approved, LocalTesting.Accounts[index].Email, now);
                if (fixedId.HasValue) db.Entry(account).Property(a => a.Id).CurrentValue = fixedId.Value;
                db.Add(account);
                await db.SaveChangesAsync(token);
                await Provision(account, submitted, accessRole, index, approver, token);
                return account;
            }

            async Task Provision(UserAccount account, EmployeeSubmission submitted, Role role, int index, Guid grantedBy, CancellationToken token)
            {
                var assignment = new UserRoleAssignment(account, role, grantedBy, now, null);
                var access = new UserAccessScope(assignment, AccessScopeKind.All);
                db.AddRange(assignment, access);
                await db.SaveChangesAsync(token);
                account.ConfigureInitialAccess(assignment, access, now);
                var binding = new LoginPhoneBinding(account, LocalTesting.Accounts[index].Phone, submitted.Id, now);
                account.ProvisionInitialWebCredentials(LocalTesting.Accounts[index].Email, hasher.Hash(account, passwords[index]),
                    WebPasswordFormats.IdentityV3, binding, assignment, access, now);
                account.RequireMfa();
                if (index == 2) account.SetMobileAccess(true, now);
                db.Add(binding);
                await db.SaveChangesAsync(token);
            }
        }, CancellationToken.None);
        await LocalTesting.VerifyDatabaseAsync(provider);
        Console.WriteLine("Created exactly 3 synthetic accounts. Passwords are in the private Test-Accounts.json, not logs or Git.");
        Console.WriteLine("No real SMS sent. Cosmix_Development was not modified.");
        return 0;

        Employee CreateEmployee(int index, bool mobile)
        {
            var employee = new Employee(now, actor);
            employee.UpdateDraft(Details(index));
            employee.SelectLoginPhone(LocalTesting.Accounts[index].Phone, mobile);
            return employee;
        }

        EmployeeDraftDetails Details(int index) => new()
        {
            FullLegalName = LocalTesting.Accounts[index].Name, IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityDocumentNumber = $"TEST-PASSPORT-{index + 1}", PersonalPhone = LocalTesting.Accounts[index].Phone,
            OfficialEmail = LocalTesting.Accounts[index].Email, JoinedOn = DateOnly.FromDateTime(now.UtcDateTime)
        };
    }

}
