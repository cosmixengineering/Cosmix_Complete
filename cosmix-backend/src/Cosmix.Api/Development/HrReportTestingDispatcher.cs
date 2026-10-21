using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Reports;
using Cosmix.Infrastructure.BackgroundJobs;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Api.Development;

// R1 acceptance only. Registration and a one-shot accepted-response latch belong to its host boundary.
public sealed class HrReportTestingDispatcher(CosmixDbContext db, IPrivateFileStore files,
    TimeProvider clock, HrReportExportService exports)
{
    public async Task<bool> DispatchAsync(Guid jobId, AuthenticatedAccount actor, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") != "HumaHrReports20261020R1")
            throw HrReportAcceptanceJobRepository.Refuse();
        HrReportAcceptanceJobRepository.ValidateActor(actor);
        if (jobId == Guid.Empty || db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any())
            throw HrReportAcceptanceJobRepository.Refuse();
        var target = ReviewedTarget;
        HrReportAcceptanceJobRepository.ValidateConnection(db, target);
        var opened = db.Database.GetDbConnection().State == ConnectionState.Closed;
        try
        {
            if (opened) await db.Database.OpenConnectionAsync(ct);
            await HrReportAcceptanceJobRepository.VerifyTargetAsync(db, target, ct);
            var job = await db.ReportExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId, ct)
                ?? throw HrReportAcceptanceJobRepository.Refuse();
            var key = jobId.ToString("N");
            var message = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x =>
                x.MessageType == HrReportExportRules.MessageType && x.DeduplicationKey == key, ct)
                ?? throw HrReportAcceptanceJobRepository.Refuse();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")).DateTime);
            var filter = new HrReportFilter(From: today, To: today, Date: today, Year: today.Year, Month: today.Month,
                EmployeeId: actor.EmployeeId, Page: 1, PageSize: 1000);
            var capture = new HrReportAcceptanceCapture(jobId, message.Id, actor, filter,
                job.ProtectedFilterScopeReference, message.CorrelationId, job.RequestedAt);
            var jobs = new HrReportAcceptanceJobRepository(db, target, capture, files, clock);
            var options = new AttendanceJobOptions { Enabled = true, MaximumAttempts = 1, LeaseSeconds = 120, BatchSize = 1 };
            // The application's registered repository is not replaced. All services share this context.
            return await new DurableJobProcessor(jobs, [new HrReportExportHandler(exports)], options, clock).DispatchOneAsync(ct);
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }

    public static HrReportAcceptanceTarget ReviewedTarget { get; } = new(
        @"np:\\.\pipe\LOCALDB#9F3001F7\tsql\query", @"Software-PC1\LOCALDB#9F3001F7", @"COSMIXRND\Huma.Khan",
        new("D9C34627-18A2-41CF-A804-821E39516303"), 25768,
        @"C:\Users\Huma.Khan\Cosmix_Testing.mdf", @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf",
        "Cosmix.HrReports.R1", true);
}

public sealed record HrReportAcceptanceTarget(
    string Pipe, string Server, string Owner, Guid Family, int ProcessId,
    string DataFile, string LogFile, string ApplicationName, bool AllowUnencryptedLocalPipe);

public sealed record HrReportAcceptanceCapture(
    Guid JobId, Guid MessageId, AuthenticatedAccount Requester, HrReportFilter Filter,
    string ProtectedScopeReference, string CorrelationId, DateTimeOffset RequestedAt);

public sealed class HrReportAcceptanceJobRepository : IDurableJobRepository
{
    private readonly CosmixDbContext db;
    private readonly HrReportAcceptanceTarget target;
    private readonly HrReportAcceptanceCapture capture;
    private readonly IPrivateFileStore files;
    private readonly TimeProvider clock;
    private readonly DurableJobRepository inner;
    private int attempted;
    private DurableJobClaim? issued;

