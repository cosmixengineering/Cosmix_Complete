using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.HR.Reports;

public sealed class HrReportExportService(HrReportService reports, HrReportAuthorization authorization,
    IHrReportExportRepository repository, ICurrentAuthentication current, ISchedulingJournalRepository journal,
    IDurableJobRepository jobs, IPrivateFileStore files, AttendanceOperation privateReader,
    INotificationOutbox notifications, AttendanceJobOptions options, TimeProvider clock)
{
    private const string RequestOperation = "hr.report.export.request";

    public Task<HrReportExportResponse> RequestAsync(string reportType, HrReportExportRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        reportType = reportType?.ToLowerInvariant() ?? "";
        if (!HrReportTypes.IsValid(reportType)) throw AttendanceOperation.Invalid("invalid_hr_report_type");
        if (request is null || !Enum.IsDefined(request.Format)) throw AttendanceOperation.Invalid("invalid_hr_report_format");
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_.:-]{8,100}\z", RegexOptions.CultureInvariant))
            throw AttendanceOperation.Invalid("invalid_idempotency_key");
        if (!options.Enabled) throw new AuthenticationFailure("hr_report_export_worker_not_enabled", 503);
        var input = (request.Filter ?? new()) with { Page = 1, PageSize = 1000 };
        var filter = HrReportAuthorization.Normalize(reportType, input, clock.GetUtcNow());
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { reportType, request.Format, request.Reason, filter = input })));
        HrReportExportScope? retainedScope = null;
        return authorization.ReadAsync(reportType, filter, true, request.Reason, context, async (selection, token) =>
        {
            var identity = current.Require();
            var receipt = await journal.FindReceiptAsync(identity.AccountId.ToString(), RequestOperation, key, token);
            if (receipt is not null)
            {
                if (receipt.RequestSha256 != digest) throw AttendanceOperation.Conflict("idempotency_key_reused");
                if (receipt.ExpiresAt <= clock.GetUtcNow()) throw AttendanceOperation.Conflict("idempotency_key_expired");
                if (receipt.ResultCode != HrReportExportRules.MessageType || receipt.ProtectedResultReference is null || receipt.CompletedAt is null)
                    throw AttendanceOperation.Conflict("idempotency_result_unavailable");
                var id = await privateReader.ReadPrivateAsync<Guid>(receipt.ProtectedResultReference, ReceiptPurpose(identity.AccountId), token);
                var existing = await FindAsync(identity.AccountId, id, token);
                retainedScope = await ScopeAsync(existing, token);
                await authorization.RequireExportAsync(retainedScope, identity, token);
                return await MapAsync(existing, reportType, token);
            }
            var scope = new HrReportExportScope(1, identity, reportType, request.Format, filter, selection);
            retainedScope = scope;
            HrReportExportRules.ValidateScope(scope);
            var saved = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(scope), HrReportExportRules.FilterPurpose(identity.AccountId), token);
            var now = clock.GetUtcNow();
            var job = new ReportExportJob(identity.AccountId.ToString(), HrReportExportRules.ReportType, request.Format,
                AttendanceOperation.Reference(saved), now, filter.From, filter.To);
            repository.Add(job);
            jobs.Stage(new(HrReportExportRules.MessageType, job.ProtectedFilterScopeReference, now, context.CorrelationId, job.Id.ToString("N")));
            var result = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(job.Id), ReceiptPurpose(identity.AccountId), token);
            var created = new IdempotencyReceipt(identity.AccountId.ToString(), RequestOperation, key, digest, now, now.AddHours(24));
            created.Complete(HrReportExportRules.MessageType, AttendanceOperation.Reference(result), clock.GetUtcNow());
            journal.AddReceipt(created);
            await authorization.RequireExportAsync(scope, identity, token);
            return await MapAsync(job, reportType, token);
        }, ct, async (_, token) =>
        {
            var identity = current.Require();
            var receipt = await journal.FindReceiptAsync(identity.AccountId.ToString(), RequestOperation, key, token);
            if (receipt is null) return null;
            if (receipt.RequestSha256 != digest) throw AttendanceOperation.Conflict("idempotency_key_reused");
            if (receipt.ExpiresAt <= clock.GetUtcNow()) throw AttendanceOperation.Conflict("idempotency_key_expired");
            if (receipt.ResultCode != HrReportExportRules.MessageType || receipt.ProtectedResultReference is null || receipt.CompletedAt is null)
                throw AttendanceOperation.Conflict("idempotency_result_unavailable");
            var id = await privateReader.ReadPrivateAsync<Guid>(receipt.ProtectedResultReference, ReceiptPurpose(identity.AccountId), token);
            var existing = await FindAsync(identity.AccountId, id, token);
            return await authorization.RequireExportAsync(await ScopeAsync(existing, token), identity, token);
        }, token => authorization.RequireExportAsync(
            retainedScope ?? throw AttendanceOperation.Conflict("hr_report_evidence_conflict"), current.Require(), token));
    }

    public Task<HrReportExportResponse> StatusAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        ReadAsync(id, context, async (job, scope, token) => await MapAsync(job, scope.ReportType, token), ct);

    public async Task<HrReportDownload> DownloadAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        DateTimeOffset? expiresAt = null;
        var download = await ReadAsync(id, context, async (job, scope, token) =>
        {
            if (job.State != ReportJobState.Completed) throw AttendanceOperation.Conflict("hr_report_not_ready");
            if (job.DownloadExpiresAt is null || job.DownloadExpiresAt <= clock.GetUtcNow())
                throw new AuthenticationFailure("hr_report_download_expired", 410);
            expiresAt = job.DownloadExpiresAt;
            var output = await OutputAsync(job, scope.ReportType, token);
            var file = await repository.FindFileAsync(output.StoredFileId, token) ?? throw AttendanceOperation.Missing();
            var reference = output.File;
            var extension = HrReportExportRules.Extension(job.Format);
            var contentType = HrReportExportRules.ContentType(job.Format);
            if (file.DeletedAt.HasValue || file.StorageKey != reference.StorageKey || file.Sha256 != reference.Sha256 ||
                file.SizeBytes != reference.SizeBytes || file.EncryptionKeyReference != reference.KeyReference ||
                file.UploadedByUserId != job.RequestedByUserId || file.Extension != extension || file.ContentType != contentType)
                throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
            await authorization.RequireExportAsync(output.Scope, current.Require(), token);
            var bytes = await files.ReadAsync(reference, HrReportExportRules.FilePurpose(job.Id, job.Format), token);
            if (bytes.LongLength != reference.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != reference.Sha256.ToUpperInvariant())
                throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
            await authorization.RequireExportAsync(output.Scope, current.Require(), token);
            if (job.DownloadExpiresAt <= clock.GetUtcNow()) throw new AuthenticationFailure("hr_report_download_expired", 410);
            return new HrReportDownload(bytes, contentType + (job.Format == ReportFormat.Csv ? "; charset=utf-8" : ""),
                $"{scope.ReportType}-{job.Id:N}{extension}");
        }, ct);
        // Audit storage and transaction completion can cross the download deadline.
        if (expiresAt is null || expiresAt <= clock.GetUtcNow())
            throw new AuthenticationFailure("hr_report_download_expired", 410);
        return download;
    }

    private async Task<T> ReadAsync<T>(Guid id, AuthRequestContext context,
        Func<ReportExportJob, HrReportExportScope, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid("invalid_hr_report_job");
        var identity = current.Require();
        await authorization.CurrentAccessAsync(identity, [HrReportPermissions.Read, HrReportPermissions.Export], ct);
        var initial = await FindAsync(identity.AccountId, id, ct);
        var scope = await ScopeAsync(initial, ct);
        return await authorization.ReadAsync(scope.ReportType, scope.Filter, true, "Read requested private export", context,
            async (_, token) =>
            {
                var job = await FindAsync(identity.AccountId, id, token);
                if (job.ProtectedFilterScopeReference != initial.ProtectedFilterScopeReference)
                    throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
                await authorization.RequireExportAsync(scope, identity, token);
                var result = await action(job, scope, token);
                await authorization.RequireExportAsync(scope, identity, token);
                return result;
            }, ct, async (_, token) => await authorization.RequireExportAsync(scope, identity, token),
            token => authorization.RequireExportAsync(scope, identity, token));
    }

    internal async Task<HrReportPreparedExport> PrepareAsync(DurableJobClaim claim, CancellationToken ct)
    {
        var metadata = claim.Preparation;
        if (claim.MessageId == Guid.Empty || claim.LeaseToken == Guid.Empty || claim.Attempt < 1 ||
            metadata?.MessageType != HrReportExportRules.MessageType || !Guid.TryParseExact(metadata.DeduplicationKey, "N", out var id))
            throw new DurableJobFailure("invalid_hr_report_job_binding", false);
        try
        {
            RequireLease(claim, ct);
            var initial = RequirePending(await repository.FindWorkerAsync(id, ct), metadata);
            var binding = HrReportJobBinding.Capture(initial);
            var scope = await ScopeAsync(initial, ct);
            var (document, generatedScope) = await repository.ReadSnapshotAsync(async token =>
            {
                var job = RequirePending(await repository.FindWorkerAsync(id, token), metadata);
                if (HrReportJobBinding.Capture(job) != binding) throw new DurableJobFailure("invalid_hr_report_job_binding", false);
                var selection = await authorization.RequireExportAsync(scope, scope.Requester, token, includeSources: true);
                var materialized = await reports.BuildExportAsync(scope.ReportType, scope.Filter, selection, token);
                var groups = scope.Selection.ScopeGroups.Concat(selection.ScopeGroups).Concat(selection.AdditionalScopeGroups)
                    .DistinctBy(g => JsonSerializer.Serialize(g)).ToArray();
                var retained = scope with { Selection = new(scope.Selection.EmployeeIds.ToArray(), scope.Selection.IncludeUnscopedAudit)
                    { ScopeGroups = groups, ReadRoleAssignmentIds = selection.ReadRoleAssignmentIds.ToArray() } };
                HrReportExportRules.ValidateScope(retained);
                return (materialized, retained);
            }, ct);
            // No source transaction or global authorization lock survives rendering/private output IO.
            RequireLease(claim, ct);
            var bytes = HrReportExportRules.Generate(binding.Format, document);
            RequireLease(claim, ct);
            var reference = await files.WriteAsync(bytes, HrReportExportRules.FilePurpose(id, binding.Format), ct);
            if (reference.SizeBytes != bytes.Length || reference.Sha256.ToUpperInvariant() != Convert.ToHexString(SHA256.HashData(bytes)))
                throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
            var now = document.Metadata.GeneratedAtUtc;
            var extension = HrReportExportRules.Extension(binding.Format);
            // Constructed here solely to reserve its ID for the private manifest; never tracked in preparation.
            var file = new StoredFile(reference.StorageKey, $"{scope.ReportType}-{id:N}{extension}", extension,
                HrReportExportRules.ContentType(binding.Format), reference.SizeBytes, reference.Sha256, binding.Owner, now, reference.KeyReference);
            var output = new HrReportExportOutput(1, id, reference, file.Id, binding.Format, document.Metadata, generatedScope);
            var manifest = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(output), HrReportExportRules.OutputPurpose(id), ct);
            RequireLease(claim, ct);
            return new(claim, binding, generatedScope, file, AttendanceOperation.Reference(manifest), now);
        }
        catch (AuthenticationFailure error) when (error.StatusCode is 400 or 401 or 403 or 404 or 409 or 410)
        { throw new DurableJobFailure(error.StatusCode is 401 or 403 ? "hr_report_scope_revoked" : error.Code, false); }
    }

    internal async Task ApplyPreparedAsync(OutboxMessage message, HrReportPreparedExport prepared, CancellationToken ct)
    {
        var claim = prepared.Claim;
        var metadata = claim.Preparation;
        RequireLease(claim, ct);
        if (metadata is null || message.Id != claim.MessageId || message.LeaseToken != claim.LeaseToken ||
            message.LeaseExpiresAt != claim.LeaseExpiresAt || message.Attempts != claim.Attempt || message.State != DispatchState.Processing ||
            message.MessageType != metadata.MessageType || message.EncryptedPayloadReference != metadata.EncryptedPayloadReference ||
            message.CorrelationId != metadata.CorrelationId || message.DeduplicationKey != metadata.DeduplicationKey)
            throw new DurableJobFailure("invalid_hr_report_job_binding", false);
        try
        {
            var id = prepared.Binding.Id;
            // Notification private IO runs before the auth lock. Its SQL intent is still uncommitted.
            await notifications.StageAsync(new(id, prepared.Scope.Requester.AccountId, NotificationTemplates.ReportReady, id),
                new("", message.CorrelationId), ct);
            await repository.LockAuthorizationAsync(ct);
            var job = RequirePending(await repository.FindWorkerAsync(id, ct), metadata);
            if (HrReportJobBinding.Capture(job) != prepared.Binding)
                throw new DurableJobFailure("invalid_hr_report_job_binding", false);
            var authorizedAt = clock.GetUtcNow();
            await authorization.RequireExportAsync(prepared.Scope, prepared.Scope.Requester, ct);
            RequireLease(claim, ct);
            job.Start();
            repository.Add(prepared.File);
            job.Complete(prepared.File.Id, prepared.ManifestReference, prepared.GeneratedAt, clock.GetUtcNow().AddHours(1));
            authorization.RequireBusinessDate(authorizedAt);
        }
        catch (AuthenticationFailure error) when (error.StatusCode is 400 or 401 or 403 or 404 or 409 or 410)
        { throw new DurableJobFailure(error.StatusCode is 401 or 403 ? "hr_report_scope_revoked" : error.Code, false); }
    }

    private void RequireLease(DurableJobClaim claim, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (claim.LeaseExpiresAt <= clock.GetUtcNow()) throw new DurableJobFailure("hr_report_preparation_expired", true);
    }

    private static ReportExportJob RequirePending(ReportExportJob? job, DurableJobPreparation metadata)
    {
        if (job is null || job.ReportType != HrReportExportRules.ReportType || job.State != ReportJobState.Pending ||
            job.Id.ToString("N") != metadata.DeduplicationKey || job.ProtectedFilterScopeReference != metadata.EncryptedPayloadReference)
            throw new DurableJobFailure("invalid_hr_report_job_binding", false);
        return job;
    }

    private async Task<ReportExportJob> FindAsync(Guid owner, Guid id, CancellationToken ct) =>
        await repository.FindAsync(owner, id, ct) ?? throw AttendanceOperation.Missing("hr_report_not_found");

    private async Task<HrReportExportScope> ScopeAsync(ReportExportJob job, CancellationToken ct)
    {
        if (job.ReportType != HrReportExportRules.ReportType || !Guid.TryParseExact(job.RequestedByUserId, "D", out var owner))
            throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        var scope = await privateReader.ReadPrivateAsync<HrReportExportScope>(job.ProtectedFilterScopeReference,
            HrReportExportRules.FilterPurpose(owner), ct);
        HrReportExportRules.ValidateScope(scope);
        if (scope.Requester.AccountId != owner || scope.Format != job.Format || scope.Filter.From != job.PeriodFrom || scope.Filter.To != job.PeriodTo)
            throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        return scope;
    }

    private async Task<HrReportExportOutput> OutputAsync(ReportExportJob job, string type, CancellationToken ct)
    {
        if (job.SourceLockReference is null) throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        var output = await privateReader.ReadPrivateAsync<HrReportExportOutput>(job.SourceLockReference, HrReportExportRules.OutputPurpose(job.Id), ct);
        if (output?.Scope is null) throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        HrReportExportRules.ValidateScope(output.Scope);
        if (output is null || output.Schema != 1 || output.JobId != job.Id || output.Format != job.Format ||
            output.StoredFileId != job.StoredFileId || output.File is null || output.File.SizeBytes is <= 0 or > HrReportExportRules.MaximumBytes ||
            output.Metadata is null || output.Metadata.ReportType != type || output.Metadata.GeneratedAtUtc != job.GeneratedAt ||
            output.Metadata.TotalCount is < 0 or > HrReportExportRules.MaximumRows || output.Metadata.ReturnedCount != output.Metadata.TotalCount ||
            output.Scope is null || output.Scope.ReportType != type || output.Scope.Format != job.Format ||
            output.Scope.Requester.AccountId.ToString() != job.RequestedByUserId || output.Metadata.Filter != output.Scope.Filter ||
            output.Scope.Filter.From != job.PeriodFrom || output.Scope.Filter.To != job.PeriodTo)
            throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        return output;
    }

    private async Task<HrReportExportResponse> MapAsync(ReportExportJob job, string type, CancellationToken ct)
    {
        var message = await jobs.FindByIdentityAsync(HrReportExportRules.MessageType, job.Id.ToString("N"), ct);
        if (message is null || message.EncryptedPayloadReference != job.ProtectedFilterScopeReference ||
            (job.State == ReportJobState.Completed) != (message.State == DispatchState.Completed))
            throw AttendanceOperation.Conflict("hr_report_evidence_conflict");
        var output = job.State == ReportJobState.Completed ? await OutputAsync(job, type, ct) : null;
        if (output is not null) await authorization.RequireExportAsync(output.Scope, current.Require(), ct);
        var state = job.State == ReportJobState.Completed ? "completed" : job.State == ReportJobState.Failed ? "failed" :
            message?.State switch { DispatchState.Processing => "running", DispatchState.Failed => "failed", _ => "pending" };
        return new(job.Id, type, state, job.Format, job.RequestedAt, job.GeneratedAt, job.DownloadExpiresAt,
            job.FailureCode ?? (message?.State == DispatchState.Failed ? message.LastFailureCode : null),
            output?.Metadata.TotalCount, output?.Metadata.ReturnedCount);
    }

    private static string ReceiptPurpose(Guid owner) => $"hr:report:receipt:{owner:N}";
}

internal sealed record HrReportJobBinding(Guid Id, string Owner, ReportFormat Format, string FilterReference, DateOnly? From, DateOnly? To)
{
    public static HrReportJobBinding Capture(ReportExportJob job) => new(job.Id, job.RequestedByUserId, job.Format,
        job.ProtectedFilterScopeReference, job.PeriodFrom, job.PeriodTo);
}

internal sealed record HrReportPreparedExport(DurableJobClaim Claim, HrReportJobBinding Binding, HrReportExportScope Scope,
    StoredFile File, string ManifestReference, DateTimeOffset GeneratedAt);

public sealed class HrReportExportHandler(HrReportExportService service) : IPreparedDurableJobHandler
{
    private HrReportPreparedExport? prepared;
    public string MessageType => HrReportExportRules.MessageType;
    public async Task PrepareAsync(DurableJobClaim claim, CancellationToken ct)
    {
        prepared = null;
        prepared = await service.PrepareAsync(claim, ct);
    }
    public Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        var result = Interlocked.Exchange(ref prepared, null)
            ?? throw new DurableJobFailure("hr_report_preparation_missing", false);
        return service.ApplyPreparedAsync(message, result, ct);
    }
}
