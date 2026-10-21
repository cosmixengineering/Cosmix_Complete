using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

/// <summary>Materializes typed sections from the approved immutable snapshot, never from the mutable draft.</summary>
public sealed class EmployeeApprovalMaterializer(IEmployeeApprovalRepository repository,
    IEmployeeHiringRepository hiring, IPrivateFileStore files, MobileAuthenticationOptions options, TimeProvider clock,
    EmployeeFieldPolicy fieldPolicy, IStaffTaxonomyResolver? taxonomy = null)
{
    public Task ValidateAsync(Employee employee, EmployeeProfileInput profile, EmployeeFinancialInput? financial,
        EmployeeMedicalInput? medical, Guid? initialSiteId, CancellationToken ct) =>
        ValidateCoreAsync(employee, profile, financial, medical, initialSiteId, clock.GetUtcNow(), ct);

    /// <summary>Call after saving the decision, with its tracked approved employee, inside the same outer transaction.</summary>
    public async Task MaterializeAsync(Employee employee, EmployeeProfileInput profile, EmployeeFinancialInput? financial,
        EmployeeMedicalInput? medical, Guid? initialSiteId, string actorId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (employee.OnboardingStatus != OnboardingStatus.Approved || employee.Status != EmployeeStatus.Active ||
            !employee.ApprovedOnboardingDecisionId.HasValue)
            throw Conflict("employee_approval_required");
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Trim().Length > 200)
            throw Invalid("invalid_employee_approval_actor");
        await ValidateCoreAsync(employee, profile, financial, medical, initialSiteId, now, ct);

        var effectiveFrom = profile.JoinedOn!.Value;
        var assignment = new EmployeeAssignment(employee.Id, profile.DepartmentId!.Value, profile.DesignationId!.Value,
            profile.StaffCategoryId!.Value, profile.EmploymentTypeId!.Value, profile.BranchId, initialSiteId,
            profile.ReportingManagerEmployeeId, effectiveFrom, null, actorId, now);
        var addresses = new[]
        {
            Address(employee.Id, EmployeeAddressType.Current, profile.CurrentAddress!),
            Address(employee.Id, EmployeeAddressType.Permanent, profile.PermanentAddress!)
        };
        var contact = profile.EmergencyContact!;
        var emergency = new EmployeeEmergencyContact(employee.Id, contact.Name, contact.Relationship,
            contact.Phone, contact.AlternatePhone);
        var certificates = profile.Certifications.Select(c => new EmployeeCertification(employee.Id, c.Type!, c.Number!,
            c.Issuer!, c.IssuedOn!.Value, c.ExpiresOn, c.DocumentId, actorId, now)).ToArray();
        var assets = profile.Assets.Select(a =>
        {
            var custody = new EmployeeAssetCustody(employee.Id, a.AssetReference!,
                StartOfBusinessDay(a.IssuedOn!.Value), a.IssueCondition!, a.EvidenceDocumentId is Guid documentId
                    ? $"employee/{employee.Id:N}/document/{documentId:N}" : null, actorId);
            if (a.ReturnedOn is DateOnly returnedOn)
                custody.RecordReturn(StartOfBusinessDay(returnedOn), a.ReturnCondition!, a.ReturnedToAccountId!.Value.ToString(), null);
            return custody;
        }).ToArray();
        var compensation = financial is not null && EmployeeFormValidation.HasCompensation(financial)
            ? new CompensationRecord(employee.Id, 1, null, financial.BaseSalary!.Value, financial.GrossSalary!.Value,
                financial.Currency!, financial.WageBasis!.Value, financial.PaymentMethod!.Value,
                financial.EffectiveFrom!.Value, null, null, actorId, now)
            : null;

        var previousEmployment = new List<PreviousEmployment>(profile.PreviousEmployment.Length);
        foreach (var previous in profile.PreviousEmployment)
        {
            var salary = financial?.PreviousSalaries.SingleOrDefault(s => s.EmploymentEntryId == previous.EntryId);
            var salaryReference = salary is null ? null : JsonSerializer.Serialize(await ProtectAsync(salary, employee.Id,
                "approved/previous-salary", ct));
            previousEmployment.Add(new PreviousEmployment(employee.Id, previous.EmployerName!, previous.RoleTitle!,
                previous.StartedOn!.Value, previous.EndedOn, previous.LeavingReason, salaryReference, actorId, now));
        }

        EmployeeMedicalRecord? medicalRecord = null;
        if (medical is not null)
        {
            var reference = await ProtectAsync(medical, employee.Id, "approved/medical", ct);
            medicalRecord = new EmployeeMedicalRecord(employee.Id, JsonSerializer.Serialize(reference), reference.KeyReference,
                effectiveFrom, medical.SupportingDocumentId, actorId, now);
        }
        EmployeeBankDetails? bankRecord = null;
        if (financial?.Bank is { } bank)
        {
            var bankName = await ProtectAsync(bank.BankName!.Trim(), employee.Id, "approved/bank/name", ct);
            var title = await ProtectAsync(bank.AccountTitle!.Trim(), employee.Id, "approved/bank/title", ct);
            var account = await ProtectAsync(bank.AccountNumber!.Trim(), employee.Id, "approved/bank/account", ct);
            var iban = string.IsNullOrWhiteSpace(bank.Iban) ? null
                : await ProtectAsync(bank.Iban.Trim(), employee.Id, "approved/bank/iban", ct);
            if (title.KeyReference != bankName.KeyReference || account.KeyReference != bankName.KeyReference ||
                (iban is not null && iban.KeyReference != bankName.KeyReference))
                throw Conflict("employee_bank_key_reference_mismatch");
            var number = bank.AccountNumber.Trim();
            var masked = number.Length > 4 ? "****" + number[^4..] : "****";
            bankRecord = new EmployeeBankDetails(employee.Id, 1, JsonSerializer.Serialize(bankName),
                JsonSerializer.Serialize(title), JsonSerializer.Serialize(account),
                iban is null ? null : JsonSerializer.Serialize(iban), bankName.KeyReference, masked,
                false, null, null, effectiveFrom, null, actorId, now);
        }

        // The form has no SIM identifier/return date. Its SIM fields and proposed shift stay in the approved snapshot.
        // Stage only after every protected write succeeds; the caller saves this graph in the decision transaction.
        ct.ThrowIfCancellationRequested();
        repository.AddInitialRecords(new(assignment, addresses, emergency, previousEmployment,
            certificates, assets, medicalRecord, compensation, bankRecord));
    }

    private async Task ValidateCoreAsync(Employee employee, EmployeeProfileInput profile, EmployeeFinancialInput? financial,
        EmployeeMedicalInput? medical, Guid? initialSiteId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ct.ThrowIfCancellationRequested();
        var today = Today(now);
        EmployeeFormValidation.ValidateProfile(profile, today);
        if (EmployeeFormValidation.RequiredFields(profile).Count != 0 || (await fieldPolicy.MissingFieldsAsync(profile, today, ct)).Count != 0)
            throw Invalid("employee_approval_profile_incomplete");
        if (employee.JoinedOn != profile.JoinedOn)
            throw Conflict("employee_approval_joining_date_mismatch");
        foreach (var previous in profile.PreviousEmployment)
            if (string.IsNullOrWhiteSpace(previous.EmployerName) || string.IsNullOrWhiteSpace(previous.RoleTitle) || !previous.StartedOn.HasValue)
                throw Invalid("employee_previous_employment_incomplete");
        foreach (var certificate in profile.Certifications)
            if (string.IsNullOrWhiteSpace(certificate.Type) || string.IsNullOrWhiteSpace(certificate.Number) ||
                string.IsNullOrWhiteSpace(certificate.Issuer) || !certificate.IssuedOn.HasValue)
                throw Invalid("employee_certification_incomplete");
        foreach (var asset in profile.Assets)
            if (string.IsNullOrWhiteSpace(asset.AssetReference) || string.IsNullOrWhiteSpace(asset.IssueCondition) || !asset.IssuedOn.HasValue)
                throw Invalid("employee_asset_custody_incomplete");
        if (profile.ReportingManagerEmployeeId == employee.Id)
            throw Invalid("ineligible_reporting_manager");
        EmployeeFormValidation.ValidateCompleteSections(profile, financial, medical);
        await hiring.ValidateReferencesAsync(employee.Id, profile, today, ct);
        await repository.ValidateAssetReturnReceiversAsync(profile.Assets, ct);
        var category = await hiring.StaffCategoryCodeAsync(profile.StaffCategoryId, ct);
        if (category is null) throw Invalid("unsupported_staff_category");
        var resolution = taxonomy is null ? StaffTaxonomyResolver.Resolve(category, profile.JoinedOn!.Value, []) :
            await taxonomy.ResolveAsync(category, profile.JoinedOn!.Value, ct);
        var joinedMapping = StaffTaxonomyResolver.RequireMapping(resolution);
        var activation = taxonomy is null ? StaffTaxonomyResolver.Resolve(category, today, []) :
            await taxonomy.ResolveAsync(category, today, ct);
        var mapping = StaffTaxonomyResolver.RequireMapping(activation);
        if (joinedMapping.WorkLocation != mapping.WorkLocation || joinedMapping.ScheduleBasis != mapping.ScheduleBasis)
            throw Conflict("employee_staff_taxonomy_location_changed_review_required");
        if (profile.JoinedOn.Value > today && (resolution.Fingerprint != activation.Fingerprint || resolution.PolicyVersionId != activation.PolicyVersionId))
            throw Conflict("employee_staff_taxonomy_changed_review_required");
        StaffTaxonomyResolver.ValidateLocation(resolution, profile.BranchId, null, StaffTaxonomyValidationPhase.Draft);
        if (activation.Baseline)
        {
            if (mapping.WorkLocation == StaffWorkLocation.Office && !profile.BranchId.HasValue) throw Invalid("employee_office_required");
            if (mapping.WorkLocation == StaffWorkLocation.Office && initialSiteId.HasValue) throw Invalid("office_staff_cannot_select_initial_site");
            if (mapping.WorkLocation == StaffWorkLocation.Site && profile.BranchId.HasValue) throw Invalid("site_staff_cannot_select_office");
        }
        StaffTaxonomyResolver.ValidateLocation(activation, profile.BranchId, initialSiteId, StaffTaxonomyValidationPhase.Activation);
        await repository.ValidateInitialSiteAsync(initialSiteId, profile.JoinedOn!.Value, ct);
        if (today > profile.JoinedOn.Value)
            await repository.ValidateInitialSiteAsync(initialSiteId, today, ct);
        if (await repository.HasInitialRecordsAsync(employee.Id, ct))
            throw Conflict("employee_already_materialized");
    }

    private static EmployeeAddress Address(Guid employeeId, EmployeeAddressType type, AddressInput address) =>
        new(employeeId, type, address.Line1, address.Line2, address.City, address.Province, address.PostalCode, address.CityId);

    private async Task<PrivateFileReference> ProtectAsync<T>(T value, Guid employeeId, string section, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var reference = await files.WriteAsync(payload, $"employee/{employeeId:N}/{section}", ct);
            if (JsonSerializer.Serialize(reference).Length > 500 || string.IsNullOrWhiteSpace(reference.KeyReference) ||
                reference.KeyReference.Length > 200)
                throw new AuthenticationFailure("private_file_reference_invalid", 503);
            return reference;
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, BusinessTimeZone()).DateTime);
    private DateTimeOffset StartOfBusinessDay(DateOnly day) => new(TimeZoneInfo.ConvertTimeToUtc(
        day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), BusinessTimeZone()));
    private TimeZoneInfo BusinessTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId); }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }

    private static AuthenticationFailure Invalid(string code) => new(code, 400);
    private static AuthenticationFailure Conflict(string code) => new(code, 409);
}