    public HrReportAcceptanceJobRepository(CosmixDbContext db, HrReportAcceptanceTarget target,
        HrReportAcceptanceCapture capture, IPrivateFileStore files, TimeProvider clock)
    {
        this.db = db; this.target = target; this.capture = capture; this.files = files; this.clock = clock;
        ValidateActor(capture.Requester);
        if (target != HrReportTestingDispatcher.ReviewedTarget || capture.JobId == Guid.Empty || capture.MessageId == Guid.Empty ||
            string.IsNullOrWhiteSpace(capture.CorrelationId) || capture.CorrelationId.Length > 100 ||
            capture.ProtectedScopeReference.Length is < 1 or > 500 || capture.RequestedAt.Offset != TimeSpan.Zero)
            throw Refuse();
        ValidateFilter(capture.Filter, capture.Requester.EmployeeId);
        ValidateConnection(db, target);
        inner = new DurableJobRepository(db, clock);
    }

    public async Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> types,
        DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var observedNow = clock.GetUtcNow();
        if (!DurableJobMessageTypes.RequireAllowed(types).SequenceEqual(new[] { HrReportExportRules.MessageType }) ||
            lease != TimeSpan.FromSeconds(120) || now > observedNow || observedNow - now > TimeSpan.FromSeconds(5) ||
            Interlocked.Exchange(ref attempted, 1) != 0) throw Refuse();
        RequireClean();
        ValidateConnection(db, target);
        var opened = db.Database.GetDbConnection().State == ConnectionState.Closed;
        try
        {
            if (opened) await db.Database.OpenConnectionAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await VerifyTargetAsync(db, target, ct);
            var job = await db.ReportExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == capture.JobId, ct);
            ValidateJob(job);
            var message = await db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == capture.MessageId, ct)
                ?? throw Refuse();
            ValidateMessage(message);
            if (message.State != DispatchState.Pending || message.Attempts != 0 || message.LeaseToken is not null ||
                message.LeaseExpiresAt is not null || message.CompletedAt is not null || message.LastFailureCode is not null ||
                message.AvailableAt != message.CreatedAt || now < message.AvailableAt || now < message.CreatedAt ||
                message.RowVersion.Length != 8) throw Refuse();
            await ValidateScopeAsync(ct);
            var claimedAt = clock.GetUtcNow();
            if (claimedAt < now || claimedAt - now > TimeSpan.FromSeconds(5)) throw Refuse();
            var leaseToken = message.Claim(claimedAt, claimedAt.Add(lease));
            var result = new DurableJobClaim(message.Id, leaseToken, message.LeaseExpiresAt!.Value, message.Attempts)
            {
                Preparation = new(message.MessageType, message.EncryptedPayloadReference, message.CorrelationId, message.DeduplicationKey)
            };
            // Save only this row. EF's original RowVersion participates in the UPDATE predicate.
            var entries = db.ChangeTracker.Entries().ToArray();
            if (entries.Length != 1 || !ReferenceEquals(entries[0].Entity, message) ||
                entries[0].State != EntityState.Modified || entries[0].Property("RowVersion").OriginalValue is not byte[] { Length: 8 })
                throw Refuse();
            ValidateConnection(db, target);
            if (await db.SaveChangesAsync(ct) != 1) throw Refuse();
            await transaction.CommitAsync(ct);
            issued = result;
            return result;
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (opened) await db.Database.CloseConnectionAsync();
        }
    }

    private void RequireClean()
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any() ||
            db.Database.CreateExecutionStrategy().RetriesOnFailure) throw Refuse();
    }

    internal static void ValidateActor(AuthenticatedAccount actor)
    {
        if (actor is null || actor.AccountId != new Guid("8475187f-bf3b-4f9c-a3ed-4bac32253005") ||
            actor.EmployeeId != new Guid("35d19922-70a8-4c80-8578-e1029360e9fc") ||
            actor.SessionId == Guid.Empty || actor.Client != SessionClient.Web || !actor.MfaSatisfied || actor.SecurityVersion < 1)
            throw Refuse();
    }

    private static void ValidateFilter(HrReportFilter filter, Guid employee)
    {
        if (filter.From is not DateOnly day || filter != new HrReportFilter(From: day, To: day, Date: day,
            Year: day.Year, Month: day.Month, EmployeeId: employee, Page: 1, PageSize: 1000)) throw Refuse();
    }

    private void ValidateJob(ReportExportJob? job)
    {
        if (job is null || job.Id != capture.JobId || job.RequestedByUserId != capture.Requester.AccountId.ToString("D") ||
            job.ReportType != HrReportExportRules.ReportType || job.Format != ReportFormat.Pdf ||
            job.State != ReportJobState.Pending || job.ProtectedFilterScopeReference != capture.ProtectedScopeReference ||
            job.RequestedAt != capture.RequestedAt || job.PeriodFrom != capture.Filter.From || job.PeriodTo != capture.Filter.To ||
            job.StoredFileId is not null || job.GeneratedAt is not null || job.DownloadExpiresAt is not null ||
            job.SourceLockReference is not null || job.FailureCode is not null || job.RowVersion.Length != 8) throw Refuse();
    }

    private void ValidateMessage(OutboxMessage message)
    {
        if (message.Id != capture.MessageId || message.MessageType != HrReportExportRules.MessageType ||
            message.DeduplicationKey != capture.JobId.ToString("N") ||
            message.EncryptedPayloadReference != capture.ProtectedScopeReference ||
            message.CorrelationId != capture.CorrelationId || message.CreatedAt != capture.RequestedAt) throw Refuse();
    }

    private async Task ValidateScopeAsync(CancellationToken ct)
    {
        var reference = StrictRead<PrivateFileReference>(System.Text.Encoding.UTF8.GetBytes(capture.ProtectedScopeReference));
        if (reference.SizeBytes is <= 0 or > 262144 || reference.KeyReference != "hr-reports-r1" ||
            string.IsNullOrWhiteSpace(reference.StorageKey) || string.IsNullOrWhiteSpace(reference.ObjectVersion) ||
            reference.Sha256 is not { Length: 64 } || !reference.Sha256.All(char.IsAsciiHexDigit)) throw Refuse();
        var bytes = await files.ReadAsync(reference, HrReportExportRules.FilterPurpose(capture.Requester.AccountId), ct);
        if (bytes.LongLength != reference.SizeBytes || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                reference.Sha256, StringComparison.OrdinalIgnoreCase)) throw Refuse();
        var scope = StrictRead<HrReportExportScope>(bytes);
        HrReportExportRules.ValidateScope(scope);
        if (scope.Requester != capture.Requester || scope.ReportType != HrReportTypes.EmployeeMaster ||
            scope.Format != ReportFormat.Pdf || scope.Filter != capture.Filter ||
            !scope.Selection.EmployeeIds.SequenceEqual(new[] { capture.Requester.EmployeeId }) ||
            scope.Selection.IncludeUnscopedAudit || scope.Selection.ReadRoleAssignmentIds.Count == 0 ||
            scope.Selection.ReadRoleAssignmentIds.Any(x => x == Guid.Empty) ||
            scope.Selection.ReadRoleAssignmentIds.Distinct().Count() != scope.Selection.ReadRoleAssignmentIds.Count)
            throw Refuse();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")).DateTime);
        if (capture.Filter.From != today) throw Refuse();
    }

    private static T StrictRead<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectAliases(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            PropertyNameCaseInsensitive = false
        }) ?? throw Refuse();
    }

    private static void RejectAliases(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in node.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Refuse();
                RejectAliases(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var element in node.EnumerateArray()) RejectAliases(element);
    }

    internal static void ValidateConnection(CosmixDbContext db, HrReportAcceptanceTarget target)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer") throw Refuse();
        var connection = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Data Source", "Initial Catalog", "Integrated Security", "Encrypt", "Trust Server Certificate",
            "Connect Timeout", "Application Name", "Pooling"
        };
        foreach (string key in connection.Keys)
            if (connection.ShouldSerialize(key) && !allowed.Contains(key)) throw Refuse();
        if (!string.Equals(connection.DataSource, target.Pipe, StringComparison.OrdinalIgnoreCase) ||
            connection.InitialCatalog != "Cosmix_Testing" || !connection.IntegratedSecurity || !connection.Encrypt ||
            !connection.TrustServerCertificate || connection.ApplicationName != target.ApplicationName ||
            db.Database.GetDbConnection().Database != "Cosmix_Testing") throw Refuse();
    }

    internal static async Task VerifyTargetAsync(CosmixDbContext db, HrReportAcceptanceTarget target, CancellationToken ct)
    {
        ValidateConnection(db, target);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        if (db.Database.CurrentTransaction is { } transaction)
            command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction);
        command.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')), DB_NAME(),
              SUSER_SNAME(d.owner_sid), ORIGINAL_LOGIN(), r.family_guid,
              CONVERT(int,SERVERPROPERTY('ProcessID')), d.is_read_only,
              (SELECT COUNT(*) FROM sys.synonyms),
              (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL),
              (SELECT MIN(physical_name) FROM sys.database_files WHERE state=0 AND type=0),
              (SELECT MIN(physical_name) FROM sys.database_files WHERE state=0 AND type=1),
              (SELECT COUNT(*) FROM sys.database_files), c.net_transport, c.encrypt_option
            FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id
            JOIN sys.dm_exec_connections c ON c.session_id=@@SPID
            WHERE d.database_id=DB_ID() AND d.state=0 AND d.user_access=0
              AND NOT EXISTS (SELECT 1 FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0);
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || Enumerable.Range(0, 14).Any(reader.IsDBNull) ||
            reader.GetString(0) != target.Server || reader.GetString(1) != "Cosmix_Testing" ||
            reader.GetString(2) != target.Owner || reader.GetString(3) != target.Owner ||
            reader.GetGuid(4) != target.Family || reader.GetInt32(5) != target.ProcessId || reader.GetBoolean(6) ||
            reader.GetInt32(7) != 0 || reader.GetInt32(8) != 0 ||
            reader.GetString(9) != target.DataFile || reader.GetString(10) != target.LogFile || reader.GetInt32(11) != 2 ||
            reader.GetString(12) != "Named pipe" ||
            reader.GetString(13) != "TRUE" && !(target.AllowUnencryptedLocalPipe && reader.GetString(13) == "FALSE") ||
            await reader.ReadAsync(ct) || await reader.NextResultAsync(ct)) throw Refuse();
    }

    private void ValidateClaim(DurableJobClaim claim)
    {
        if (issued is null || claim != issued || claim.Preparation != issued.Preparation) throw Refuse();
    }

    public void RequireProducerTransaction() => inner.RequireProducerTransaction();
    public Task<OutboxMessage?> FindByIdentityAsync(string type, string key, CancellationToken ct) => inner.FindByIdentityAsync(type, key, ct);
    public Task<bool> RecipientExistsAsync(Guid id, CancellationToken ct) => inner.RecipientExistsAsync(id, ct);
    public void Stage(OutboxMessage message) => inner.Stage(message);
    public void AddAudit(AuditEvent audit) => inner.AddAudit(audit);

    public async Task<bool> CompleteAsync(DurableJobClaim claim, Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct)
    {
        ValidateClaim(claim);
        return await WithVerifiedTargetAsync(() => inner.CompleteAsync(claim, async (message, token) =>
        {
            ValidateMessage(message);
            if (message.Attempts != claim.Attempt || message.Attempts != 1) throw Refuse();
            await action(message, token);
        }, ct), ct);
    }

    public Task<bool> FailAsync(DurableJobClaim claim, string code, DateTimeOffset? retryAt, CancellationToken ct)
    {
        ValidateClaim(claim);
        return WithVerifiedTargetAsync(() => inner.FailAsync(claim, code, retryAt, ct), ct);
    }

    private async Task<bool> WithVerifiedTargetAsync(Func<Task<bool>> action, CancellationToken ct)
    {
        RequireClean();
        ValidateConnection(db, target);
        var opened = db.Database.GetDbConnection().State == ConnectionState.Closed;
        try
        {
            if (opened) await db.Database.OpenConnectionAsync(ct);
            await VerifyTargetAsync(db, target, ct);
            return await action();
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }

    internal static InvalidOperationException Refuse() => new("R1 exact report dispatch binding rejected; values suppressed.");
}
