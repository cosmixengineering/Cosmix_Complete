namespace Cosmix.Domain.HR.Employees;

public sealed record EmployeeBasicDetails(string? PersonalEmail, string? MaritalStatus);

public sealed record EmployeeIdentityDetails(
    string FullLegalName,
    string? FatherOrSpouseName,
    IdentityDocumentType IdentityDocumentType,
    string IdentityDocumentNumber,
    DateOnly? IdentityDocumentExpiresOn,
    DateOnly DateOfBirth,
    string? Gender,
    string PersonalPhone,
    string? CompanyPhone,
    string? OfficialEmail);
