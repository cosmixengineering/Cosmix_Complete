using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeProfileInput
{
    [StringLength(200)] public string? FullLegalName { get; init; }
    [StringLength(200)] public string? FatherOrSpouseName { get; init; }
    public IdentityDocumentType? IdentityDocumentType { get; init; }
    [StringLength(50)] public string? IdentityDocumentNumber { get; init; }
    public DateOnly? IdentityDocumentExpiresOn { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    [StringLength(40)] public string? Gender { get; init; }
    [StringLength(40)] public string? MaritalStatus { get; init; }
    [StringLength(64)] public string? PersonalPhone { get; init; }
    [StringLength(64)] public string? CompanyPhone { get; init; }
    public DateOnly? SimIssuedOn { get; init; }
    [StringLength(40)] public string? SimStatus { get; init; }
    [StringLength(50)] public string? SimCreditTier { get; init; }
    [StringLength(320)] public string? PersonalEmail { get; init; }
    [StringLength(320)] public string? OfficialEmail { get; init; }
    public DateOnly? JoinedOn { get; init; }
    public DateOnly? ProbationEndsOn { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? DesignationId { get; init; }
    public Guid? StaffCategoryId { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? ReportingManagerEmployeeId { get; init; }
    public Guid? ProposedShiftId { get; init; }
    [StringLength(64)] public string? LoginPhoneNumber { get; init; }
    public bool MobileAccessRequested { get; init; }
    public AddressInput? CurrentAddress { get; init; }
    public AddressInput? PermanentAddress { get; init; }
    public EmergencyContactInput? EmergencyContact { get; init; }
    [Required, MaxLength(100)] public Guid[] DocumentIds { get; init; } = [];
    [Required, MaxLength(100)] public PreviousEmploymentInput[] PreviousEmployment { get; init; } = [];
    [Required, MaxLength(100)] public CertificationInput[] Certifications { get; init; } = [];
    [Required, MaxLength(100)] public AssetProposalInput[] Assets { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AddressInput
{
    [StringLength(250)] public string? Line1 { get; init; }
    [StringLength(250)] public string? Line2 { get; init; }
    [StringLength(100)] public string? City { get; init; }
    public Guid? CityId { get; init; }
    [StringLength(100)] public string? Province { get; init; }
    [StringLength(20)] public string? PostalCode { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmergencyContactInput
{
    [StringLength(200)] public string? Name { get; init; }
    [StringLength(100)] public string? Relationship { get; init; }
    [StringLength(64)] public string? Phone { get; init; }
    [StringLength(64)] public string? AlternatePhone { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreviousEmploymentInput
{
    public Guid EntryId { get; init; }
    [StringLength(200)] public string? EmployerName { get; init; }
    [StringLength(150)] public string? RoleTitle { get; init; }
    public DateOnly? StartedOn { get; init; }
    public DateOnly? EndedOn { get; init; }
    [StringLength(1000)] public string? LeavingReason { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CertificationInput
{
    public Guid? DocumentId { get; init; }
    [StringLength(100)] public string? Type { get; init; }
    [StringLength(100)] public string? Number { get; init; }
    [StringLength(200)] public string? Issuer { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AssetProposalInput
{
    [StringLength(100)] public string? AssetReference { get; init; }
    [StringLength(1000)] public string? IssueCondition { get; init; }
    [StringLength(1000)] public string? ReturnCondition { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ReturnedOn { get; init; }
    public Guid? ReturnedToAccountId { get; init; }
    public Guid? EvidenceDocumentId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeFinancialInput
{
    public decimal? BaseSalary { get; init; }
    public decimal? GrossSalary { get; init; }
    [StringLength(3)] public string? Currency { get; init; }
    public WageBasis? WageBasis { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public BankInput? Bank { get; init; }
    [Required, MaxLength(100)] public PreviousSalaryInput[] PreviousSalaries { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BankInput
{
    [StringLength(200)] public string? BankName { get; init; }
    [StringLength(200)] public string? AccountTitle { get; init; }
    [StringLength(100)] public string? AccountNumber { get; init; }
    [StringLength(34)] public string? Iban { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreviousSalaryInput
{
    public Guid EmploymentEntryId { get; init; }
    public decimal? Amount { get; init; }
    [StringLength(3)] public string? Currency { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeMedicalInput
{
    [StringLength(10000)] public string? Details { get; init; }
    [StringLength(500)] public string? AuthorizationReference { get; init; }
    public Guid? SupportingDocumentId { get; init; }
}
