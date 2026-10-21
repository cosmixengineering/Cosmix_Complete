using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

/// <summary>Typed current records and protected snapshots for post-hire edits. Hiring snapshots stay immutable.</summary>
public sealed class EmployeeUpdateSections(IEmployeeUpdateRepository repository, IEmployeeHiringRepository hiring, IPrivateFileStore files,
    IEmployeeBankRepository bankVerification, EmployeeFieldPolicy? fieldPolicy = null,
    IEmployeeDocumentRepository? categoryDocuments = null)
{
    public async Task<EmployeeUpdateValue> LoadAsync(Employee employee, EmployeeChangeSection section, DateOnly? asOf, CancellationToken ct)
    {
        var start = employee.JoinedOn ?? throw EmployeeOperation.Conflict("employee_joining_date_missing");
        var latestChange = EmployeeUpdateValidation.Dated(section) ? null : await repository.LastChangeAsync(employee.Id, section, ct);
        if (latestChange is not null) start = latestChange.EffectiveFrom;
        switch (section)
        {
            case EmployeeChangeSection.Basic:
                var addresses = await repository.AddressesAsync(employee.Id, ct);
                var emergency = await repository.EmergencyAsync(employee.Id, ct);
                var current = addresses.SingleOrDefault(x => x.AddressType == EmployeeAddressType.Current);
                var permanent = addresses.SingleOrDefault(x => x.AddressType == EmployeeAddressType.Permanent);
                return new(null, start, null, new(Basic: new(Address(current), Address(permanent), new EmergencyContactInput
                { Name = emergency?.Name, Relationship = emergency?.Relationship, Phone = emergency?.Phone, AlternatePhone = emergency?.AlternatePhone },
                    employee.PersonalEmail, employee.MaritalStatus)));
            case EmployeeChangeSection.Identity:
                Guid proofId;
                if (latestChange is not null)
                    proofId = (await ReadAsync<EmployeeUpdateValue>(latestChange.AfterPrivateReference, employee.Id, "changes/history", ct))
                        .Data.Identity?.IdentityDocumentId ?? throw EmployeeOperation.Conflict("employee_identity_evidence_unavailable");
                else
                {
                    var manifest = await HiringDocumentsAsync(employee, ct);
                    var document = (await hiring.DocumentsAsync(employee.Id, ct)).FirstOrDefault(d => manifest.Contains(d.Id) &&
                        d.Privacy == EmployeeDocumentPrivacy.Standard && d.DocumentType == EmployeeDocumentType.Identity);
                    proofId = document?.Id ?? Guid.Empty;
                }
                return new(null, start, null, new(Identity: new(employee.FullLegalName ?? "", employee.FatherOrSpouseName,
                    employee.IdentityDocumentType ?? 0, employee.IdentityDocumentNumber ?? "", employee.IdentityDocumentExpiresOn,
                    employee.DateOfBirth ?? default, employee.Gender, employee.PersonalPhone ?? "", employee.CompanyPhone,
                    employee.OfficialEmail, proofId)));
            case EmployeeChangeSection.Employment:
                var assignment = await repository.AssignmentAsync(employee.Id, asOf, ct);
                return assignment is null ? new(null, start, null, new()) : new(assignment.Id, assignment.EffectiveFrom, assignment.EffectiveTo,
                    new(Employment: new(assignment.DepartmentId, assignment.DesignationId, assignment.ReportingManagerId, assignment.BranchId)));
            case EmployeeChangeSection.Bank:
                var bank = await repository.BankAsync(employee.Id, asOf, ct);
                if (bank is null) return new(null, start, null, new());
                var details = new BankInput
                {
                    BankName = await ReadAsync<string>(bank.BankNameEncryptedReference, employee.Id, "approved/bank/name", ct),
                    AccountTitle = await ReadAsync<string>(bank.AccountTitleEncryptedReference, employee.Id, "approved/bank/title", ct),
                    AccountNumber = await ReadAsync<string>(bank.AccountNumberEncryptedReference, employee.Id, "approved/bank/account", ct),
                    Iban = bank.IbanEncryptedReference is null ? null : await ReadAsync<string>(bank.IbanEncryptedReference, employee.Id, "approved/bank/iban", ct)
                };
                if (await bankVerification.HasConflictingHistoryAsync(employee.Id, bank.Id, ct))
                    throw EmployeeOperation.Conflict("employee_bank_history_conflict");
                var verification = await bankVerification.LatestAsync(employee.Id, bank.Id, ct);
                return new(bank.Id, bank.EffectiveFrom, bank.EffectiveTo, new(Bank: details), verification?.IsVerified ?? bank.IsVerified);
            case EmployeeChangeSection.Medical:
                var medical = await repository.MedicalAsync(employee.Id, asOf, ct);
                return medical is null ? new(null, start, null, new()) : new(medical.Id, medical.EffectiveOn, null,
                    new(Medical: await ReadAsync<EmployeeMedicalInput>(medical.EncryptedPayloadReference, employee.Id, "approved/medical", ct)));
            case EmployeeChangeSection.Documents:
                return new(null, start, null, new(Documents: new(await DocumentsAsync(employee, ct))));
            default: throw EmployeeOperation.Invalid("invalid_employee_change_section");
        }
    }

    public async Task ValidateAsync(Employee employee, EmployeeChangeSection section, EmployeeUpdateData data,
        DateOnly effective, DateTimeOffset now, CancellationToken ct)
    {
        if (section == EmployeeChangeSection.Basic) await repository.ValidateBasicAsync(data.Basic!, ct);
        if (section == EmployeeChangeSection.Identity)
        {
            await repository.ValidateIdentityUniqueAsync(employee.Id, data.Identity!, ct);
            var proof = await DocumentAsync(employee.Id, data.Identity!.IdentityDocumentId, EmployeeDocumentPrivacy.Standard, effective, ct);
            if (proof.DocumentType != EmployeeDocumentType.Identity) throw EmployeeOperation.Invalid("employee_identity_document_required");
        }
        if (section == EmployeeChangeSection.Employment)
        {
            var previous = await repository.AssignmentAsync(employee.Id, null, ct) ?? throw EmployeeOperation.Conflict("employee_assignment_missing");
            await repository.ValidateEmploymentAsync(employee.Id, data.Employment!, previous, effective, now, ct);
        }
        if (section == EmployeeChangeSection.Medical && data.Medical?.SupportingDocumentId is Guid medicalId)
            await DocumentAsync(employee.Id, medicalId, EmployeeDocumentPrivacy.Medical, effective, ct);
        if (section == EmployeeChangeSection.Documents)
        {
            var documents = new List<EmployeeDocument>();
            foreach (var id in data.Documents!.DocumentIds)
                documents.Add(await DocumentAsync(employee.Id, id, EmployeeDocumentPrivacy.Standard, effective, ct));
            if (!documents.Any(d => d.DocumentType == EmployeeDocumentType.Identity) || !documents.Any(d => d.DocumentType == EmployeeDocumentType.ProfilePhoto))
                throw EmployeeOperation.Invalid("employee_required_documents_missing");
            if (fieldPolicy is not null)
            {
                if (categoryDocuments is null) throw EmployeePublishedPolicyOptions.Unavailable();
                var category = await categoryDocuments.CurrentStaffCategoryCodeAsync(employee.Id, effective, ct);
                var required = await fieldPolicy.RequiredDocumentTypesAsync(category, effective, ct);
                if (required.Any(type => !documents.Any(document => document.DocumentType == type)))
                    throw EmployeeOperation.Invalid("employee_published_required_documents_missing");
            }
        }
    }

    public async Task<EmployeeUpdateValue> ApplyAsync(Employee employee, EmployeeChangeSection section, EmployeeUpdateData data,
        DateOnly effective, EmployeeActor actor, CancellationToken ct)
    {
        switch (section)
        {
            case EmployeeChangeSection.Basic:
                var basic = data.Basic!;
                employee.UpdateApprovedBasic(new(basic.PersonalEmail, basic.MaritalStatus));
                var addresses = await repository.AddressesAsync(employee.Id, ct);
                foreach (var (type, input) in new[] { (EmployeeAddressType.Current, basic.CurrentAddress), (EmployeeAddressType.Permanent, basic.PermanentAddress) })
                {
                    var row = addresses.SingleOrDefault(x => x.AddressType == type);
                    if (row is null) repository.AddAddress(new(employee.Id, type, input.Line1, input.Line2, input.City, input.Province, input.PostalCode, input.CityId));
                    else row.UpdateDetails(input.Line1, input.Line2, input.City, input.Province, input.PostalCode, input.CityId);
                }
                var emergency = await repository.EmergencyAsync(employee.Id, ct);
                if (emergency is null) repository.AddEmergency(new(employee.Id, basic.EmergencyContact.Name, basic.EmergencyContact.Relationship,
                    basic.EmergencyContact.Phone, basic.EmergencyContact.AlternatePhone));
                else emergency.SetDetails(basic.EmergencyContact.Name, basic.EmergencyContact.Relationship, basic.EmergencyContact.Phone, basic.EmergencyContact.AlternatePhone);
                return new(null, effective, null, data);
            case EmployeeChangeSection.Identity:
                var identity = data.Identity!;
                employee.UpdateApprovedIdentity(new(identity.FullLegalName, identity.FatherOrSpouseName, identity.IdentityDocumentType,
                    identity.IdentityDocumentNumber, identity.IdentityDocumentExpiresOn, identity.DateOfBirth, identity.Gender,
                    identity.PersonalPhone, identity.CompanyPhone, identity.OfficialEmail));
                return new(null, effective, null, data);
            case EmployeeChangeSection.Employment:
                var previous = await repository.AssignmentAsync(employee.Id, null, ct) ?? throw EmployeeOperation.Conflict("employee_assignment_missing");
                if (previous.EffectiveTo.HasValue) throw EmployeeOperation.Conflict("employee_assignment_already_ended");
                var employment = data.Employment!;
                var assignment = new EmployeeAssignment(employee.Id, employment.DepartmentId, employment.DesignationId, previous.StaffCategoryId,
                    previous.EmploymentTypeId, employment.BranchId, previous.SiteId, employment.ReportingManagerEmployeeId, effective, null,
                    actor.Identity.AccountId.ToString(), actor.Now);
                previous.EndOn(effective);
                repository.AddAssignment(assignment);
                return new(assignment.Id, effective, null, data);
            case EmployeeChangeSection.Bank:
                var bank = data.Bank!;
                var priorBank = await repository.BankAsync(employee.Id, null, ct);
                var name = await ProtectAsync(bank.BankName!.Trim(), employee.Id, "approved/bank/name", ct);
                var title = await ProtectAsync(bank.AccountTitle!.Trim(), employee.Id, "approved/bank/title", ct);
                var account = await ProtectAsync(bank.AccountNumber!.Trim(), employee.Id, "approved/bank/account", ct);
                var iban = string.IsNullOrWhiteSpace(bank.Iban) ? null : await ProtectAsync(bank.Iban.Trim(), employee.Id, "approved/bank/iban", ct);
                if (name.KeyReference != title.KeyReference || name.KeyReference != account.KeyReference || (iban is not null && name.KeyReference != iban.KeyReference))
                    throw EmployeeOperation.Conflict("employee_bank_key_reference_mismatch");
                var number = bank.AccountNumber.Trim();
                var record = new EmployeeBankDetails(employee.Id, 1, JsonSerializer.Serialize(name), JsonSerializer.Serialize(title),
                    JsonSerializer.Serialize(account), iban is null ? null : JsonSerializer.Serialize(iban), name.KeyReference,
                    number.Length > 4 ? "****" + number[^4..] : "****", false, null, null, effective, null,
                    actor.Identity.AccountId.ToString(), actor.Now, priorBank is null ? 1 : checked(priorBank.Revision + 1), priorBank?.Id);
                repository.AddBank(record);
                return new(record.Id, effective, null, data, false);
            case EmployeeChangeSection.Medical:
                var reference = await ProtectAsync(data.Medical!, employee.Id, "approved/medical", ct);
                var medical = new EmployeeMedicalRecord(employee.Id, JsonSerializer.Serialize(reference), reference.KeyReference,
                    effective, data.Medical!.SupportingDocumentId, actor.Identity.AccountId.ToString(), actor.Now);
                repository.AddMedical(medical);
                return new(medical.Id, effective, null, data);
            case EmployeeChangeSection.Documents: return new(null, effective, null, data);
            default: throw EmployeeOperation.Invalid("invalid_employee_change_section");
        }
    }

    public async Task<T> ReadAsync<T>(string encoded, Guid employeeId, string section, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(encoded) ?? throw EmployeeOperation.Conflict("employee_change_evidence_unavailable");
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(reference, EmployeeHiringService.Purpose(employeeId, section), ct))
                ?? throw EmployeeOperation.Conflict("employee_change_evidence_unavailable");
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("employee_change_evidence_unavailable"); }
    }

    public async Task<string> WriteAsync<T>(T data, Guid employeeId, string section, CancellationToken ct) =>
        JsonSerializer.Serialize(await ProtectAsync(data, employeeId, section, ct));

    private async Task<PrivateFileReference> ProtectAsync<T>(T data, Guid employeeId, string section, CancellationToken ct)
    {
        var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(data), EmployeeHiringService.Purpose(employeeId, section), ct);
        if (JsonSerializer.Serialize(reference).Length > 500) throw EmployeeOperation.Conflict("employee_change_evidence_reference_invalid");
        return reference;
    }

    private async Task<Guid[]> DocumentsAsync(Employee employee, CancellationToken ct)
    {
        var change = await repository.LastChangeAsync(employee.Id, EmployeeChangeSection.Documents, ct);
        if (change is not null)
            return (await ReadAsync<EmployeeUpdateValue>(change.AfterPrivateReference, employee.Id, "changes/history", ct)).Data.Documents?.DocumentIds
                ?? throw EmployeeOperation.Conflict("employee_document_manifest_unavailable");
        return await HiringDocumentsAsync(employee, ct);
    }

    private async Task<Guid[]> HiringDocumentsAsync(Employee employee, CancellationToken ct)
    {
        var submission = await repository.ApprovedSubmissionAsync(employee, ct) ?? throw EmployeeOperation.Conflict("employee_approved_submission_missing");
        var snapshot = await ReadAsync<EmployeeHiringService.HiringSnapshot>(submission.SnapshotStorageKey, employee.Id, "submission", ct);
        if (snapshot.EmployeeId != employee.Id || snapshot.DraftRevision != submission.DraftRevision || submission.SnapshotSchemaVersion != 1)
            throw EmployeeOperation.Conflict("employee_approved_submission_invalid");
        return snapshot.Profile.DocumentIds;
    }

    private async Task<EmployeeDocument> DocumentAsync(Guid employeeId, Guid id, EmployeeDocumentPrivacy privacy, DateOnly date, CancellationToken ct)
    {
        var document = await hiring.FindDocumentAsync(employeeId, id, ct);
        if (document is null || document.Privacy != privacy || !document.IsAvailableOn(date))
            throw EmployeeOperation.Conflict("employee_change_document_unavailable");
        return document;
    }

    private static AddressInput Address(EmployeeAddress? address) => new() { Line1 = address?.AddressLine1, Line2 = address?.AddressLine2,
        City = address?.City, CityId = address?.CityId, Province = address?.Province, PostalCode = address?.PostalCode };
}
