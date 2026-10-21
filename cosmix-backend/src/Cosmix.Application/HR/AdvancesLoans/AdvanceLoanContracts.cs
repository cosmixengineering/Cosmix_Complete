using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;

namespace Cosmix.Application.HR.AdvancesLoans;

public static class AdvanceLoanPermissions
{
    public const string Self = "attendance.advancesloans.self";
    public const string Request = "attendance.advancesloans.request";
    public const string Review = "attendance.advancesloans.review";
    public const string Approve = "attendance.advancesloans.approve";
    public const string Repay = "attendance.advancesloans.repay";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own advance and loan requests, repayment schedules, and ledger history", false, false, true),
        new(Request, "Submit advance and loan requests for employees within administrative scope", false, true, false),
        new(Review, "Review pending advance and loan requests within scope", true, true, false),
        new(Approve, "Approve advance/loan terms, create repayment agreements and schedule installments", true, true, false),
        new(Repay, "Record payroll deductions, received repayments, or ledger reversals", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvanceLoanSubmitRequest(
    AdvanceLoanKind Kind,
    [Range(1, 10_000_000)] decimal RequestedAmount,
    [Required, StringLength(10)] string Currency,
    [Required, StringLength(2000)] string Reason,
    DateOnly RequestedOn);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvanceLoanCancelRequest(
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvanceLoanApproveRequest(
    [Range(1, 10_000_000)] decimal ApprovedPrincipal,
    [Required, StringLength(10)] string Currency,
    DateOnly EffectiveOn,
    [Required, StringLength(500)] string ApprovedTerms,
    [Range(1, 60)] int NumberOfInstallments,
    DateOnly FirstDueDate,
    [Range(typeof(decimal), "0", "0")] decimal OtherApprovedMonthlyAmount = 0m);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvanceLoanRejectRequest(
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordRepaymentRequest(
    Guid? RepaymentScheduleEntryId,
    RepaymentEntryKind Kind,
    [Range(1, 10_000_000)] decimal Amount,
    [Required, StringLength(10)] string Currency,
    DateOnly ValueDate,
    [Required, StringLength(150)] string SourceReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReverseRepaymentRequest(
    Guid ReversesEntryId,
    DateOnly ValueDate,
    [Required, StringLength(150)] string SourceReference,
    [Required, StringLength(1000)] string Reason);

public sealed record AdvanceLoanRequestResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    AdvanceLoanKind Kind,
    decimal RequestedAmount,
    string Currency,
    string Reason,
    DateOnly RequestedOn,
    DateTimeOffset CreatedAt,
    string State,
    Guid? AgreementId,
    Guid? ApprovalRequestId);

public sealed record RepaymentScheduleEntryResponse(
    Guid Id,
    int Sequence,
    DateOnly DueDate,
    decimal PrincipalAmount,
    decimal OtherApprovedAmount,
    decimal TotalDue,
    string Currency,
    decimal PaidAmount,
    bool IsSettled);

public sealed record RepaymentLedgerEntryResponse(
    Guid Id,
    Guid? RepaymentScheduleEntryId,
    RepaymentEntryKind Kind,
    decimal Amount,
    string Currency,
    DateOnly ValueDate,
    string SourceReference,
    Guid? ReversesEntryId,
    DateTimeOffset RecordedAt);

public sealed record AdvanceLoanAgreementDetailResponse(
    Guid Id,
    Guid AdvanceLoanRequestId,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    AdvanceLoanKind Kind,
    decimal ApprovedPrincipal,
    string Currency,
    DateOnly EffectiveOn,
    string ApprovedTerms,
    decimal TotalRepaid,
    decimal OutstandingBalance,
    bool IsFullyRepaid,
    DateTimeOffset RecordedAt,
    IReadOnlyList<RepaymentScheduleEntryResponse> Schedule,
    IReadOnlyList<RepaymentLedgerEntryResponse> Ledger);

public sealed record EmployeeLoanAccountSummaryResponse(
    Guid EmployeeId,
    int TotalRequestsCount,
    int ActiveAgreementsCount,
    decimal TotalApprovedPrincipal,
    decimal TotalRepaidAmount,
    decimal CurrentOutstandingBalance,
    string Currency,
    IReadOnlyList<AdvanceLoanRequestResponse> RecentRequests,
    IReadOnlyList<AdvanceLoanAgreementDetailResponse> ActiveAgreements);

public sealed record AdvanceLoanPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore);
