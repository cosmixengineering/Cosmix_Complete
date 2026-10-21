namespace Cosmix.Domain.HR.Employees;

/// <summary>Draft input only; this is not an API DTO or activation validation contract.</summary>
public sealed record EmployeeDraftDetails
{
    public string? FullLegalName { get; init; }
    public string? FatherOrSpouseName { get; init; }
    public IdentityDocumentType? IdentityDocumentType { get; init; }
    public string? IdentityDocumentNumber { get; init; }
    public DateOnly? IdentityDocumentExpiresOn { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public string? PersonalPhone { get; init; }
    public string? CompanyPhone { get; init; }
    public DateOnly? SimIssuedOn { get; init; }
    public string? SimStatus { get; init; }
    public string? SimCreditTier { get; init; }
    public string? PersonalEmail { get; init; }
    public string? OfficialEmail { get; init; }
    public DateOnly? JoinedOn { get; init; }
    public DateOnly? ProbationEndsOn { get; init; }
}

