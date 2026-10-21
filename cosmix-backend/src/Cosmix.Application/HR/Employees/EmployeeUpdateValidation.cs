using Cosmix.Domain.HR.Employees;
using Cosmix.Application.Authentication.Providers;

namespace Cosmix.Application.HR.Employees;

internal static class EmployeeUpdateValidation
{
    internal static void Section(EmployeeChangeSection section, bool request = false)
    {
        if (!Enum.IsDefined(section) || (request && section == EmployeeChangeSection.Basic))
            throw EmployeeOperation.Invalid("invalid_employee_change_section");
    }

    internal static void Data(EmployeeChangeSection section, EmployeeUpdateData? data, DateOnly today)
    {
        Section(section);
        if (data is null || new object?[] { data.Basic, data.Identity, data.Employment, data.Bank, data.Medical, data.Documents }.Count(x => x is not null) != 1)
            throw EmployeeOperation.Invalid("employee_change_requires_one_section");
        switch (section)
        {
            case EmployeeChangeSection.Basic when data.Basic is { } basic:
                var profile = new EmployeeProfileInput { CurrentAddress = basic.CurrentAddress, PermanentAddress = basic.PermanentAddress,
                    EmergencyContact = basic.EmergencyContact, PersonalEmail = basic.PersonalEmail, MaritalStatus = basic.MaritalStatus };
                EmployeeFormValidation.ValidateProfile(profile, today);
                if (EmployeeFormValidation.RequiredFields(profile).Any(x => x.StartsWith("currentAddress", StringComparison.Ordinal) ||
                    x.StartsWith("permanentAddress", StringComparison.Ordinal) || x.StartsWith("emergencyContact", StringComparison.Ordinal)))
                    throw EmployeeOperation.Invalid("employee_basic_details_incomplete");
                break;
            case EmployeeChangeSection.Identity when data.Identity is { } identity:
                EmployeeFormValidation.ValidateProfile(new EmployeeProfileInput { FullLegalName = identity.FullLegalName,
                    FatherOrSpouseName = identity.FatherOrSpouseName, IdentityDocumentType = identity.IdentityDocumentType,
                    IdentityDocumentNumber = identity.IdentityDocumentNumber, IdentityDocumentExpiresOn = identity.IdentityDocumentExpiresOn,
                    DateOfBirth = identity.DateOfBirth, Gender = identity.Gender, PersonalPhone = identity.PersonalPhone,
                    CompanyPhone = identity.CompanyPhone, OfficialEmail = identity.OfficialEmail }, today);
                if (string.IsNullOrWhiteSpace(identity.FullLegalName) || string.IsNullOrWhiteSpace(identity.IdentityDocumentNumber) ||
                    string.IsNullOrWhiteSpace(identity.PersonalPhone) || identity.IdentityDocumentId == Guid.Empty || identity.IdentityDocumentExpiresOn < today)
                    throw EmployeeOperation.Invalid("employee_identity_details_incomplete");
                break;
            case EmployeeChangeSection.Employment when data.Employment is { } employment:
                if (employment.DepartmentId == Guid.Empty || employment.DesignationId == Guid.Empty ||
                    !employment.ReportingManagerEmployeeId.HasValue || employment.ReportingManagerEmployeeId == Guid.Empty || employment.BranchId == Guid.Empty)
                    throw EmployeeOperation.Invalid("invalid_employee_transfer_reference");
                break;
            case EmployeeChangeSection.Bank when data.Bank is { } bank:
                EmployeeFormValidation.ValidateCompleteSections(new(), new EmployeeFinancialInput { Bank = bank }, null);
                break;
            case EmployeeChangeSection.Medical when data.Medical is { } medical:
                EmployeeFormValidation.ValidateMedical(medical);
                if (string.IsNullOrWhiteSpace(medical.Details) || string.IsNullOrWhiteSpace(medical.AuthorizationReference))
                    throw EmployeeOperation.Invalid("employee_medical_authorization_required");
                break;
            case EmployeeChangeSection.Documents when data.Documents is { } documents:
                if (documents.DocumentIds is null || documents.DocumentIds.Length is < 1 or > 100 ||
                    documents.DocumentIds.Any(id => id == Guid.Empty) || documents.DocumentIds.Distinct().Count() != documents.DocumentIds.Length)
                    throw EmployeeOperation.Invalid("invalid_employee_document_manifest");
                break;
            default: throw EmployeeOperation.Invalid("employee_change_section_mismatch");
        }
    }

