using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.Attendance.Reports;

public sealed class AttendanceReportService(AttendanceOperation operation, ICurrentAuthentication current,
    IAttendanceReportRepository reports, IAttendanceSummaryRepository summaries, AttendanceSummaryService summaryService,
    IAccountAccessReader access, IDurableJobRepository jobs, IPrivateFileStore files, AttendanceJobOptions options, TimeProvider clock)
{
    // Keep the original queue identity for already persisted CSV requests; the bound job owns its format.
    public const string MessageType = "attendance.report-csv.v1";
    public const string ReportType = "attendance.factual.v1";

    public Task<AttendanceFactualBatchResponse> BatchAsync(Guid[] employeeIds, DateOnly from, DateOnly to,
        AuthRequestContext context, CancellationToken ct) =>
        ReadBatchAsync(employeeIds, from, to, "attendance.report.factual-batch", batch => batch, context, ct);

    public Task<AttendanceDashboardResponse> DashboardAsync(Guid[] employeeIds, DateOnly from, DateOnly to,
        AuthRequestContext context, CancellationToken ct) =>
        ReadBatchAsync(employeeIds, from, to, "attendance.report.dashboard", FactualAttendanceDashboard.Create, context, ct);

    private Task<T> ReadBatchAsync<T>(Guid[] employeeIds, DateOnly from, DateOnly to, string operationName,
        Func<AttendanceFactualBatchResponse, T> map, AuthRequestContext context, CancellationToken ct)
    {
        ValidateRange(employeeIds, from, to);
        // Capture caller input once; authorization and returned rows must use the same immutable selection.
        var selected = employeeIds.Order().ToArray();
        return operation.ReadAsync(AttendanceReportPermissions.Read, operationName, null, context,
            async (_, token) =>
            {
                var rows = await LoadRowsAsync(selected, from, to, token);
                return map(FactualAttendanceDashboard.Batch(selected, from, to, clock.GetUtcNow(), rows));
            }, ct,
            resourceScopes: (_, token) => ResourceScopesAsync(selected, from, to, token));
    }

    public Task<IReadOnlyList<FactualReportRow>> PreviewAsync(Guid employeeId, DateOnly from, DateOnly to,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateRange([employeeId], from, to);
        return operation.ReadAsync(AttendanceReportPermissions.Read, "attendance.report.preview", employeeId, context,
            (_, token) => LoadRowsAsync([employeeId], from, to, token), ct,
            resourceScopes: (_, token) => ResourceScopesAsync([employeeId], from, to, token));
    }

    public Task<AttendanceExportResponse> RequestAsync(AttendanceExportRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || !Validator.TryValidateObject(request, new ValidationContext(request), [], true)) throw AttendanceOperation.Invalid();
        ValidateRange(request.EmployeeIds, request.From, request.To);
        if (!FactualAttendanceDocument.Supports(request.Format)) throw AttendanceOperation.Invalid("unsupported_attendance_report_format");
        if (!options.Enabled) throw new AuthenticationFailure("attendance_export_worker_not_enabled", 503);
        return operation.WriteAsync(AttendanceReportPermissions.Export, "attendance.report.request", null, key, request, request.Reason,
            context, async (actor, token) =>
            {
                var groups = await ResourceScopesAsync(request.EmployeeIds, request.From, request.To, token);
                var scope = new AttendanceReportScope(actor.Identity, request.EmployeeIds.Order().ToArray(), request.From, request.To, [], groups);
                var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(scope), FilterPurpose(actor.Identity.AccountId), token);
                var job = new ReportExportJob(actor.Identity.AccountId.ToString(), ReportType, request.Format,
                    AttendanceOperation.Reference(reference), actor.Now, request.From, request.To);
                reports.Add(job);
                // The same protected filter reference is bound to the job by its immutable identity and type.
                jobs.Stage(new OutboxMessage(MessageType, job.ProtectedFilterScopeReference, actor.Now, context.CorrelationId, job.Id.ToString("N")));
                return new AttendanceChange<AttendanceExportResponse>(job.Id, null, Map(job, null));
            }, ct, resourceScopes: (_, token) => ResourceScopesAsync(request.EmployeeIds, request.From, request.To, token));
    }

    public Task<AttendanceExportResponse> StatusAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendanceReportPermissions.Export, "attendance.report.status", null, context,
            async (actor, token) =>
            {
                var job = await reports.FindAsync(actor.Identity.AccountId, id, token) ?? throw AttendanceOperation.Missing();
                return Map(job, await jobs.FindByIdentityAsync(MessageType, id.ToString("N"), token));
            }, ct, resourceScopes: async (actor, token) => await JobScopesAsync(actor.Identity.AccountId, id, token));
    }

    public Task<AttendanceReportDownload> DownloadAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendanceReportPermissions.Export, "attendance.report.download", null, context,
            async (actor, token) =>
            {
                var job = await reports.FindAsync(actor.Identity.AccountId, id, token) ?? throw AttendanceOperation.Missing();
                if (job.State != ReportJobState.Completed || job.StoredFileId is not Guid fileId || job.SourceLockReference is null)
                    throw AttendanceOperation.Conflict("attendance_report_not_ready");
                if (job.DownloadExpiresAt is null || job.DownloadExpiresAt <= clock.GetUtcNow())
                    throw new AuthenticationFailure("attendance_report_download_expired", 410);
                var output = await operation.ReadPrivateAsync<AttendanceReportOutput>(job.SourceLockReference, OutputPurpose(job.Id), token);
                var file = await reports.FindFileAsync(fileId, token) ?? throw AttendanceOperation.Missing();
                var scope = await ScopeAsync(job, token);
                ValidateOutput(job, scope, output);
                var reference = new PrivateFileReference(output.File.StorageKey, output.File.ObjectVersion, output.File.Sha256,
                    output.File.KeyReference, output.File.SizeBytes);
                var extension = FactualAttendanceDocument.Extension(job.Format);
                var contentType = FactualAttendanceDocument.ContentType(job.Format);
                if (file.DeletedAt.HasValue || output.StoredFileId != fileId ||
                    file.StorageKey != reference.StorageKey || file.Sha256 != reference.Sha256 || file.SizeBytes != reference.SizeBytes ||
                    file.EncryptionKeyReference != reference.KeyReference || reference.SizeBytes is <= 0 or > 2_000_000 ||
                    file.UploadedByUserId != actor.Identity.AccountId.ToString() || file.Extension != extension || file.ContentType != contentType)
                    throw AttendanceOperation.Conflict("attendance_report_evidence_conflict");
                var bytes = await files.ReadAsync(reference, FilePurpose(job.Id, job.Format), token);
                if (bytes.LongLength != reference.SizeBytes) throw AttendanceOperation.Conflict("attendance_report_evidence_conflict");
                return new AttendanceReportDownload(bytes, job.Format == ReportFormat.Csv ? contentType + "; charset=utf-8" : contentType,
                    $"attendance-factual-{job.Id:N}{extension}");
            }, ct, resourceScopes: async (actor, token) => await JobScopesAsync(actor.Identity.AccountId, id, token));
    }

    public async Task<IReadOnlyList<FactualReportRow>> LoadRowsAsync(Guid[] employees, DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(employees, from, to);
        var rows = new List<FactualReportRow>();
        foreach (var employee in employees.Order())
        for (var number = from.DayNumber; number <= to.DayNumber; number++)
        {
            var day = await summaryService.LoadAuthorizedDayAsync(employee, DateOnly.FromDayNumber(number), ct);
            var revision = AttendanceSummaryService.IsSavedRevisionCurrent(day) ? day.LatestSavedRevision : null;
            rows.Add(new(employee, day.BusinessDate, day.FactualStatus, day.Facts.WorkedSeconds, day.Facts.WorkedMinutes,
                day.Facts.OpenSessions, day.Facts.SessionIds.Count, day.ExceptionCount, day.IsLocked,
                revision?.Id, revision?.Revision, day.Facts.InputFingerprint));
        }
        return rows;
    }

    public async Task<IReadOnlyList<AttendanceResourceScope>> ResourceScopesAsync(Guid[] employees, DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(employees, from, to);
        var scopes = new List<AttendanceResourceScope>();
        foreach (var id in employees) scopes.AddRange(await summaries.ResourceScopesAsync(id, from, to, ct));
        ValidateScopeEmployees(scopes, employees);
        return scopes;
    }

    public async Task<AttendanceReportScope> ScopeAsync(ReportExportJob job, CancellationToken ct)
    {
        if (job.ReportType != ReportType || !FactualAttendanceDocument.Supports(job.Format) || !Guid.TryParseExact(job.RequestedByUserId, "D", out var owner))
            throw AttendanceOperation.Conflict("attendance_report_identity_conflict");
        var scope = await operation.ReadPrivateAsync<AttendanceReportScope>(job.ProtectedFilterScopeReference, FilterPurpose(owner), ct);
        ValidateRange(scope.EmployeeIds, scope.From, scope.To);
        if (scope.Requester is null || scope.Requester.AccountId != owner || scope.From != job.PeriodFrom || scope.To != job.PeriodTo ||
            scope.OriginalResources is null)
            throw AttendanceOperation.Conflict("attendance_report_identity_conflict");
        OriginalScopes(scope);
        return scope;
    }

    public async Task RequireScopeAsync(AuthenticatedAccount actor, AttendanceReportScope scope, CancellationToken ct)
    {
        if (scope.Requester is null || actor.AccountId != scope.Requester.AccountId || actor.EmployeeId != scope.Requester.EmployeeId)
            throw AttendanceOperation.Denied();
        var original = OriginalScopes(scope);
        var currentScopes = await ResourceScopesAsync(scope.EmployeeIds, scope.From, scope.To, ct);
        var grants = await access.GetGrantsAsync(actor.AccountId, clock.GetUtcNow(), ct);
        if (!AttendanceScopeEvaluation.Allows(actor, grants, AttendanceReportPermissions.Export, original) ||
            !AttendanceScopeEvaluation.Allows(actor, grants, AttendanceReportPermissions.Export, currentScopes))
            throw AttendanceOperation.Denied();
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> JobScopesAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await reports.FindAsync(owner, id, ct) ?? throw AttendanceOperation.Missing();
        var scope = await ScopeAsync(job, ct);
        var actor = current.Require();
        await RequireScopeAsync(actor, scope, ct);
        var required = OriginalScopes(scope).Concat(await ResourceScopesAsync(scope.EmployeeIds, scope.From, scope.To, ct))
            // Identical original/current predicates need one check. Preserve different slices and
            // alternative groups, including any legacy singleton requirement.
            .DistinctBy(x => JsonSerializer.Serialize(x.Alternatives.Distinct()
                .OrderBy(r => r.EmployeeId).ThenBy(r => r.DepartmentId).ThenBy(r => r.TeamId).ThenBy(r => r.SiteId).ThenBy(r => r.ProjectId)))
            .ToArray();
        ValidateScopeEmployees(required, scope.EmployeeIds);
        return required;
    }

    private static IReadOnlyList<AttendanceResourceScope> OriginalScopes(AttendanceReportScope scope)
    {
        ValidateRange(scope.EmployeeIds, scope.From, scope.To);
        if (scope.OriginalResources is null || scope.OriginalResources.Count > AttendanceScopeEvaluation.MaximumScopes ||
            (scope.OriginalScopeGroups is not null && !AttendanceScopeEvaluation.IsValid(scope.OriginalScopeGroups)))
            throw AttendanceOperation.Conflict("attendance_report_identity_conflict");
        // Missing groups identify legacy evidence. Its flat ALL requirements remain singleton
        // groups; adding a new grouped snapshot must never weaken those persisted requirements.
        var groups = AttendanceScopeEvaluation.FromResources(scope.OriginalResources)
            .Concat(scope.OriginalScopeGroups ?? []).ToArray();
        ValidateScopeEmployees(groups, scope.EmployeeIds);
        return groups;
    }

    private static void ValidateScopeEmployees(IReadOnlyList<AttendanceResourceScope> scopes, Guid[] employees)
    {
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives)
                .Any(x => !x.EmployeeId.HasValue || !employees.Contains(x.EmployeeId.Value)) ||
            employees.Any(id => !scopes.Any(x => x.Alternatives[0].EmployeeId == id)))
            throw AttendanceOperation.Conflict("attendance_report_identity_conflict");
    }

    public static void ValidateRange(Guid[]? employees, DateOnly from, DateOnly to)
    {
        if (employees is null || employees.Length is < 1 or > 25 || employees.Any(x => x == Guid.Empty) ||
            employees.Distinct().Count() != employees.Length || from == default || to == DateOnly.MaxValue || to < from ||
            to.DayNumber - from.DayNumber > 30) throw AttendanceOperation.Invalid("attendance_report_range_limit");
    }
    public static string FilterPurpose(Guid requester) => $"attendance:report:filters:{requester:N}";
    public static string OutputPurpose(Guid job) => $"attendance:report:manifest:{job:N}";
    public static string FilePurpose(Guid job) => $"attendance:report:csv:{job:N}";
    public static string FilePurpose(Guid job, ReportFormat format) => format == ReportFormat.Csv ? FilePurpose(job) :
        $"attendance:report:{FactualAttendanceDocument.Extension(format)[1..]}:{job:N}";

    private static void ValidateOutput(ReportExportJob job, AttendanceReportScope scope, AttendanceReportOutput output)
    {
        if (output.Format != job.Format || output.Rows is null || output.File is null)
            throw AttendanceOperation.Conflict("attendance_report_evidence_conflict");
        // Legacy v1 CSV manifests did not contain format/metadata. Preserve their exact old contract.
        if (job.Format == ReportFormat.Csv && output.GeneratorVersion == FactualAttendanceCsv.GeneratorVersion && output.Metadata is null)
            return;
        var metadata = output.Metadata;
        if (output.GeneratorVersion != FactualAttendanceDocument.Version(job.Format) || metadata is null ||
            metadata.ReportId != job.Id || metadata.GeneratedAtUtc != job.GeneratedAt || metadata.From != scope.From ||
            metadata.To != scope.To || metadata.EmployeeIds is null || !metadata.EmployeeIds.Order().SequenceEqual(scope.EmployeeIds.Order()))
            throw AttendanceOperation.Conflict("attendance_report_evidence_conflict");
        try { FactualAttendanceDocument.Validate(output.Rows, metadata); }
        catch (Exception error) when (error is ArgumentException or AuthenticationFailure)
        { throw AttendanceOperation.Conflict("attendance_report_evidence_conflict"); }
    }
    private static AttendanceExportResponse Map(ReportExportJob job, OutboxMessage? message) => new(job.Id,
        job.State == ReportJobState.Completed ? "completed" : job.State == ReportJobState.Failed ? "failed" : message?.State switch
        { DispatchState.Processing => "running", DispatchState.Failed => "failed", _ => "pending" },
        job.Format, job.PeriodFrom!.Value, job.PeriodTo!.Value, job.RequestedAt, job.GeneratedAt, job.DownloadExpiresAt,
        job.FailureCode ?? (message?.State == DispatchState.Failed ? message.LastFailureCode : null));
}
