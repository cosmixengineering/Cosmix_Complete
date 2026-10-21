using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

/// <summary>Initial approved records only; the caller owns authorization, the transaction and SaveChanges.</summary>
public interface IEmployeeApprovalRepository
{
    void AddDecision(EmployeeOnboardingDecision decision);
    /// <summary>Latest 100 HR decisions for this employee, newest first. Not the complete lifetime history.</summary>
    Task<IReadOnlyList<EmployeeOnboardingDecision>> DecisionsAsync(Guid employeeId, CancellationToken ct);
    Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken ct);
    Task ValidateInitialSiteAsync(Guid? siteId, DateOnly effectiveFrom, CancellationToken ct);
    /// <summary>Resolve actual historical receivers after profile validation, without requiring current access eligibility.</summary>
    Task ValidateAssetReturnReceiversAsync(IReadOnlyList<AssetProposalInput> assets, CancellationToken ct);
    Task<bool> HasInitialRecordsAsync(Guid employeeId, CancellationToken ct);
    void AddInitialRecords(EmployeeApprovalRecords records);
}

public sealed record EmployeeApprovalRecords(
    EmployeeAssignment Assignment,
    IReadOnlyList<EmployeeAddress> Addresses,
    EmployeeEmergencyContact EmergencyContact,
    IReadOnlyList<PreviousEmployment> PreviousEmployment,
    IReadOnlyList<EmployeeCertification> Certifications,
    IReadOnlyList<EmployeeAssetCustody> Assets,
    EmployeeMedicalRecord? Medical,
    CompensationRecord? Compensation,
    EmployeeBankDetails? Bank);