    internal static void Reason(string? reason, bool required = true, int maximum = 1000)
    {
        if ((required && string.IsNullOrWhiteSpace(reason)) || reason?.Length > maximum || reason?.Any(char.IsControl) == true)
            throw EmployeeOperation.Invalid("invalid_employee_change_reason");
    }

    // Match persisted domain values so approval/history never describes untrimmed values that were not saved.
    internal static EmployeeUpdateData Normalize(EmployeeUpdateData data, IPhoneNumberNormalizer phones)
    {
        if (data.Basic is { } basic)
            return data with { Basic = basic with { CurrentAddress = NormalizeAddress(basic.CurrentAddress),
                PermanentAddress = NormalizeAddress(basic.PermanentAddress), PersonalEmail = Optional(basic.PersonalEmail),
                MaritalStatus = Optional(basic.MaritalStatus), EmergencyContact = new() { Name = Optional(basic.EmergencyContact.Name),
                    Relationship = Optional(basic.EmergencyContact.Relationship), Phone = EmployeeFormValidation.NormalizePhone(basic.EmergencyContact.Phone, phones),
                    AlternatePhone = EmployeeFormValidation.NormalizePhone(basic.EmergencyContact.AlternatePhone, phones) } } };
        if (data.Identity is { } identity)
            return data with { Identity = identity with { FullLegalName = identity.FullLegalName.Trim(),
                FatherOrSpouseName = Optional(identity.FatherOrSpouseName), Gender = Optional(identity.Gender),
                IdentityDocumentNumber = string.Concat(identity.IdentityDocumentNumber.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant(),
                PersonalPhone = EmployeeFormValidation.NormalizePhone(identity.PersonalPhone, phones)!,
                CompanyPhone = EmployeeFormValidation.NormalizePhone(identity.CompanyPhone, phones), OfficialEmail = Optional(identity.OfficialEmail) } };
        if (data.Bank is { } bank)
            return data with { Bank = new() { BankName = Optional(bank.BankName), AccountTitle = Optional(bank.AccountTitle),
                AccountNumber = Optional(bank.AccountNumber), Iban = Optional(bank.Iban) } };
        return data;
    }

    private static AddressInput NormalizeAddress(AddressInput address) => new() { Line1 = Optional(address.Line1),
        Line2 = Optional(address.Line2), City = Optional(address.City), CityId = address.CityId,
        Province = Optional(address.Province), PostalCode = Optional(address.PostalCode) };
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void Page(int page, int size)
    {
        if (page is < 1 or > 1000000 || size is < 1 or > 100) throw EmployeeOperation.Invalid("invalid_employee_change_pagination");
    }

    internal static bool Dated(EmployeeChangeSection section) => section is EmployeeChangeSection.Employment or EmployeeChangeSection.Bank or EmployeeChangeSection.Medical;

    internal static string[] ReadPermissions(EmployeeChangeSection section)
    {
        Section(section);
        return section switch
        {
            EmployeeChangeSection.Identity => [EmployeePermissions.Read, EmployeeUpdatePermissions.IdentityRead],
            EmployeeChangeSection.Bank => [EmployeePermissions.Read, EmployeeUpdatePermissions.BankRead],
            EmployeeChangeSection.Medical => [EmployeePermissions.Read, EmployeePermissions.MedicalRead],
            EmployeeChangeSection.Documents => [EmployeePermissions.Read, EmployeePermissions.DocumentsRead],
            _ => [EmployeePermissions.Read]
        };
    }

    internal static string[] RequestPermissions(EmployeeChangeSection section) => ReadPermissions(section).Concat(section switch
    {
        EmployeeChangeSection.Identity => new[] { EmployeeUpdatePermissions.Request, EmployeeUpdatePermissions.IdentityRequest, EmployeePermissions.DocumentsRead },
        EmployeeChangeSection.Bank => [EmployeeUpdatePermissions.Request, EmployeeUpdatePermissions.BankRequest],
        EmployeeChangeSection.Medical => [EmployeeUpdatePermissions.Request, EmployeePermissions.MedicalEdit, EmployeePermissions.DocumentsRead],
        EmployeeChangeSection.Documents => [EmployeeUpdatePermissions.Request, EmployeePermissions.DocumentsManage],
        _ => [EmployeeUpdatePermissions.Request]
    }).Distinct().ToArray();
}
