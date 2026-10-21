using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeUpdatePermissions
{
    public const string BasicEdit = "hr.employees.basic.manage";
    public const string Request = "hr.employees.changes.request";
    public const string Review = "hr.employees.changes.review";
    public const string IdentityRead = "hr.employees.identity.read";
    public const string IdentityRequest = "hr.employees.identity.request";
    public const string BankRead = "hr.employees.bank.read";
    public const string BankRequest = "hr.employees.bank.request";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeBasicInput(AddressInput CurrentAddress, AddressInput PermanentAddress,
    EmergencyContactInput EmergencyContact, string? PersonalEmail, string? MaritalStatus);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeIdentityInput(string FullLegalName, string? FatherOrSpouseName,
    IdentityDocumentType IdentityDocumentType, string IdentityDocumentNumber, DateOnly? IdentityDocumentExpiresOn,
    DateOnly DateOfBirth, string? Gender, string PersonalPhone, string? CompanyPhone, string? OfficialEmail,
    Guid IdentityDocumentId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeEmploymentInput(Guid DepartmentId, Guid DesignationId,
    Guid? ReportingManagerEmployeeId, Guid? BranchId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeDocumentSelection(Guid[] DocumentIds);

// Exactly one section is accepted. Credentials, salary, staff category and site cannot be mass-assigned here.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeUpdateData(EmployeeBasicInput? Basic = null, EmployeeIdentityInput? Identity = null,
    EmployeeEmploymentInput? Employment = null, BankInput? Bank = null,
    EmployeeMedicalInput? Medical = null, EmployeeDocumentSelection? Documents = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeBasic(string ExpectedVersion, EmployeeBasicInput Data, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestEmployeeChange(EmployeeChangeSection Section, string ExpectedVersion,
    EmployeeUpdateData Data, DateOnly? EffectiveFrom, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideEmployeeChange(string ExpectedEmployeeVersion, string ExpectedRequestVersion,
    ApprovalState Decision, string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelEmployeeChange(string ExpectedRequestVersion);
public sealed record EmployeeChangeQuery(int Page = 1, int PageSize = 25, ApprovalState? State = null);
public sealed record EmployeeChangePageQuery(int Page = 1, int PageSize = 25);
public sealed record EmployeeUpdateValue(Guid? RecordId, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    EmployeeUpdateData Data, bool? BankVerified = null);
public sealed record EmployeeCurrentUpdate(Guid EmployeeId, string Version, EmployeeChangeSection Section,
    DateOnly AsOf, EmployeeUpdateValue Current, EmployeeUpdateValue? Scheduled);
public sealed record EmployeeChangeSummary(Guid Id, Guid EmployeeId, EmployeeChangeSection Section,
    DateOnly EffectiveFrom, Guid? ApprovalRequestId, string ActorId, DateTimeOffset RecordedAt);
public sealed record EmployeeChangeHistoryDetail(EmployeeChangeSummary Change, EmployeeUpdateValue Before,
    EmployeeUpdateValue After, string Reason);
public sealed record EmployeeChangeRequestSummary(Guid Id, Guid EmployeeId, ApprovalState State,
    string RequesterId, DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt, string Version);
public sealed record EmployeeChangeDecision(Guid Id, ApprovalState Decision, string ActorId,
    string RoleCode, string? Reason, DateTimeOffset DecidedAt);
public sealed record EmployeeChangeRequestDetail(EmployeeChangeRequestSummary Request, EmployeeChangeSection Section,
    EmployeeUpdateValue Before, EmployeeUpdateData Proposed, DateOnly? EffectiveFrom, string Reason,
    IReadOnlyList<EmployeeChangeDecision> Decisions);
public sealed record EmployeeChangeSnapshot(int SchemaVersion, Guid EmployeeId, EmployeeChangeSection Section,
    EmployeeUpdateValue Before, EmployeeUpdateData Proposed, DateOnly? EffectiveFrom, string Reason,
    string? FoundationFieldPolicyVersion = null);

public interface IEmployeeUpdateRepository
{
    Task<IReadOnlyList<EmployeeAddress>> AddressesAsync(Guid employeeId, CancellationToken ct);
    Task<EmployeeEmergencyContact?> EmergencyAsync(Guid employeeId, CancellationToken ct);
    Task<EmployeeAssignment?> AssignmentAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct);
    Task<EmployeeBankDetails?> BankAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct);
    Task<EmployeeMedicalRecord?> MedicalAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct);
    Task<EmployeeSubmission?> ApprovedSubmissionAsync(Employee employee, CancellationToken ct);
    Task ValidateBasicAsync(EmployeeBasicInput data, CancellationToken ct);
    Task ValidateIdentityUniqueAsync(Guid employeeId, EmployeeIdentityInput data, CancellationToken ct);
    Task ValidateEmploymentAsync(Guid employeeId, EmployeeEmploymentInput data, EmployeeAssignment previous,
        DateOnly effectiveFrom, DateTimeOffset now, CancellationToken ct);
    Task<bool> HasConflictingHistoryAsync(Guid employeeId, EmployeeChangeSection section, CancellationToken ct);
    Task<EmployeeChange?> LastChangeAsync(Guid employeeId, EmployeeChangeSection section, CancellationToken ct);
    Task<EmployeeChange?> FindChangeAsync(Guid employeeId, EmployeeChangeSection section, Guid changeId, CancellationToken ct);
    Task<PagedResult<EmployeeChangeSummary>> HistoryAsync(Guid employeeId, EmployeeChangeSection section, EmployeeChangePageQuery query, CancellationToken ct);
    Task<bool> HasPendingAsync(Guid employeeId, string requestType, CancellationToken ct);
    Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, string requestType, Guid requestId, CancellationToken ct);
    Task<PagedResult<EmployeeChangeRequestSummary>> RequestsAsync(Guid? employeeId, string requestType, EmployeeChangeQuery query, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    void AddAddress(EmployeeAddress address);
    void AddEmergency(EmployeeEmergencyContact contact);
    void AddAssignment(EmployeeAssignment assignment);
    void AddBank(EmployeeBankDetails bank);
    void AddMedical(EmployeeMedicalRecord medical);
    void AddChange(EmployeeChange change);
    void AddRequest(ApprovalRequest request);
    void AddDecision(ApprovalDecision decision);
}
