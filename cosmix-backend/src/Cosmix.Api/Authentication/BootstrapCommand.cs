using System.Security.Cryptography;
using System.Security.Principal;
using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Infrastructure.Authentication.Bootstrap;

namespace Cosmix.Api.Authentication;

public static class BootstrapCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine("bootstrap-superadmin: interactive owner self-initialization on an empty, migrated database.");
            Console.WriteLine("No arguments are accepted. Identity/approval JSON, evidence, snapshot directory and a backed-up 32-byte key must be in private local paths outside Git.");
            Console.WriteLine("JSON fields: Employee (EmployeeDraftDetails), LoginPhone, OwnerLegalName, OwnerIdentityDocumentNumber, OwnerApprovalReference, OwnerApprovedAt, AuthorizesSelfInitialization, AuthorizesFixedRoleGrants, OwnerApprovalEvidenceFile.");
            Console.WriteLine("Configure ConnectionStrings:CosmixDatabase, WebAuthentication:MinimumPasswordLength and MobileAuthentication:BusinessTimeZoneId privately. No server, session or SMS is started by this command.");
            return 0;
        }
        if (args.Length != 0 || Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("Bootstrap requires an interactive private console and accepts no credentials or other arguments.");
            return 2;
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        BootstrapRequest? request = null;
        try
        {
            Console.WriteLine("The real CEO/owner must authorize these fixed-role grants and enter their own password.");
            Console.WriteLine(string.Join(", ", OwnerBootstrapService.FixedPermissionCodes));
            request = await PrivateBootstrapSnapshotStore.ReadInputAsync(Read("Private identity/approval JSON path"), cancellation.Token);
            var storage = new BootstrapStorageOptions(Read("Private snapshot directory"), Read("Private durable 32-byte key file"), Read("Stable key reference (not key material)"));
            if (Read("Type KEY BACKED UP to attest the encryption key has durable private backup") != "KEY BACKED UP") return 2;
            if (Read("Type OWNER SELF-INITIALIZATION to authorize grants on your own identity") != "OWNER SELF-INITIALIZATION") return 2;

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], ApplicationName = typeof(BootstrapCommand).Assembly.GetName().Name });
            builder.Logging.ClearProviders();
            builder.Services.AddOwnerBootstrap(builder.Configuration, storage);
            using var provider = builder.Services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<OwnerBootstrapService>();
            var repository = scope.ServiceProvider.GetRequiredService<IBootstrapRepository>();
            var target = await repository.GetTargetAsync(cancellation.Token);
            Console.WriteLine($"Actual SQL target: {target.Confirmation}");
            if (Read("Type the complete SQL target exactly to confirm") != target.Confirmation) return 2;
            var password = ReadPassword("Owner password", cancellation.Token);
            var confirmation = ReadPassword("Repeat owner password", cancellation.Token);
            if (!string.Equals(password, confirmation, StringComparison.Ordinal)) throw new InvalidOperationException("Passwords differ.");
            string executor;
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                executor = "windows:" + identity.User!.Value;
            }
            else executor = "local:" + Environment.UserName;
            var result = await service.InitializeAsync(request, password, target, executor, cancellation.Token);
            Console.WriteLine($"Initialized account {result.AccountId}; employee {result.EmployeeId}. Snapshot: {result.SnapshotReference}");
            Console.WriteLine("Web password plus registered-number SMS proof is still required. Mobile access is disabled.");
            return 0;
        }
        catch (BootstrapFailedException error)
        {
            Console.Error.WriteLine(error.CommitUncertain ? "Commit outcome is uncertain. Inspect the database before retrying." : "Bootstrap failed; the database transaction was rolled back.");
            if (error.RetainedSnapshotReference is not null)
                Console.Error.WriteLine($"Retained encrypted snapshot: {error.RetainedSnapshotReference}. Preserve until the database outcome is reconciled.");
            return 1;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Bootstrap cancelled."); return 2; }
        catch (Exception)
        {
            // SQL, JSON and filesystem exception messages can contain identities, paths or connection details.
            Console.Error.WriteLine("Bootstrap refused or failed. Check private inputs, permissions, target, migrations and password policy; no success was reported.");
            return 1;
        }
        finally
        {
            if (request is not null) CryptographicOperations.ZeroMemory(request.OwnerApprovalEvidence);
            Console.CancelKeyPress -= cancel;
        }
    }

    private static string Read(string prompt)
    {
        Console.Write(prompt + ": ");
        return Console.ReadLine()?.Trim() ?? throw new OperationCanceledException();
    }

    private static string ReadPassword(string prompt, CancellationToken ct)
    {
        Console.Write(prompt + ": ");
        var buffer = new char[1024];
        var length = 0;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(buffer, 0, length); }
                if (key.Key == ConsoleKey.Backspace) { if (length > 0) buffer[--length] = '\0'; continue; }
                if (char.IsControl(key.KeyChar)) continue;
                if (length == buffer.Length) throw new InvalidOperationException("Password exceeds the supported length.");
                buffer[length++] = key.KeyChar;
            }
        }
        finally { Array.Clear(buffer); }
    }
}
