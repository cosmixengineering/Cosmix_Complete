using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeFormValidation
{
    public static void ValidateProfile(EmployeeProfileInput input, DateOnly today)
    {
        ValidateAnnotations(input, "invalid_employee_profile");
        ValidateEnum(input.IdentityDocumentType, "invalid_employee_identity_document");
        ValidateIdentity(input.IdentityDocumentType, input.IdentityDocumentNumber);
        ValidatePhone(input.PersonalPhone);
        ValidatePhone(input.CompanyPhone);
        ValidatePhone(input.LoginPhoneNumber);
        ValidateEmail(input.PersonalEmail);
        ValidateEmail(input.OfficialEmail);
        ValidateDate(input.DateOfBirth);
        ValidateDate(input.IdentityDocumentExpiresOn);
        ValidateDate(input.SimIssuedOn);
        if (input.DateOfBirth >= today) throw Invalid("invalid_employee_date_of_birth");
        ValidateDates(input.JoinedOn, input.ProbationEndsOn);

        ValidateId(input.DepartmentId);
        ValidateId(input.DesignationId);
        ValidateId(input.StaffCategoryId);
        ValidateId(input.EmploymentTypeId);
        ValidateId(input.BranchId);
        ValidateId(input.ReportingManagerEmployeeId);
        ValidateId(input.ProposedShiftId);
        ValidateAddress(input.CurrentAddress);
        ValidateAddress(input.PermanentAddress);
        if (input.EmergencyContact is { } contact)
        {
            ValidateAnnotations(contact, "invalid_employee_emergency_contact");
            ValidatePhone(contact.Phone);
            ValidatePhone(contact.AlternatePhone);
        }

        var documentIds = new HashSet<Guid>();
        foreach (var id in input.DocumentIds)
        {
            ValidateId(id);
            if (!documentIds.Add(id)) throw Invalid("invalid_employee_document_manifest");
        }

        var employmentIds = new HashSet<Guid>();
        foreach (var employment in input.PreviousEmployment)
        {
            ValidateAnnotations(employment, "invalid_employee_previous_employment");
            ValidateId(employment.EntryId);
            if (!employmentIds.Add(employment.EntryId)) throw Invalid("invalid_employee_previous_employment");
            ValidateDates(employment.StartedOn, employment.EndedOn);
        }
        foreach (var certification in input.Certifications)
        {
            ValidateAnnotations(certification, "invalid_employee_certification");
            ValidateId(certification.DocumentId);
            ValidateDates(certification.IssuedOn, certification.ExpiresOn);
        }
        foreach (var asset in input.Assets)
        {
            ValidateAnnotations(asset, "invalid_employee_asset_proposal");
            ValidateId(asset.EvidenceDocumentId);
            ValidateId(asset.ReturnedToAccountId);
            ValidateDates(asset.IssuedOn, asset.ReturnedOn);
            if (asset.ReturnedOn.HasValue)
            {
                if (!asset.IssuedOn.HasValue || !asset.ReturnedToAccountId.HasValue || string.IsNullOrWhiteSpace(asset.ReturnCondition))
                    throw Invalid("employee_asset_return_incomplete");
            }
            else if (asset.ReturnedToAccountId.HasValue || !string.IsNullOrWhiteSpace(asset.ReturnCondition))
                throw Invalid("employee_asset_return_date_required");
        }
        // Returned proposals do not participate in the active-custody SQL index.
        // Validate their half-open intervals within this same submission as well.
        for (var left = 0; left < input.Assets.Length; left++)
        for (var right = left + 1; right < input.Assets.Length; right++)
        {
            var a = input.Assets[left]; var b = input.Assets[right];
            if (string.IsNullOrWhiteSpace(a.AssetReference) || string.IsNullOrWhiteSpace(b.AssetReference) ||
                !a.IssuedOn.HasValue || !b.IssuedOn.HasValue ||
                !string.Equals(a.AssetReference.Trim(), b.AssetReference.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            if (a.ReturnedOn <= a.IssuedOn || b.ReturnedOn <= b.IssuedOn) continue;
            if ((!b.ReturnedOn.HasValue || a.IssuedOn < b.ReturnedOn) &&
                (!a.ReturnedOn.HasValue || b.IssuedOn < a.ReturnedOn))
                throw Invalid("employee_asset_proposal_overlap");
        }
    }

    public static void ValidateFinancial(EmployeeFinancialInput input)
    {
        ValidateAnnotations(input, "invalid_employee_financial");
        ValidateAmount(input.BaseSalary);
        ValidateAmount(input.GrossSalary);
        if (input.GrossSalary < input.BaseSalary) throw Invalid("invalid_employee_salary_range");
        ValidateCurrency(input.Currency);
        ValidateEnum(input.WageBasis, "invalid_employee_wage_basis");
        ValidateEnum(input.PaymentMethod, "invalid_employee_payment_method");
        ValidateDate(input.EffectiveFrom);
        if (input.Bank is { } bank) ValidateAnnotations(bank, "invalid_employee_bank");

        var employmentIds = new HashSet<Guid>();
        foreach (var salary in input.PreviousSalaries)
        {
            ValidateAnnotations(salary, "invalid_employee_previous_salary");
            ValidateId(salary.EmploymentEntryId);
            if (!employmentIds.Add(salary.EmploymentEntryId)) throw Invalid("invalid_employee_previous_salary");
            ValidateAmount(salary.Amount);
            ValidateCurrency(salary.Currency);
        }
    }

    public static void ValidateMedical(EmployeeMedicalInput input)
    {
        ValidateAnnotations(input, "invalid_employee_medical");
        ValidateId(input.SupportingDocumentId);
    }

    /// <summary>Complete supplied financial/medical sections before submission or approval; drafts may remain partial.</summary>
    public static void ValidateCompleteSections(EmployeeProfileInput profile, EmployeeFinancialInput? financial,
        EmployeeMedicalInput? medical)
    {
        ValidateAnnotations(profile, "invalid_employee_profile");
        if (medical is not null) ValidateMedical(medical);
        if (financial is null) return;
        ValidateFinancial(financial);
        if (HasCompensation(financial) && (!financial.BaseSalary.HasValue || !financial.GrossSalary.HasValue ||
            string.IsNullOrWhiteSpace(financial.Currency) || !financial.WageBasis.HasValue ||
            !financial.PaymentMethod.HasValue || !financial.EffectiveFrom.HasValue))
            throw Invalid("employee_compensation_incomplete");
        if (financial.Bank is { } bank && (string.IsNullOrWhiteSpace(bank.BankName) ||
            string.IsNullOrWhiteSpace(bank.AccountTitle) || string.IsNullOrWhiteSpace(bank.AccountNumber)))
            throw Invalid("employee_bank_details_incomplete");
        foreach (var salary in financial.PreviousSalaries)
        {
            if (!salary.Amount.HasValue || string.IsNullOrWhiteSpace(salary.Currency))
                throw Invalid("employee_previous_salary_incomplete");
            if (!profile.PreviousEmployment.Any(p => p is not null && p.EntryId == salary.EmploymentEntryId))
                throw Invalid("invalid_previous_salary_reference");
        }
    }

    internal static bool HasCompensation(EmployeeFinancialInput financial) => financial.BaseSalary.HasValue ||
        financial.GrossSalary.HasValue || financial.Currency is not null || financial.WageBasis.HasValue ||
        financial.PaymentMethod.HasValue || financial.EffectiveFrom.HasValue;

    /// <summary>HR submission completeness only; reference, branch and document checks belong to the service.</summary>
    public static IReadOnlyList<string> RequiredFields(EmployeeProfileInput input)
    {
        if (input is null) throw Invalid("invalid_employee_profile");
        var missing = new List<string>();
        RequireText(input.FullLegalName, "fullLegalName", missing);
        if (!input.IdentityDocumentType.HasValue) missing.Add("identityDocumentType");
        RequireText(input.IdentityDocumentNumber, "identityDocumentNumber", missing);
        if (!input.DateOfBirth.HasValue) missing.Add("dateOfBirth");
        RequireText(input.PersonalPhone, "personalPhone", missing);
        RequireAddress(input.CurrentAddress, "currentAddress", missing);
        RequireAddress(input.PermanentAddress, "permanentAddress", missing);
        RequireText(input.EmergencyContact?.Name, "emergencyContact.name", missing);
        RequireText(input.EmergencyContact?.Relationship, "emergencyContact.relationship", missing);
        RequireText(input.EmergencyContact?.Phone, "emergencyContact.phone", missing);
        RequireId(input.DepartmentId, "departmentId", missing);
        RequireId(input.DesignationId, "designationId", missing);
        RequireId(input.EmploymentTypeId, "employmentTypeId", missing);
        RequireId(input.StaffCategoryId, "staffCategoryId", missing);
        RequireId(input.ReportingManagerEmployeeId, "reportingManagerEmployeeId", missing);
        if (!input.JoinedOn.HasValue) missing.Add("joinedOn");
        return missing;
    }

    /// <summary>Maps an already validated profile; related draft rows are owned by the hiring service.</summary>
    public static EmployeeDraftDetails ToDomain(EmployeeProfileInput input)
    {
        if (input is null) throw Invalid("invalid_employee_profile");
        return new EmployeeDraftDetails
        {
            FullLegalName = Optional(input.FullLegalName),
            FatherOrSpouseName = Optional(input.FatherOrSpouseName),
            IdentityDocumentType = input.IdentityDocumentType,
            IdentityDocumentNumber = NormalizeIdentity(input.IdentityDocumentNumber),
            IdentityDocumentExpiresOn = input.IdentityDocumentExpiresOn,
            DateOfBirth = input.DateOfBirth,
            Gender = Optional(input.Gender),
            MaritalStatus = Optional(input.MaritalStatus),
            PersonalPhone = NormalizePhoneShape(input.PersonalPhone),
            CompanyPhone = NormalizePhoneShape(input.CompanyPhone),
            SimIssuedOn = input.SimIssuedOn,
            SimStatus = Optional(input.SimStatus),
            SimCreditTier = Optional(input.SimCreditTier),
            PersonalEmail = Optional(input.PersonalEmail),
            OfficialEmail = Optional(input.OfficialEmail),
            JoinedOn = input.JoinedOn,
            ProbationEndsOn = input.ProbationEndsOn
        };
    }

    private static void ValidateAnnotations(object? input, string code)
    {
        if (input is null || !Validator.TryValidateObject(input, new ValidationContext(input), [], true))
            throw Invalid(code);
    }

    private static void ValidateAddress(AddressInput? input)
    {
        if (input is null) return;
        ValidateAnnotations(input, "invalid_employee_address");
        ValidateId(input.CityId);
    }

    private static void ValidateId(Guid? value)
    {
        if (value == Guid.Empty) throw Invalid("invalid_employee_reference");
    }

    private static void ValidateEnum<T>(T? value, string code) where T : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value)) throw Invalid(code);
    }

    private static void ValidatePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _ = NormalizePhoneShape(value);
    }

    private static string? NormalizePhoneShape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 64 || value.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')' or '.')))
            throw Invalid("invalid_employee_phone");
        var number = string.Concat(value.Where(c => c is not (' ' or '-' or '(' or ')' or '.')));
        if (number.Length is < 3 or > 16 || number[0] != '+' || number[1] is < '1' or > '9' ||
            number.AsSpan(2).ContainsAnyExceptInRange('0', '9')) throw Invalid("invalid_employee_phone");
        return number;
    }

    public static EmployeeProfileInput NormalizePhones(EmployeeProfileInput input, IPhoneNumberNormalizer normalizer) => input with
    {
        PersonalPhone = NormalizePhone(input.PersonalPhone, normalizer),
        CompanyPhone = NormalizePhone(input.CompanyPhone, normalizer),
        LoginPhoneNumber = NormalizePhone(input.LoginPhoneNumber, normalizer),
        EmergencyContact = input.EmergencyContact is { } contact ? contact with
        {
            Phone = NormalizePhone(contact.Phone, normalizer), AlternatePhone = NormalizePhone(contact.AlternatePhone, normalizer)
        } : null
    };

    internal static string? NormalizePhone(string? value, IPhoneNumberNormalizer normalizer)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return normalizer.Normalize(value); }
        catch (ArgumentException) { throw Invalid("invalid_employee_phone"); }
    }

    private static void ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Any(char.IsWhiteSpace) || !new EmailAddressAttribute().IsValid(value) ||
            !MailAddress.TryCreate(value, out var address) || address.Address != value)
            throw Invalid("invalid_employee_email");
    }

    private static void ValidateIdentity(IdentityDocumentType? type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        // Accept presentation spaces/hyphens without imposing a country's passport series or length.
        if (value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not (' ' or '-')))
            throw Invalid("invalid_employee_identity_document");
        var number = NormalizeIdentity(value)!;
        if (number.Length == 0 || (type == IdentityDocumentType.Cnic &&
            (number.Length != 13 || number.AsSpan().ContainsAnyExceptInRange('0', '9'))))
            throw Invalid("invalid_employee_identity_document");
    }

    private static string? NormalizeIdentity(string? value) => Optional(value) is { } number
        ? number.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant()
        : null;

    private static void ValidateDates(DateOnly? start, DateOnly? end)
    {
        ValidateDate(start);
        ValidateDate(end);
        if (end < start) throw Invalid("invalid_employee_date_range");
    }

    private static void ValidateDate(DateOnly? value)
    {
        // Missing draft dates are null, not the CLR's default date sentinel.
        if (value == DateOnly.MinValue) throw Invalid("invalid_employee_date");
    }

    private static void ValidateAmount(decimal? amount)
    {
        // Match the existing SQL decimal(18,2), rejecting rounding and overflow before persistence.
        if (amount is { } value && (value < 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value))
            throw Invalid("invalid_employee_salary");
    }

    private static void ValidateCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return;
        if (currency.Length != 3 || currency.AsSpan().ContainsAnyExceptInRange('A', 'Z'))
            throw Invalid("invalid_employee_currency");
    }

    private static void RequireAddress(AddressInput? address, string prefix, List<string> missing)
    {
        RequireText(address?.Line1, prefix + ".line1", missing);
        if (!address?.CityId.HasValue ?? true)
            RequireText(address?.City, prefix + ".city", missing);
        else if (address!.CityId == Guid.Empty && string.IsNullOrWhiteSpace(address.City))
            missing.Add(prefix + ".city");
        RequireText(address?.Province, prefix + ".province", missing);
    }

    private static void RequireText(string? value, string path, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value)) missing.Add(path);
    }

    private static void RequireId(Guid? value, string path, List<string> missing)
    {
        if (!value.HasValue || value == Guid.Empty) missing.Add(path);
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static AuthenticationFailure Invalid(string code) => new(code, 400);
}
