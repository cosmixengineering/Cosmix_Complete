using System.Data;
using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Authentication.Bootstrap;

public sealed class BootstrapRepository(CosmixDbContext db) : IBootstrapRepository
{
    public async Task<BootstrapTarget> GetTargetAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME()";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(0) || reader.IsDBNull(1))
            throw new InvalidOperationException("Cannot establish the database identity.");
        var target = new BootstrapTarget(reader.GetString(0), reader.GetString(1));
        if (string.IsNullOrWhiteSpace(target.Server) || string.IsNullOrWhiteSpace(target.Database) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(target.Database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bootstrap requires an identified application database.");
        return target;
    }

    public async Task<T> InEmptyDatabaseTransactionAsync<T>(BootstrapTarget expected,
        Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Bootstrap requires a fresh context.");
        if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Apply the reviewed bootstrap migration before initialization.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committing = false;
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'cosmix:authentication',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51001, 'Authentication is temporarily busy.', 1;
                """, ct);
            if (await GetTargetAsync(ct) != expected)
                throw new InvalidOperationException("Database identity differs from the confirmed target.");
            await RequireEmptyDatabaseAsync(ct);
            using var scope = new BootstrapWriteScope();
            var result = await action(ct);
            committing = true;
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception error)
        {
            var rolledBack = false;
            try { await transaction.RollbackAsync(CancellationToken.None); rolledBack = !committing; }
            catch (Exception) { /* A disconnected or uncertain commit must retain its external evidence. */ }
            throw new BootstrapTransactionException(rolledBack, error);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private async Task RequireEmptyDatabaseAsync(CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        // Include unmapped business tables too; only EF's migration ledger is exempt.
        command.CommandText = """
            SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            WHERE t.is_ms_shipped=0 AND NOT (s.name=N'dbo' AND t.name=N'__EFMigrationsHistory')
            ORDER BY s.name, t.name
            """;
        var tables = new List<(string Schema, string Name)>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) tables.Add((reader.GetString(0), reader.GetString(1)));
        if (tables.Count == 0) throw new InvalidOperationException("The application schema is missing.");
        foreach (var table in tables)
        {
            command.CommandText = $"SELECT TOP (1) 1 FROM {Quote(table.Schema)}.{Quote(table.Name)} WITH (UPDLOCK,HOLDLOCK)";
            if (await command.ExecuteScalarAsync(ct) is not null)
                throw new InvalidOperationException("Bootstrap requires an empty business database.");
        }
    }

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    public void AddEmployee(Employee employee) => db.Add(employee);
    public void AddSubmission(EmployeeSubmission submission) => db.Add(submission);
    public void AddDecision(EmployeeOnboardingDecision decision) => db.Add(decision);
    public void AddAccount(UserAccount account) => db.Add(account);
    public void AddAccess(BootstrapAccess access)
    {
        db.AddRange(access.Roles); db.AddRange(access.Permissions); db.AddRange(access.Grants);
        db.Add(access.Assignment); db.Add(access.Scope);
    }
    public void AddBinding(LoginPhoneBinding binding) => db.Add(binding);
    public void AddAudit(AuditEvent audit) => db.Add(audit);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
