using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Leaves;

namespace Cosmix.Application.HR.EmployeeSelfService;

public static class EmployeeSelfServicePermissions
{
    public const string ProfileRead = "ess.profile.read";
    public const string AttendanceRead = "ess.attendance.read";
    public const string LeavesRead = "ess.leaves.read";
    public const string FinancesRead = "ess.finances.read";
    public const string RequestsRead = "ess.requests.read";
    public const string ResignationSelf = "ess.resignation.self";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(ProfileRead, "Read own employee profile, assignment, documents and manager", false, false, true),
        new(AttendanceRead, "Read own monthly attendance calendar and history", false, false, true),
        new(LeavesRead, "Read own leave balances and request history", false, false, true),
        new(FinancesRead, "Read own salary, advances, allowances, fuel and deductions ledger", false, false, true),
        new(RequestsRead, "Read own submitted requests and approval status summary", false, false, true),
        new(ResignationSelf, "Submit, read and cancel own pending resignation requests without changing employment status", false, false, true)
    ];
}

// ------------------------------------------------------------------------------------------------
// Profile
// ------------------------------------------------------------------------------------------------

public sealed record EssProfileResponse(
    Guid EmployeeId,
    long EmployeeNumber,
    string FullLegalName,
    string? PreferredName,
    string? FatherOrSpouseName,
    string? IdentityDocumentType,
    string? IdentityDocumentNumber,
    DateOnly? IdentityDocumentExpiresOn,
    DateOnly? DateOfBirth,
    string? Gender,
    string? MaritalStatus,
    string? PersonalPhone,
    string? CompanyPhone,
    string? PersonalEmail,
    string? OfficialEmail,
    DateOnly? JoinedOn,
    DateOnly? ProbationEndsOn,
    string Status,
    bool IsArchived,
    EssWorkAssignmentInfo? ActiveAssignment,
    IReadOnlyList<EssDocumentSummary> Documents,
    IReadOnlyList<EssAssetSummary> AssignedAssets,
    IReadOnlyList<EssSimSummary> AssignedSims
)
{
    public DateOnly AssignmentAsOf { get; init; }
    public string AssignmentStatus { get; init; } = "unavailable";
}

public sealed record EssWorkAssignmentInfo(
    Guid AssignmentId,
    string DepartmentName,
    string DesignationTitle,
    string StaffCategoryName,
    string EmploymentTypeName,
    string? BranchName,
    string? SiteName,
    Guid? ReportingManagerId,
    string? ReportingManagerName,
    long? ReportingManagerEmployeeNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo
);

public sealed record EssDocumentSummary(
    Guid DocumentId,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    string ScanStatus,
    string VerificationStatus,
    bool IsVerified
);

public sealed record EssAssetSummary(
    Guid Id,
    string AssetReference,
    DateTimeOffset IssuedAt,
    string IssueCondition,
    DateTimeOffset? ReturnedAt,
    string? ReturnCondition,
    bool IsActive
);

public sealed record EssSimSummary(
    Guid Id,
    string PhoneNumber,
    string SimReference,
    string AllowedCreditTierCode,
    string Status,
    DateOnly IssuedOn,
    DateOnly? ReturnedOn,
    bool IsActive
);

// ------------------------------------------------------------------------------------------------
// Attendance Calendar
// ------------------------------------------------------------------------------------------------

public sealed record EssAttendanceCalendarResponse(
    int Year,
    int Month,
    string Status,
    int TotalCalendarDays,
    int? PresentDays,
    int? AbsentDays,
    int? LeaveDays,
    int? HolidayDays,
    int? RestDays,
    int? LateDays,
    int TotalWorkedMinutes,
    int? TotalOvertimeMinutes,
    IReadOnlyList<AttendanceDayResponse> Days
);

// ------------------------------------------------------------------------------------------------
// Leaves
// ------------------------------------------------------------------------------------------------

public sealed record EssLeaveOverviewResponse(
    DateOnly AsOf,
    string Status,
    IReadOnlyList<EssLeaveBalanceSummary> Balances,
    IReadOnlyList<LeaveResponse> RecentRequests,
    IReadOnlyList<LeaveResponse> UpcomingApprovedLeaves
);

public sealed record EssLeaveBalanceSummary(
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    string Status,
    LeaveSelfBalanceResponse? Balance,
    IReadOnlyList<LeavePolicyResponse> ApplicablePolicies,
    IReadOnlyList<string> Reasons
);

public sealed record EssLeaveRequestSelection(IReadOnlyList<Guid> Recent, IReadOnlyList<Guid> Upcoming);

// ------------------------------------------------------------------------------------------------
// Financial Overview
// ------------------------------------------------------------------------------------------------

public sealed record EssFinancialOverviewResponse(
    EssCompensationSummary? CurrentCompensation,
    IReadOnlyList<EssAdvanceLoanAgreementSummary> ActiveAgreements,
    IReadOnlyList<EssRepaymentHistoryItem> RecentRepayments,
    IReadOnlyList<EssAllowanceClaimSummary> RecentAllowanceClaims,
    IReadOnlyList<EssFuelClaimSummary> RecentFuelClaims,
    EssVehicleInfo? AssignedVehicle,
    IReadOnlyList<EssPayrollHandoffSummary> RecentPayrollSummaries
);

public sealed record EssCompensationSummary(
    decimal BaseAmount,
    decimal GrossAmount,
    string Currency,
    string WageBasis,
    string PaymentMethod,
    DateOnly EffectiveFrom
);

public sealed record EssAdvanceLoanAgreementSummary(
    Guid AgreementId,
    string AgreementNumber,
    string Kind,
    decimal PrincipalAmount,
    string Currency,
    int TotalInstallments,
    decimal MonthlyInstallment,
    decimal RepaidAmount,
    decimal RemainingBalance,
    string Status,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateOnly? NextDueDate
);

public sealed record EssRepaymentHistoryItem(
    Guid Id,
    Guid AgreementId,
    string AgreementNumber,
    decimal Amount,
    string Currency,
    string Source,
    string EntryType,
    DateOnly EntryDate
);

public sealed record EssAllowanceClaimSummary(
    Guid ClaimId,
    string ClaimNumber,
    string Kind,
    decimal Amount,
    string Currency,
    DateOnly DutyDate,
    decimal ShiftHours,
    string Status,
    DateTimeOffset SubmittedAtUtc
);

public sealed record EssFuelClaimSummary(
    Guid ClaimId,
    string ClaimNumber,
    DateOnly ClaimDate,
    decimal OdometerReading,
    decimal Liters,
    decimal Amount,
    string Status,
    DateTimeOffset SubmittedAtUtc
);

public sealed record EssVehicleInfo(
    Guid VehicleId,
    string RegistrationNumber,
    string VehicleType,
    string MakeModel,
    string? AssetReference,
    decimal IssueOdometerKm,
    DateTimeOffset IssuedAt
);

public sealed record EssPayrollHandoffSummary(
    Guid BatchId,
    string BatchReference,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPayable,
    string Currency,
    DateTimeOffset PreparedAt
);

// ------------------------------------------------------------------------------------------------
// Requests Status Summary
// ------------------------------------------------------------------------------------------------

public sealed record EssRequestsSummaryResponse(
    int PendingLeaveRequestsCount,
    int PendingAdvanceLoanRequestsCount,
    int PendingAllowanceClaimsCount,
    int PendingFuelClaimsCount,
    int PendingAttendanceCorrectionRequestsCount,
    int TotalPendingRequestsCount
);
