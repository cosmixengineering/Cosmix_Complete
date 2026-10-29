using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeHiringService(EmployeeOperation operation, IEmployeeHiringRepository repository,
    IPrivateFileStore files, MobileAuthenticationOptions options, TimeProvider clock, IEmployeeApprovalRepository approvals,
    IPhoneNumberNormalizer phones, EmployeeFieldPolicy fieldPolicy, IStaffTaxonomyResolver? taxonomy = null)
{
    public Task<EmployeeWriteResult> CreateAsync(string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Edit], "hr.employee.draft.created", null, key, new { }, context,
            async (actor, token) =>
            {
                var employee = new Employee(actor.Now, actor.Identity.AccountId.ToString());
                repository.AddEmployee(employee, new(employee.Id));
                await repository.SaveChangesAsync(token);
                return Result(employee);
            }, ct);

    public Task<PagedResult<EmployeeSummary>> ListAsync(EmployeeListRequest input, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read], "hr.employee.directory.read", null, context, (_, token) =>
        {
            ValidateList(input); return repository.ListAsync(input, token);
        }, ct, concurrentRead: true);

    public Task<EmployeeProfileResponse> GetAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read], "hr.employee.profile.read", id, context, async (actor, token) =>
        {
            var employee = await EmployeeAsync(id, token);
            if (employee.OnboardingStatus == OnboardingStatus.Approved)
                await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.IdentityRead], token);
            var draft = await DraftAsync(id, token);
            return new EmployeeProfileResponse(Summary(employee), await ReadAsync<EmployeeProfileInput>(draft.ProfileReference, id, "profile", token) ?? new());
        }, ct);

    public Task<EmployeeWriteResult> SaveProfileAsync(Guid id, SaveEmployeeSection<EmployeeProfileInput> input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Edit], "hr.employee.profile.saved", id, key, input, context, async (_, token) =>
        {
            OrganizationOperation.Validate(input);
            var employee = await EditableAsync(id, input.ExpectedVersion, token);
            var draft = await DraftAsync(id, token);
            EmployeeFormValidation.ValidateProfile(input.Data, Today());
            var p = EmployeeFormValidation.NormalizePhones(input.Data, phones);
            await fieldPolicy.ValidateDraftAsync(p, Today(), token);
            if (p.MobileAccessRequested && string.IsNullOrWhiteSpace(p.LoginPhoneNumber))
                throw EmployeeOperation.Invalid("employee_login_phone_required");
            await repository.ValidateReferencesAsync(id, p, Today(), token);
            var documents = await repository.DocumentsAsync(id, token);
            if (p.DocumentIds.Any(docId => !documents.Any(d => d.Id == docId && d.Privacy == EmployeeDocumentPrivacy.Standard)))
                throw EmployeeOperation.Invalid("invalid_employee_document_reference");
            employee.UpdateDraft(EmployeeFormValidation.ToDomain(p));
            employee.SelectLoginPhone(p.LoginPhoneNumber, p.MobileAccessRequested);
            await repository.ValidateUniqueAsync(employee, token);
            var reference = await WriteAsync(p, id, "profile", token);
            draft.SetProfile(reference, p.DepartmentId, p.DesignationId, p.StaffCategoryId, p.EmploymentTypeId,
                p.BranchId, p.ReportingManagerEmployeeId, p.ProposedShiftId);
            await repository.SaveChangesAsync(token);
            return Result(employee);
        }, ct);

    public Task<EmployeeSectionResponse<EmployeeFinancialInput>> GetFinancialAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.FinancialRead], "hr.employee.financial.read", id, context,
            async (actor, token) =>
            {
                var e = await EmployeeAsync(id, token); var draft = await DraftAsync(id, token);
                var financial = await ReadAsync<EmployeeFinancialInput>(draft.FinancialReference, id, "financial", token);
                if (financial?.Bank is not null) await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.BankRead], token);
                return new EmployeeSectionResponse<EmployeeFinancialInput>(id, Version(e), financial);
            }, ct);

    public Task<EmployeeWriteResult> SaveFinancialAsync(Guid id, SaveEmployeeSection<EmployeeFinancialInput> input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Edit, EmployeePermissions.FinancialEdit], "hr.employee.financial.saved", id, key, input, context,
            async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                var e = await EditableAsync(id, input.ExpectedVersion, token); var draft = await DraftAsync(id, token);
                EmployeeFormValidation.ValidateFinancial(input.Data);
                var previous = await ReadAsync<EmployeeFinancialInput>(draft.FinancialReference, id, "financial", token);
                if (input.Data.Bank is not null || previous?.Bank is not null)
                    await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.BankRead, EmployeeUpdatePermissions.BankRequest], token);
                var profile = await ReadAsync<EmployeeProfileInput>(draft.ProfileReference, id, "profile", token) ?? new();
                ValidateSalaryReferences(profile, input.Data);
                draft.SetFinancial(await WriteAsync(input.Data, id, "financial", token));
                await repository.SaveChangesAsync(token); return Result(e);
            }, ct, authorizeResource: async (actor, token) =>
            {
                var draft = await DraftAsync(id, token);
                var previous = await ReadAsync<EmployeeFinancialInput>(draft.FinancialReference, id, "financial", token);
                if (input.Data.Bank is not null || previous?.Bank is not null)
                    await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.BankRead, EmployeeUpdatePermissions.BankRequest], token);
            });

    public Task<EmployeeSectionResponse<EmployeeMedicalInput>> GetMedicalAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.MedicalRead], "hr.employee.medical.read", id, context,
            async (_, token) =>
            {
                var e = await EmployeeAsync(id, token); var draft = await DraftAsync(id, token);
                return new EmployeeSectionResponse<EmployeeMedicalInput>(id, Version(e), await ReadAsync<EmployeeMedicalInput>(draft.MedicalReference, id, "medical", token));
            }, ct);

    public Task<EmployeeWriteResult> SaveMedicalAsync(Guid id, SaveEmployeeSection<EmployeeMedicalInput> input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Edit, EmployeePermissions.MedicalEdit], "hr.employee.medical.saved", id, key, input, context,
            async (_, token) =>
            {
                OrganizationOperation.Validate(input);
                var e = await EditableAsync(id, input.ExpectedVersion, token); var draft = await DraftAsync(id, token);
                EmployeeFormValidation.ValidateMedical(input.Data);
                if (input.Data.SupportingDocumentId.HasValue)
                {
                    var doc = await repository.FindDocumentAsync(id, input.Data.SupportingDocumentId.Value, token);
                    if (doc is null || doc.Privacy != EmployeeDocumentPrivacy.Medical) throw EmployeeOperation.Invalid("invalid_medical_document_reference");
                }
                draft.SetMedical(await WriteAsync(input.Data, id, "medical", token));
                await repository.SaveChangesAsync(token); return Result(e);
            }, ct);

    public Task<EmployeeSubmissionValidation> ValidateSubmissionAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.Submit], "hr.employee.submission.validation", id, context,
            async (_, token) =>
            {
                var e = await EmployeeAsync(id, token); RequireEditable(e);
                return await ValidateSubmissionCoreAsync(id, await LoadSnapshotAsync(e, token), token);
            }, ct);

    public Task<EmployeeWriteResult> SubmitAsync(Guid id, SubmitEmployeeRequest input, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Read, EmployeePermissions.Submit], "hr.employee.submitted", id, key, input, context,
            async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                var e = await EditableAsync(id, input.ExpectedVersion, token);
                var snapshot = await LoadSnapshotAsync(e, token);
                var validation = await ValidateSubmissionCoreAsync(id, snapshot, token);
                if (!validation.CanSubmit) throw EmployeeOperation.Conflict("employee_form_incomplete_or_documents_unavailable");
                await repository.ValidateUniqueAsync(e, token);
                var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(id, "submission"), token);
                var submission = e.SubmitForApproval(actor.Identity.AccountId, clock.GetUtcNow(), JsonSerializer.Serialize(reference),
                    reference.ObjectVersion, reference.Sha256, reference.KeyReference, 1);
                repository.AddSubmission(submission);
                await repository.SaveChangesAsync(token);
                return Result(e) with { SubmissionId = submission.Id };
            }, ct);

    public Task<PagedResult<EmployeeSubmissionSummary>> QueueAsync(EmployeeListRequest input, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.Review], "hr.employee.approval_queue.read", null, context,
            (_, token) => { ValidateList(input); return repository.SubmissionsAsync(input, token); }, ct, adminReview: true, concurrentRead: true);

    public Task<EmployeeSubmissionProfile> GetSubmissionAsync(Guid id, Guid submissionId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.Review, EmployeePermissions.DocumentsRead], "hr.employee.submission.read", id, context,
            async (_, token) =>
            {
                var (submission, snapshot) = await SubmissionAsync(id, submissionId, token);
                return new EmployeeSubmissionProfile(submission.Id, id, submission.Version, snapshot.Profile,
                    snapshot.Documents.Where(x => x.Privacy == EmployeeDocumentPrivacy.Standard).ToArray());
            }, ct, adminReview: true, auditTarget: result => $"submission:{result.SubmissionId:N}/{result.Version}");

    public Task<EmployeeFinancialInput?> GetSubmittedFinancialAsync(Guid id, Guid submissionId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<EmployeeFinancialInput?>([EmployeePermissions.Read, EmployeePermissions.Review, EmployeePermissions.FinancialRead], "hr.employee.submitted_financial.read", id, context,
            async (actor, token) =>
            {
                var snapshot = (await SubmissionAsync(id, submissionId, token)).Snapshot;
                if (snapshot.Financial?.Bank is not null) await operation.RequirePermissionsAsync(actor, [EmployeeUpdatePermissions.BankRead], token);
                return snapshot.Financial;
            }, ct, adminReview: true, auditTarget: _ => $"submission:{submissionId:N}");
    public async Task<EmployeeMedicalInput?> GetSubmittedMedicalAsync(Guid id, Guid submissionId, AuthRequestContext context, CancellationToken ct) =>
        (await operation.ReadAsync([EmployeePermissions.Read, EmployeePermissions.Review, EmployeePermissions.MedicalRead], "hr.employee.submitted_medical.read", id, context,
            (_, token) => SubmissionAsync(id, submissionId, token), ct, adminReview: true,
            auditTarget: result => $"submission:{result.Submission.Id:N}/{result.Submission.Version}")).Snapshot.Medical;

    internal async Task<(EmployeeSubmission Submission, HiringSnapshot Snapshot)> SubmissionAsync(Guid id, Guid submissionId, CancellationToken ct)
    {
        var s = await repository.FindSubmissionAsync(id, submissionId, ct) ?? throw EmployeeOperation.Missing();
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(s.SnapshotStorageKey);
        if (reference is null || reference.ObjectVersion != s.SnapshotObjectVersion || reference.Sha256 != s.SnapshotHash || reference.KeyReference != s.EncryptionKeyReference)
            throw EmployeeOperation.Conflict("employee_submission_snapshot_unavailable");
        var snapshot = JsonSerializer.Deserialize<HiringSnapshot>(await files.ReadAsync(reference, Purpose(id, "submission"), ct));
        if (snapshot is null || snapshot.EmployeeId != id || snapshot.DraftRevision != s.DraftRevision || s.SnapshotSchemaVersion != 1)
            throw EmployeeOperation.Conflict("employee_submission_snapshot_unavailable");
        return (s, snapshot);
    }
    private async Task<HiringSnapshot> LoadSnapshotAsync(Employee e, CancellationToken ct)
    {
        var today = Today();
        var draft = await DraftAsync(e.Id, ct);
        var profile = await ReadAsync<EmployeeProfileInput>(draft.ProfileReference, e.Id, "profile", ct) ?? new();
        var financial = await ReadAsync<EmployeeFinancialInput>(draft.FinancialReference, e.Id, "financial", ct);
        var medical = await ReadAsync<EmployeeMedicalInput>(draft.MedicalReference, e.Id, "medical", ct);
        var all = await repository.DocumentsAsync(e.Id, ct);
        var documents = all.Where(d => profile.DocumentIds.Contains(d.Id) || d.Id == medical?.SupportingDocumentId)
            .Select(d => MapDocument(d, today)).ToArray();
        EmployeeHiringTaxonomyEvidence? taxonomyEvidence = null;
        EmployeeHiringTaxonomyEvidence? activationTaxonomyEvidence = null;
        if (profile.StaffCategoryId.HasValue && await repository.StaffCategoryCodeAsync(profile.StaffCategoryId, ct) is { } code)
        {
            var on = profile.JoinedOn ?? today;
            var resolution = await ResolveTaxonomyAsync(code, on, ct);
            StaffTaxonomyResolver.RequireMapping(resolution);
            taxonomyEvidence = Evidence(code, on, resolution);
            var activation = await ResolveTaxonomyAsync(code, today, ct);
            StaffTaxonomyResolver.RequireMapping(activation);
            activationTaxonomyEvidence = Evidence(code, today, activation);
        }
        var foundationFieldPolicyVersion = await fieldPolicy.AuthorityVersionAsync(today, ct);
        return new(e.Id, e.EmployeeNumber, e.DraftRevision, profile, financial, medical, documents, taxonomyEvidence,
            foundationFieldPolicyVersion, activationTaxonomyEvidence);
    }
    internal async Task<EmployeeSubmissionValidation> ValidateSubmissionCoreAsync(Guid id, HiringSnapshot snapshot,
        CancellationToken ct, bool revalidateTaxonomy = false)
    {
        var p = snapshot.Profile; var today = Today();
        if (revalidateTaxonomy && snapshot.FoundationFieldPolicyVersion != await fieldPolicy.AuthorityVersionAsync(today, ct))
            throw EmployeeOperation.Conflict("employee_field_policy_changed_review_required");
        EmployeeFormValidation.ValidateProfile(p, today);
        await repository.ValidateReferencesAsync(id, p, today, ct);
        var missing = EmployeeFormValidation.RequiredFields(p).ToList();
        missing.AddRange(await fieldPolicy.MissingFieldsAsync(p, today, ct));
        if (await repository.StaffCategoryCodeAsync(p.StaffCategoryId, ct) is { } categoryCode)
        {
            var on = p.JoinedOn ?? today;
            var resolution = await ResolveTaxonomyAsync(categoryCode, on, ct);
            var activation = await ResolveTaxonomyAsync(categoryCode, today, ct);
            if (revalidateTaxonomy)
            {
                // Pre-taxonomy immutable snapshots remain approvable only under the exact historical baseline.
                // A published mapping, including one with an unchanged label, requires a freshly pinned proposal.
                if (snapshot.Taxonomy is null)
                {
                    if (!resolution.Baseline || !resolution.Available)
                        throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
                }
                else if (snapshot.Taxonomy != Evidence(categoryCode, on, resolution))
                    throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
                // Old protected snapshots had one historical pin. They cannot adopt a later publication
                // without a new proposal that freezes the independent activation interpretation.
                if (snapshot.ActivationTaxonomy is null)
                {
                    if (!resolution.Available || !resolution.Baseline || !activation.Available || !activation.Baseline ||
                        activation.Fingerprint != resolution.Fingerprint || activation.PolicyVersionId != resolution.PolicyVersionId)
                        throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
                }
                else if (!activation.Available || !SameIdentity(snapshot.ActivationTaxonomy, Evidence(categoryCode, today, activation)))
                    throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
            }
            else if (snapshot.Taxonomy is not null && snapshot.Taxonomy != Evidence(categoryCode, on, resolution))
                throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
            else if (snapshot.ActivationTaxonomy is not null &&
                !SameIdentity(snapshot.ActivationTaxonomy, Evidence(categoryCode, today, activation)))
                throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
            var joinedMapping = StaffTaxonomyResolver.RequireMapping(resolution);
            var activationMapping = StaffTaxonomyResolver.RequireMapping(activation);
            if (joinedMapping.WorkLocation != activationMapping.WorkLocation || joinedMapping.ScheduleBasis != activationMapping.ScheduleBasis)
                throw EmployeeOperation.Conflict("employee_staff_taxonomy_location_changed_review_required");
            // A separate reviewed future-activation workflow is still required when a future appointment
            // depends on a different publication. Ordinary past joining dates retain independent history.
            if (on > today && (resolution.Fingerprint != activation.Fingerprint || resolution.PolicyVersionId != activation.PolicyVersionId))
                throw EmployeeOperation.Conflict("employee_staff_taxonomy_changed_review_required");
            // initialSiteId belongs to the independent approval decision. Submission requires the office branch,
            // but defers the site's activation requirement until that decision supplies the initial site.
            StaffTaxonomyResolver.ValidateLocation(resolution, p.BranchId, null, StaffTaxonomyValidationPhase.Draft);
            missing.AddRange(StaffTaxonomyResolver.MissingLocationFields(activation, p.BranchId, null,
                StaffTaxonomyValidationPhase.Activation).Where(field => field != "siteId"));
        }
        if (p.IdentityDocumentExpiresOn < today) missing.Add("identityDocumentExpiresOn");
        if (p.MobileAccessRequested && string.IsNullOrWhiteSpace(p.LoginPhoneNumber)) missing.Add("loginPhoneNumber");
        for (var i = 0; i < p.PreviousEmployment.Length; i++)
        {
            var previous = p.PreviousEmployment[i];
            if (string.IsNullOrWhiteSpace(previous.EmployerName) || string.IsNullOrWhiteSpace(previous.RoleTitle) || !previous.StartedOn.HasValue)
                missing.Add($"previousEmployment[{i}]");
        }
        for (var i = 0; i < p.Certifications.Length; i++)
        {
            var certificate = p.Certifications[i];
            if (string.IsNullOrWhiteSpace(certificate.Type) || string.IsNullOrWhiteSpace(certificate.Number) ||
                string.IsNullOrWhiteSpace(certificate.Issuer) || !certificate.IssuedOn.HasValue || !certificate.DocumentId.HasValue || certificate.ExpiresOn < today)
                missing.Add($"certifications[{i}]");
        }
        for (var i = 0; i < p.Assets.Length; i++)
            if (string.IsNullOrWhiteSpace(p.Assets[i].AssetReference) || !p.Assets[i].IssuedOn.HasValue || string.IsNullOrWhiteSpace(p.Assets[i].IssueCondition))
                missing.Add($"assets[{i}]");
        if (snapshot.Financial is not null) { EmployeeFormValidation.ValidateFinancial(snapshot.Financial); ValidateSalaryReferences(p, snapshot.Financial); }
        if (snapshot.Medical is not null) EmployeeFormValidation.ValidateMedical(snapshot.Medical);
        try { EmployeeFormValidation.ValidateCompleteSections(p, snapshot.Financial, snapshot.Medical); }
        catch (AuthenticationFailure failure) when (failure.Code is "employee_compensation_incomplete" or
            "employee_bank_details_incomplete" or "employee_previous_salary_incomplete")
        {
            missing.Add(failure.Code switch
            {
                "employee_compensation_incomplete" => "financial.compensation",
                "employee_bank_details_incomplete" => "financial.bank",
                _ => "financial.previousSalaries"
            });
        }
        var docs = snapshot.Documents;
        var available = docs.Where(d => d.IsAvailable && (!d.IssuedOn.HasValue || d.IssuedOn <= today) &&
            (!d.ExpiresOn.HasValue || d.ExpiresOn >= today)).ToArray();
        if (!available.Any(d => d.Type == EmployeeDocumentType.ProfilePhoto && d.Privacy == EmployeeDocumentPrivacy.Standard)) missing.Add("documents.profilePhoto");
        if (!available.Any(d => d.Type == EmployeeDocumentType.Identity && d.Privacy == EmployeeDocumentPrivacy.Standard)) missing.Add("documents.identity");
        var documentCategoryCode = await repository.StaffCategoryCodeAsync(p.StaffCategoryId, ct);
        foreach (var requiredType in await fieldPolicy.RequiredDocumentTypesAsync(documentCategoryCode, today, ct))
            if (!available.Any(d => d.Type == requiredType && d.Privacy == EmployeeDocumentPrivacy.Standard && p.DocumentIds.Contains(d.Id)))
                missing.Add("documents.required." + requiredType);
        var unavailable = p.DocumentIds.Where(docId => !available.Any(d => d.Id == docId && d.Privacy == EmployeeDocumentPrivacy.Standard)).ToList();
        foreach (var docId in p.Certifications.Where(x => x.DocumentId.HasValue).Select(x => x.DocumentId!.Value)
                     .Concat(p.Assets.Where(x => x.EvidenceDocumentId.HasValue).Select(x => x.EvidenceDocumentId!.Value)))
            if (!p.DocumentIds.Contains(docId) || !available.Any(d => d.Id == docId)) unavailable.Add(docId);
        if (snapshot.Medical?.SupportingDocumentId is Guid medicalId && !available.Any(d => d.Id == medicalId && d.Privacy == EmployeeDocumentPrivacy.Medical))
            missing.Add("medical.supportingDocument");
        await approvals.ValidateAssetReturnReceiversAsync(p.Assets, ct);
        return new(missing.Count == 0 && unavailable.Count == 0, missing.Distinct().ToArray(), unavailable.Distinct().ToArray());
    }
    private static void ValidateSalaryReferences(EmployeeProfileInput p, EmployeeFinancialInput f)
    {
        if (f.PreviousSalaries.Any(s => !p.PreviousEmployment.Any(e => e.EntryId == s.EmploymentEntryId)))
            throw EmployeeOperation.Invalid("invalid_previous_salary_reference");
    }
    private async Task<string> WriteAsync<T>(T data, Guid id, string section, CancellationToken ct) =>
        JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(data), Purpose(id, section), ct));
    private async Task<T?> ReadAsync<T>(string? reference, Guid id, string section, CancellationToken ct) where T : class =>
        reference is null ? null : JsonSerializer.Deserialize<T>(await files.ReadAsync(JsonSerializer.Deserialize<PrivateFileReference>(reference)!, Purpose(id, section), ct))
        ?? throw EmployeeOperation.Conflict("employee_protected_section_unavailable");
    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct) => await repository.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
    private async Task<EmployeeHiringDraft> DraftAsync(Guid id, CancellationToken ct) => await repository.FindDraftAsync(id, ct) ?? throw EmployeeOperation.Missing();
    private async Task<Employee> EditableAsync(Guid id, string version, CancellationToken ct)
    {
        var e = await EmployeeAsync(id, ct); RequireEditable(e); repository.ExpectVersion(e, OrganizationOperation.Version(version)); return e;
    }
    internal static void RequireEditable(Employee e)
    {
        if (e.Status != EmployeeStatus.Draft || e.OnboardingStatus is not (OnboardingStatus.Draft or OnboardingStatus.Returned))
            throw EmployeeOperation.Conflict("employee_form_not_editable");
    }
    internal DateOnly Today()
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime); }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
    private static void ValidateList(EmployeeListRequest input)
    {
        OrganizationOperation.Validate(input);
        if (input.Status.HasValue && !Enum.IsDefined(input.Status.Value)) throw EmployeeOperation.Invalid("invalid_employee_status");
    }
    internal static string Purpose(Guid id, string section) => $"employee/{id:N}/{section}";
    internal static string Version(Employee e) => Convert.ToBase64String(e.RowVersion);
    internal static EmployeeWriteResult Result(Employee e) => new(e.Id, Version(e), e.OnboardingStatus);
    private static EmployeeSummary Summary(Employee e) => new(
        e.Id,
        e.EmployeeNumber,
        e.FullLegalName,
        e.Status,
        e.OnboardingStatus,
        Version(e),
        e.IsArchived,
        e.CompanyPhone ?? e.PersonalPhone,
        e.OfficialEmail ?? e.PersonalEmail,
        e.JoinedOn,
        e.Gender,
        e.IdentityDocumentNumber,
        e.CreatedAt,
        e.MobileAccessRequested);
    internal static EmployeeDocumentResponse MapDocument(EmployeeDocument d) => new(d.Id, d.DocumentType, d.OriginalFileName,
        d.ContentType, d.SizeBytes, d.IssuedOn, d.ExpiresOn, d.ScanStatus, d.VerificationStatus, d.IsAvailable, Convert.ToBase64String(d.RowVersion), d.Privacy,
        d.LifecycleStatus, d.IsScanInProgress, d.HistoryRevision);
    internal static EmployeeDocumentResponse MapDocument(EmployeeDocument d, DateOnly on) =>
        MapDocument(d) with { IsAvailable = d.IsAvailableOn(on) };
    internal async Task<EmployeeProfileInput> DocumentRequirementProfileAsync(Guid id, CancellationToken ct)
    {
        var draft = await DraftAsync(id, ct);
        return await ReadAsync<EmployeeProfileInput>(draft.ProfileReference, id, "profile", ct) ?? new();
    }
    private Task<StaffTaxonomyResolution> ResolveTaxonomyAsync(string code, DateOnly on, CancellationToken ct) =>
        taxonomy is null ? Task.FromResult(StaffTaxonomyResolver.Resolve(code, on, [])) : taxonomy.ResolveAsync(code, on, ct);
    private static EmployeeHiringTaxonomyEvidence Evidence(string code, DateOnly on, StaffTaxonomyResolution resolution) =>
        new(code, on, resolution.PolicyVersionId, resolution.Fingerprint, resolution.Baseline);
    private static bool SameIdentity(EmployeeHiringTaxonomyEvidence frozen, EmployeeHiringTaxonomyEvidence current) =>
        frozen.CategoryCode == current.CategoryCode && frozen.PolicyVersionId == current.PolicyVersionId &&
        frozen.Fingerprint == current.Fingerprint && frozen.Baseline == current.Baseline;
    internal sealed record HiringSnapshot(Guid EmployeeId, long EmployeeNumber, int DraftRevision,
        EmployeeProfileInput Profile, EmployeeFinancialInput? Financial, EmployeeMedicalInput? Medical,
        IReadOnlyList<EmployeeDocumentResponse> Documents, EmployeeHiringTaxonomyEvidence? Taxonomy = null,
        string? FoundationFieldPolicyVersion = null, EmployeeHiringTaxonomyEvidence? ActivationTaxonomy = null);
}
