using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeDocumentService(EmployeeOperation operation, IEmployeeHiringRepository repository,
    IPrivateFileStore files, IEmployeeFileScanner scanner, TimeProvider clock, IEmployeeDocumentRepository documents,
    EmployeeHiringService forms, EmployeeUpdateSections sections, MobileAuthenticationOptions options,
    EmployeeFieldPolicy? fieldPolicy = null)
{
    public const int MaximumUploadBytes = 10 * 1024 * 1024;
    public const int MaximumCompletenessEmployees = 25;
    public Task<IReadOnlyList<EmployeeDocumentResponse>> ListAsync(Guid id, bool medical, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(Permissions(medical, false), "hr.employee.documents.read", id, context, async (_, token) =>
        {
            await EmployeeAsync(id, token);
            var today = Today();
            return (IReadOnlyList<EmployeeDocumentResponse>)(await repository.DocumentsAsync(id, token))
                .Where(d => d.Privacy == Privacy(medical)).Select(d => EmployeeHiringService.MapDocument(d, today)).ToArray();
        }, ct);

    public async Task<EmployeeWriteResult> UploadAsync(Guid id, EmployeeDocumentType type, bool medical, string fileName,
        string contentType, byte[] content, DateOnly? issuedOn, DateOnly? expiresOn, string expectedVersion,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateFile(type, fileName, contentType, content);
        // Own the bytes across all awaits and pass a separate copy to the scanner.
        content = content.ToArray();
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(content));
            var payload = new { type, medical, fileName, contentType, hash, issuedOn, expiresOn, expectedVersion };
            var replay = await operation.TryReplayAsync(Permissions(medical, true), "hr.employee.document.uploaded", id, key, payload, context, ct,
                authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));
            if (replay is not null) return replay;
            await operation.ReadAsync(Permissions(medical, true), "hr.employee.document.upload_authorized", id, context,
                async (actor, token) => { await EditableAsync(id, expectedVersion, actor, token); return true; }, ct);
            if (issuedOn.HasValue && expiresOn < issuedOn) throw EmployeeOperation.Invalid("invalid_document_dates");
            // Scanner I/O is outside the shared SQL authorization lock. The write rechecks access and rowversion.
            var scanned = await ScanSafeAsync(content, ct);
            return await operation.WriteAsync(Permissions(medical, true), "hr.employee.document.uploaded", id, key,
                payload, context, async (actor, token) =>
                {
                    var e = await EditableAsync(id, expectedVersion, actor, token);
                    var reference = await files.WriteAsync(content, EmployeeHiringService.Purpose(id, "document"), token);
                    if (reference.Sha256 != hash || reference.SizeBytes != content.LongLength)
                        throw new AuthenticationFailure("private_file_storage_unavailable", 503);
                    var encoded = JsonSerializer.Serialize(reference);
                    if (encoded.Length > 400) throw new AuthenticationFailure("private_file_reference_too_long", 503);
                    var document = new EmployeeDocument(id, type, encoded, fileName, contentType, content.Length, hash,
                        actor.Identity.AccountId.ToString(), clock.GetUtcNow(), issuedOn, expiresOn, Privacy(medical));
                    History(document, EmployeeDocumentHistoryAction.Uploaded, actor, "Private evidence uploaded", "{}");
                    var beforeScan = EmployeeDocumentState.Capture(document).Encode();
                    document.RecordScan(scanned.State, true, scanned.ScannerVersion, clock.GetUtcNow());
                    History(document, EmployeeDocumentHistoryAction.ScanCompleted, actor, "Initial scan: " + scanned.State, beforeScan);
                    repository.AddDocument(document);
                    await repository.SaveChangesAsync(token);
                    return EmployeeHiringService.Result(e) with { DocumentId = document.Id };
                }, ct, authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));
        }
        finally { CryptographicOperations.ZeroMemory(content); }
    }

    public async Task<EmployeeWriteResult> RescanAsync(Guid id, Guid documentId, bool medical, EmployeeDocumentActionRequest input,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateAction(input);
        var payload = new { documentId, medical, input };
        var replay = await operation.TryReplayAsync(Permissions(medical, true), "hr.employee.document.rescanned", id, key, payload, context, ct,
            authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));
        if (replay is not null) return replay;
        var actorId = await operation.ReadAsync(Permissions(medical, true), "hr.employee.document.rescan_actor_authorized", id,
            context, async (actor, token) =>
            {
                await EmployeeAsync(id, token);
                await AuthorizeApprovedAsync(id, actor, token);
                return actor.Identity.AccountId;
            }, ct);
        // Each actor/resource/key has its own attempt; start receipts also bind the exact payload.
        var attemptId = new Guid(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, documentId, key, actorId })).AsSpan(0, 16));
        await operation.WriteAsync(Permissions(medical, true), "hr.employee.document.rescan_started", id, key,
            payload, context, async (actor, token) =>
            {
                if (actor.Identity.AccountId != actorId) throw EmployeeOperation.Denied();
                var e = await EditableAsync(id, input.ExpectedVersion, actor, token);
                var d = await DocumentAsync(id, documentId, medical, token);
                documents.ExpectVersion(d, OrganizationOperation.Version(input.ExpectedDocumentVersion));
                var before = EmployeeDocumentState.Capture(d).Encode();
                var wasInProgress = d.IsScanInProgress;
                if (d.LifecycleStatus != EmployeeDocumentLifecycleStatus.Current)
                    throw EmployeeOperation.Conflict("employee_document_withdrawn");
                d.BeginScan(attemptId, actor.Identity.AccountId.ToString(), input.Reason, clock.GetUtcNow());
                History(d, wasInProgress ? EmployeeDocumentHistoryAction.ScanRetried : EmployeeDocumentHistoryAction.ScanStarted,
                    actor, input.Reason, before);
                await repository.SaveChangesAsync(token);
                return EmployeeHiringService.Result(e) with { DocumentId = d.Id };
            }, ct, authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));
        var evidence = await operation.ReadAsync(Permissions(medical, true), "hr.employee.document.rescan_authorized", id, context,
            async (actor, token) =>
            {
                await EmployeeAsync(id, token);
                await AuthorizeApprovedAsync(id, actor, token);
                var d = await DocumentAsync(id, documentId, medical, token, refresh: true);
                RequireAttempt(d, attemptId);
                return new { Document = d, d.OriginalFileName, d.ContentType, d.DocumentType, d.Id,
                    Version = Convert.ToBase64String(d.RowVersion), d.StorageKey, d.SizeBytes, d.Sha256 };
            }, ct, auditTarget: result => $"document:{result.Id:N}/{result.Version}");
        var content = await ContentAsync(evidence.Document, ct);
        try
        {
            FileScanResult scan;
            var signatureVerified = true;
            try { ValidateFile(evidence.DocumentType, evidence.OriginalFileName, evidence.ContentType, content); }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 400) { signatureVerified = false; }
            scan = signatureVerified ? await ScanSafeAsync(content, ct) : new(DocumentScanStatus.Rejected, "file-signature-validation/v1");
            var scanAt = clock.GetUtcNow();
            return await operation.WriteAsync(Permissions(medical, true), "hr.employee.document.rescanned", id, key,
                payload, context, async (actor, token) =>
                {
                    var e = await EmployeeAsync(id, token);
                    var d = await DocumentAsync(id, documentId, medical, token, refresh: true);
                    RequireAttempt(d, attemptId);
                    documents.ExpectVersion(d, OrganizationOperation.Version(evidence.Version));
                    if (d.StorageKey != evidence.StorageKey || d.SizeBytes != evidence.SizeBytes || d.Sha256 != evidence.Sha256)
                        throw EmployeeOperation.Conflict("employee_document_changed_during_scan");
                    var before = EmployeeDocumentState.Capture(d).Encode();
                    d.CompleteScan(attemptId, scan.State, signatureVerified, scan.ScannerVersion, scanAt);
                    History(d, EmployeeDocumentHistoryAction.ScanCompleted, actor, "Rescan: " + scan.State, before);
                    await repository.SaveChangesAsync(token);
                    return EmployeeHiringService.Result(e) with { DocumentId = d.Id };
                }, ct, authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));
        }
        finally { CryptographicOperations.ZeroMemory(content); }
    }

    public Task<EmployeeWriteResult> ChangeLifecycleAsync(Guid id, Guid documentId, bool medical,
        EmployeeDocumentLifecycleStatus status, EmployeeDocumentActionRequest input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateAction(input);
        var permission = status switch
        {
            EmployeeDocumentLifecycleStatus.Expired => EmployeeDocumentPermissions.Expire,
            EmployeeDocumentLifecycleStatus.Revoked => EmployeeDocumentPermissions.Revoke,
            EmployeeDocumentLifecycleStatus.Archived => EmployeeDocumentPermissions.Archive,
            _ => throw EmployeeOperation.Invalid("invalid_employee_document_lifecycle")
        };
        return operation.WriteAsync(LifecyclePermissions(medical, permission), "hr.employee.document." + status.ToString().ToLowerInvariant(),
            id, key, new { documentId, medical, status, input }, context, async (actor, token) =>
            {
                var e = await EmployeeAsync(id, token);
                repository.ExpectVersion(e, OrganizationOperation.Version(input.ExpectedVersion));
                var d = await DocumentAsync(id, documentId, medical, token);
                documents.ExpectVersion(d, OrganizationOperation.Version(input.ExpectedDocumentVersion));
                var before = EmployeeDocumentState.Capture(d).Encode();
                try { d.ChangeLifecycle(status, actor.Identity.AccountId.ToString(), input.Reason, clock.GetUtcNow()); }
                catch (InvalidOperationException) { throw EmployeeOperation.Conflict("employee_document_lifecycle_conflict"); }
                var action = status switch
                {
                    EmployeeDocumentLifecycleStatus.Expired => EmployeeDocumentHistoryAction.Expired,
                    EmployeeDocumentLifecycleStatus.Revoked => EmployeeDocumentHistoryAction.Revoked,
                    _ => EmployeeDocumentHistoryAction.Archived
                };
                History(d, action, actor, input.Reason, before);
                await repository.SaveChangesAsync(token);
                return EmployeeHiringService.Result(e) with { DocumentId = d.Id };
            }, ct);
    }

    public Task<EmployeeWriteResult> VerifyAsync(Guid id, Guid documentId, bool medical, EmployeeDocumentVerificationRequest input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(VerifyPermissions(medical), "hr.employee.document.verified", id, key, new { documentId, medical, input }, context,
            async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                if (input.Decision is not (DocumentVerificationStatus.Verified or DocumentVerificationStatus.Rejected) ||
                    input.Reason?.Any(char.IsControl) == true ||
                    (input.Decision == DocumentVerificationStatus.Rejected && string.IsNullOrWhiteSpace(input.Reason)))
                    throw EmployeeOperation.Invalid("invalid_document_verification");
                var e = await EditableAsync(id, input.ExpectedVersion, actor, token);
                var d = await DocumentAsync(id, documentId, medical, token);
                documents.ExpectVersion(d, OrganizationOperation.Version(input.ExpectedDocumentVersion));
                if (d.LifecycleStatus != EmployeeDocumentLifecycleStatus.Current || d.IsScanInProgress)
                    throw EmployeeOperation.Conflict("employee_document_unavailable");
                if (input.Decision == DocumentVerificationStatus.Verified && !d.IsCleanForReviewOn(Today())) throw EmployeeOperation.Conflict("employee_document_not_clean");
                var before = EmployeeDocumentState.Capture(d).Encode();
                d.RecordVerification(input.Decision, actor.Identity.AccountId.ToString(), input.Reason, clock.GetUtcNow());
                History(d, input.Decision == DocumentVerificationStatus.Verified ? EmployeeDocumentHistoryAction.Verified : EmployeeDocumentHistoryAction.VerificationRejected,
                    actor, string.IsNullOrWhiteSpace(input.Reason) ? "Verified by authorized reviewer" : input.Reason, before);
                await repository.SaveChangesAsync(token);
                return EmployeeHiringService.Result(e) with { DocumentId = d.Id };
            }, ct, authorizeResource: (actor, token) => AuthorizeApprovedAsync(id, actor, token));

    public async Task<EmployeeDocumentDownload> DownloadAsync(Guid id, Guid documentId, bool medical, bool review,
        AuthRequestContext context, CancellationToken ct)
    {
        var permissions = review ? VerifyPermissions(medical) : Permissions(medical, false);
        var evidence = await operation.ReadAsync(permissions, "hr.employee.document.download_authorized", id, context, async (_, token) =>
            {
                await EmployeeAsync(id, token);
                var d = await DocumentAsync(id, documentId, medical, token);
                RequireDownloadable(d, review);
                return new { Document = d, Version = Convert.ToBase64String(d.RowVersion), d.StorageKey, d.SizeBytes, d.Sha256 };
            }, ct, auditTarget: result => $"document:{documentId:N}/{result.Version}");
        var content = await ContentAsync(evidence.Document, ct);
        try
        {
            // Storage I/O does not hold the SQL authorization lock. Recheck live grants, employee access and evidence afterward.
            return await operation.ReadAsync(permissions, review ? "hr.employee.document.reviewed" : "hr.employee.document.downloaded",
                id, context, async (_, token) =>
                {
                    await EmployeeAsync(id, token);
                    var d = await DocumentAsync(id, documentId, medical, token, refresh: true);
                    RequireDownloadable(d, review);
                    if (Convert.ToBase64String(d.RowVersion) != evidence.Version || d.StorageKey != evidence.StorageKey ||
                        d.Sha256 != evidence.Sha256 || d.SizeBytes != evidence.SizeBytes)
                        throw EmployeeOperation.Conflict("employee_document_changed_during_download");
                    return new EmployeeDocumentDownload(content, d.ContentType, d.OriginalFileName, d.Id, evidence.Version);
                }, ct, auditTarget: result => $"document:{result.DocumentId:N}/{result.Version}");
        }
        catch { CryptographicOperations.ZeroMemory(content); throw; }
    }

    private async Task<byte[]> ContentAsync(EmployeeDocument d, CancellationToken ct)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(d.StorageKey) ?? throw EmployeeOperation.Conflict("employee_document_unavailable");
        var content = await files.ReadAsync(reference, EmployeeHiringService.Purpose(d.EmployeeId, "document"), ct);
        if (content.LongLength != d.SizeBytes || Convert.ToHexString(SHA256.HashData(content)) != d.Sha256)
        {
            CryptographicOperations.ZeroMemory(content);
            throw EmployeeOperation.Conflict("employee_document_unavailable");
        }
        return content;
    }
    public Task<EmployeeDocumentStatusResponse> StatusAsync(Guid id, Guid documentId, bool medical,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(Permissions(medical, false), "hr.employee.document.status.read", id, context, async (_, token) =>
        {
            await EmployeeAsync(id, token);
            var d = await DocumentAsync(id, documentId, medical, token);
            return new EmployeeDocumentStatusResponse(EmployeeHiringService.MapDocument(d, Today()), d.LifecycleStatus,
                d.IsScanInProgress, d.ActiveScanAttemptId, d.ScanStartedAt, d.ScannedAt, d.ScannerVersion,
                d.SignatureVerified, d.HasScanConflict, d.HistoryRevision, d.LifecycleReason, d.LifecycleChangedAt);
        }, ct, auditTarget: result => $"document:{result.Document.Id:N}/{result.Document.Version}");

    public Task<IReadOnlyList<EmployeeDocumentHistoryResponse>> HistoryAsync(Guid id, Guid documentId, bool medical,
        int afterRevision, int take, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<EmployeeDocumentHistoryResponse>>(Permissions(medical, false), "hr.employee.document.history.read",
            id, context, async (_, token) =>
            {
                if (afterRevision < 0 || take is < 1 or > 100) throw EmployeeOperation.Invalid("invalid_document_history_page");
                await EmployeeAsync(id, token);
                await DocumentAsync(id, documentId, medical, token);
                return (await documents.HistoryAsync(id, documentId, afterRevision, take, token)).Select(h =>
                    new EmployeeDocumentHistoryResponse(h.Id, h.Revision, h.Action, h.ActorUserId, h.Reason, h.RecordedAt,
                        h.ScanAttemptId, h.BeforeState == "{}" ? null : JsonSerializer.Deserialize<EmployeeDocumentState>(h.BeforeState),
                        JsonSerializer.Deserialize<EmployeeDocumentState>(h.AfterState) ?? throw EmployeeOperation.Conflict("employee_document_history_unavailable"))).ToArray();
            }, ct, auditTarget: _ => $"document:{documentId:N}/history/{afterRevision}");

    public Task<EmployeeDocumentCompletenessResponse> CompletenessAsync(Guid id, int expiringWithinDays,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(Permissions(false, false), "hr.employee.document.completeness.read", id, context, async (_, token) =>
        {
            var e = await EmployeeAsync(id, token);
            return await LoadCompletenessAsync(e, Today(), expiringWithinDays, token);
        }, ct);

    public async Task<EmployeeDocumentCompletenessBatchResponse> CompletenessBatchAsync(
        EmployeeDocumentCompletenessBatchRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (request?.EmployeeIds is null) throw EmployeeOperation.Invalid("invalid_employee_document_batch");
        // Snapshot the selection before asynchronous authorization or private I/O.
        var selected = request.EmployeeIds.Order().ToArray();
        var days = request.ExpiringWithinDays;
        if (selected.Length is < 1 or > MaximumCompletenessEmployees || selected.Any(id => id == Guid.Empty) ||
            selected.Distinct().Count() != selected.Length)
            throw EmployeeOperation.Invalid("invalid_employee_document_batch");
        if (days is < 1 or > 365) throw EmployeeOperation.Invalid("invalid_document_expiry_window");
        ct.ThrowIfCancellationRequested();
        var permissions = Permissions(false, false);
        var prepared = await operation.ReadAsync(permissions, "hr.employee.documents.completeness.batch.prepared", null,
            context, async (actor, token) =>
            {
                var timeZone = options.BusinessTimeZoneId;
                var today = Today(clock.GetUtcNow(), timeZone);
                if (today.DayNumber > DateOnly.MaxValue.DayNumber - days)
                    throw new AuthenticationFailure("business_date_out_of_range", 503);
                // All-scope authority covers the explicit population; resolve every ID before any private row reads.
                var employees = new List<Employee>(selected.Length);
                foreach (var id in selected)
                {
                    token.ThrowIfCancellationRequested();
                    employees.Add(await EmployeeAsync(id, token));
                }
                var rows = new List<EmployeeDocumentCompletenessResponse>(selected.Length);
                foreach (var employee in employees)
                {
                    token.ThrowIfCancellationRequested();
                    rows.Add(await LoadCompletenessAsync(employee, today, days, token));
                }
                return new { actor.Identity, TimeZone = timeZone, AsOf = today, Rows = rows.ToArray() };
            }, ct);
        // A separate gate reruns the actual current account/session, web MFA and All-scope authorizer after private I/O.
        return await operation.ReadAsync(permissions, "hr.employee.documents.completeness.batch.read", null,
            context, (actor, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (actor.Identity != prepared.Identity) throw EmployeeOperation.Denied();
                var metadata = new EmployeeDocumentCompletenessBatchMetadata(clock.GetUtcNow(), prepared.TimeZone,
                    prepared.AsOf, prepared.AsOf.AddDays(days), new(selected, days));
                return Task.FromResult(new EmployeeDocumentCompletenessBatchResponse(metadata, prepared.Rows));
            }, ct);
    }

    private async Task<EmployeeDocumentCompletenessResponse> LoadCompletenessAsync(Employee employee, DateOnly today,
        int expiringWithinDays, CancellationToken ct)
    {
        var id = employee.Id;
        Guid[] selected;
        string? categoryCode;
        IReadOnlyList<EmployeeCertificationRequirement> certificates;
        var approved = employee.OnboardingStatus == Cosmix.Domain.HR.Onboarding.OnboardingStatus.Approved;
        if (approved)
        {
            selected = (await sections.LoadAsync(employee, EmployeeChangeSection.Documents, null, ct)).Data.Documents?.DocumentIds
                ?? throw EmployeeOperation.Conflict("employee_document_selection_unavailable");
            certificates = await documents.CertificationsAsync(id, today, ct);
            categoryCode = fieldPolicy is null ? null : await documents.CurrentStaffCategoryCodeAsync(id, today, ct);
        }
        else
        {
            var profile = await forms.DocumentRequirementProfileAsync(id, ct);
            selected = profile.DocumentIds;
            categoryCode = fieldPolicy is null ? null : await repository.StaffCategoryCodeAsync(profile.StaffCategoryId, ct);
            certificates = profile.Certifications.Select((c, index) => new EmployeeCertificationRequirement($"certifications[{index}]",
                c.Type, c.IssuedOn, c.ExpiresOn, c.DocumentId, !string.IsNullOrWhiteSpace(c.Type) &&
                !string.IsNullOrWhiteSpace(c.Number) && !string.IsNullOrWhiteSpace(c.Issuer) && c.IssuedOn.HasValue && c.DocumentId.HasValue)).ToArray();
        }
        var requiredTypes = fieldPolicy is null ? [] : await fieldPolicy.RequiredDocumentTypesAsync(categoryCode, today, ct);
        return EmployeeDocumentCompleteness.Evaluate(id, await repository.DocumentsAsync(id, ct), selected, certificates,
            today, expiringWithinDays, approved, requiredTypes);
    }

    private async Task<EmployeeDocument> DocumentAsync(Guid id, Guid documentId, bool medical, CancellationToken ct, bool refresh = false)
    {
        var doc = await repository.FindDocumentAsync(id, documentId, ct);
        if (doc is not null && refresh) await documents.RefreshAsync(doc, ct);
        return doc is not null && doc.Privacy == Privacy(medical) ? doc : throw EmployeeOperation.Missing();
    }
    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct) => await repository.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
    private async Task AuthorizeApprovedAsync(Guid id, EmployeeActor actor, CancellationToken ct)
    {
        var employee = await EmployeeAsync(id, ct);
        if (employee.OnboardingStatus == Cosmix.Domain.HR.Onboarding.OnboardingStatus.Approved)
            await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.Request], ct);
    }

    private async Task<Employee> EditableAsync(Guid id, string version, EmployeeActor actor, CancellationToken ct)
    {
        var e = await EmployeeAsync(id, ct);
        if (e.OnboardingStatus == Cosmix.Domain.HR.Onboarding.OnboardingStatus.Approved && e.Status == EmployeeStatus.Active &&
            e.ApprovedOnboardingDecisionId.HasValue)
        {
            // New uploads are proposal evidence only. The approved document manifest changes through Admin review.
            await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.Request], ct);
        }
        else EmployeeHiringService.RequireEditable(e);
        repository.ExpectVersion(e, OrganizationOperation.Version(version)); return e;
    }
    private DateOnly Today() => Today(clock.GetUtcNow(), options.BusinessTimeZoneId);
    private static DateOnly Today(DateTimeOffset now, string timeZone)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime); }
        catch (Exception error) when (error is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
    private void RequireDownloadable(EmployeeDocument d, bool review)
    {
        if (review ? !d.IsCleanForReviewOn(Today()) : !d.IsAvailableOn(Today()))
            throw EmployeeOperation.Conflict("employee_document_unavailable");
    }
    private static void RequireAttempt(EmployeeDocument d, Guid attemptId)
    {
        if (!d.IsScanInProgress || d.ActiveScanAttemptId != attemptId || d.LifecycleStatus != EmployeeDocumentLifecycleStatus.Current)
            throw EmployeeOperation.Conflict("employee_document_scan_attempt_conflict");
    }
    private void History(EmployeeDocument d, EmployeeDocumentHistoryAction action, EmployeeActor actor, string reason, string before) =>
        documents.AddHistory(d.RecordHistory(action, actor.Identity.AccountId.ToString(), reason, clock.GetUtcNow(),
            before, EmployeeDocumentState.Capture(d).Encode()));
    private static void ValidateAction(EmployeeDocumentActionRequest input)
    {
        OrganizationOperation.Validate(input);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Any(char.IsControl))
            throw EmployeeOperation.Invalid("invalid_employee_document_reason");
    }
    private async Task<FileScanResult> ScanSafeAsync(byte[] content, CancellationToken ct)
    {
        var copy = content.ToArray();
        try
        {
            FileScanResult result;
            try { result = await scanner.ScanAsync(copy, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { ct.ThrowIfCancellationRequested(); return new(DocumentScanStatus.Failed, "scanner/unavailable"); }
            if (result is null || result.State is not (DocumentScanStatus.Clean or DocumentScanStatus.Rejected or DocumentScanStatus.Failed) ||
                string.IsNullOrWhiteSpace(result.ScannerVersion) || result.ScannerVersion.Length > 150 || result.ScannerVersion.Any(char.IsControl))
                return new(DocumentScanStatus.Failed, "scanner/invalid-response");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }
    private static EmployeeDocumentPrivacy Privacy(bool medical) => medical ? EmployeeDocumentPrivacy.Medical : EmployeeDocumentPrivacy.Standard;
    private static string[] Permissions(bool medical, bool write) => medical
        ? [write ? EmployeePermissions.Edit : EmployeePermissions.Read, write ? EmployeePermissions.DocumentsManage : EmployeePermissions.DocumentsRead,
            write ? EmployeePermissions.MedicalEdit : EmployeePermissions.MedicalRead]
        : [write ? EmployeePermissions.Edit : EmployeePermissions.Read, write ? EmployeePermissions.DocumentsManage : EmployeePermissions.DocumentsRead];
    private static string[] VerifyPermissions(bool medical) => medical
        ? [EmployeePermissions.Read, EmployeePermissions.DocumentsVerify, EmployeePermissions.MedicalRead]
        : [EmployeePermissions.Read, EmployeePermissions.DocumentsVerify];
    private static string[] LifecyclePermissions(bool medical, string action) => medical
        ? [EmployeePermissions.Read, EmployeePermissions.DocumentsManage, EmployeePermissions.MedicalEdit, action]
        : [EmployeePermissions.Read, EmployeePermissions.DocumentsManage, action];

    public static void ValidateFile(EmployeeDocumentType type, string name, string contentType, byte[] content)
    {
        if (!Enum.IsDefined(type) || content is null || content.Length is <= 0 or > MaximumUploadBytes || string.IsNullOrWhiteSpace(name) ||
            name.Length > 180 || name.IndexOfAny(['/', '\\', ':']) >= 0 || name.Any(char.IsControl) || name.Trim() != name)
            throw EmployeeOperation.Invalid("invalid_employee_document");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var bytes = content.AsSpan();
        var png = extension == ".png" && contentType == "image/png" && bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = extension is ".jpg" or ".jpeg" && contentType == "image/jpeg" && bytes.StartsWith(new byte[] {255,216,255}) && bytes.EndsWith(new byte[] {255,217});
        var pdf = extension == ".pdf" && contentType == "application/pdf" && bytes.StartsWith("%PDF-"u8);
        if (!(png || jpeg || pdf) || (type == EmployeeDocumentType.ProfilePhoto && !(png || jpeg)))
            throw EmployeeOperation.Invalid("unsupported_or_mismatched_document_type");
    }
}
