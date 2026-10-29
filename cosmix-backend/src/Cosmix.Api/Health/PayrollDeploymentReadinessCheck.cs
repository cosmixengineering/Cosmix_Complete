using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Cosmix.Api.Health;

/// <summary>
/// Opt-in operational probe, separate from liveness and business-period approval.
/// Reads configuration and schema metadata only. Never dispatches OTP/jobs, migrates,
/// reads salaries, initializes an owner, or claims carrier/device/business acceptance.
/// </summary>
public sealed class PayrollDeploymentReadinessCheck(IServiceScopeFactory scopes, IHostEnvironment environment)
    : IHealthCheck
{
    public sealed record ConfigurationSnapshot(bool MobileEnabled, bool MobileValid,
        bool WebEnabled, bool WebValid, bool OtpConfigured, bool SmsConfigured,
        bool ExternalSmsSelected, bool PrivateFilesConfigured, bool JobsEnabled, bool JobsValid,
        bool Production);

    public static IReadOnlyList<string> EvaluateConfiguration(ConfigurationSnapshot state)
    {
        var codes = new List<string>();
        if (!state.MobileEnabled || !state.MobileValid) codes.Add("mobile_authentication_unavailable");
        if (!state.WebEnabled || !state.WebValid) codes.Add("web_authentication_unavailable");
        if (!state.OtpConfigured) codes.Add("otp_crypto_unavailable");
        if (!state.SmsConfigured || (state.Production && !state.ExternalSmsSelected)) codes.Add("sms_login_recovery_unavailable");
        if (!state.PrivateFilesConfigured) codes.Add("private_evidence_storage_unavailable");
        if (!state.JobsEnabled || !state.JobsValid) codes.Add("report_notification_dispatch_unavailable");
        return codes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var schemaUnavailable = false;
        using var scope = scopes.CreateScope();
        try
        {
            var services = scope.ServiceProvider;
            var mobile = services.GetRequiredService<MobileAuthenticationOptions>();
            var web = services.GetRequiredService<WebAuthenticationOptions>();
            var jobs = services.GetRequiredService<AttendanceJobOptions>();
            var sms = services.GetRequiredService<IOptions<SmsGatewayOptions>>().Value;
            issues.AddRange(EvaluateConfiguration(new(mobile.Enabled, mobile.IsValid(), web.Enabled, web.IsValid(),
                services.GetRequiredService<IOtpProtector>().IsConfigured,
                services.GetRequiredService<IOtpSmsSender>().IsConfigured,
                sms.Provider == SmsGatewayOptions.GenericHttpProvider,
                PrivateStorageConfigured(services),
                jobs.Enabled, jobs.IsValid(), environment.IsProduction())));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { issues.Add("deployment_configuration_unavailable"); }

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
            if (db.Database.HasPendingModelChanges()) issues.Add("model_migration_missing");
            var expected = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            if (expected.Count == 0) issues.Add("migration_manifest_missing");
            if (expected.Except(applied).Any()) issues.Add("database_migrations_pending");
            if (applied.Except(expected).Any()) issues.Add("database_migrations_unknown");

            // History alone cannot detect a deleted table/column. Compare SQL metadata with
            // mapped payroll columns; no employee/payment/bank values leave the database.
            var required = db.Model.GetEntityTypes()
                .Where(e => e.ClrType.Namespace?.StartsWith("Cosmix.Domain.HR.Payroll", StringComparison.Ordinal) == true)
                .SelectMany(e => e.GetTableName() is { } table
                    ? e.GetProperties().Select(p => (Schema: e.GetSchema() ?? "dbo", Table: table,
                        Column: p.GetColumnName(Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, e.GetSchema()))))
                    : []).Where(x => x.Column is not null).ToArray();
            if (required.Length == 0) issues.Add("payroll_model_missing");
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandTimeout = 5;
                command.CommandText = "SELECT s.name,t.name,c.name FROM sys.schemas s JOIN sys.tables t ON t.schema_id=s.schema_id JOIN sys.columns c ON c.object_id=t.object_id";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var actual = new HashSet<(string Schema, string Table, string? Column)>();
                while (await reader.ReadAsync(cancellationToken)) actual.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                if (required.Any(column => !actual.Contains(column))) issues.Add("payroll_schema_columns_missing");
            }
            finally { await db.Database.CloseConnectionAsync(); }
            schemaUnavailable = issues.Any(x => x.StartsWith("model_", StringComparison.Ordinal) ||
                x.StartsWith("database_", StringComparison.Ordinal) || x.StartsWith("migration_", StringComparison.Ordinal) ||
                x.StartsWith("payroll_", StringComparison.Ordinal));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            schemaUnavailable = true;
            issues.Add("payroll_schema_probe_unavailable");
        }
        // Never put exception text, configuration values, SQL endpoints or personal data in health output.
        var data = new Dictionary<string, object> { ["codes"] = issues.Distinct().Order().ToArray(),
            ["businessAcceptanceEstablished"] = false, ["providerDeliveryTested"] = false };
        return new HealthCheckResult(schemaUnavailable ? HealthStatus.Unhealthy :
            issues.Count > 0 ? HealthStatus.Degraded : HealthStatus.Healthy,
            "Payroll deployment configuration and schema metadata only.", data: data);
    }

    private static bool PrivateStorageConfigured(IServiceProvider services)
    {
        // The configured encrypted-store constructor validates path/key metadata.
        // There is deliberately no write/read round trip in a health request.
        try { _ = services.GetRequiredService<IPrivateFileStore>(); return true; }
        catch (Exception) { return false; }
    }
}
