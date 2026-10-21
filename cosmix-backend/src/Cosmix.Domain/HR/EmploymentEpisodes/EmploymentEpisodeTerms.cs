using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmploymentEpisodes;

// Preparation types are intentionally not mapped or referenced by existing EF entities.
public sealed record EmploymentEpisodeIdentity(Guid EmployeeId, long EmployeeNumber, DateOnly FirstJoinedOn,
    Guid OriginalOnboardingDecisionId, Guid PreviousEpisodeId, int PreviousEpisodeSequence,
    Guid TerminalStatusChangeId, EmployeeStatus TerminalStatus, DateTimeOffset PreviousEndedAt,
    EmploymentBanEvidence BanEvidence);

public sealed record EmploymentEpisodeTerms(DateOnly JoinedOn, DateTimeOffset EffectiveAt, DateOnly? ProbationEndsOn,
    Guid DepartmentId, Guid DesignationId, Guid StaffCategoryId, Guid EmploymentTypeId, Guid? BranchId,
    Guid? InitialSiteId, Guid ReportingManagerEmployeeId, decimal BaseAmount, decimal GrossAmount,
    string Currency, WageBasis WageBasis, PaymentMethod PaymentMethod, string? ApprovedLoginPhoneNumber,
    bool MobileAccessRequested, bool WebAccessRequested)
{
    public void Validate(Guid employeeId)
    {
        if (employeeId == Guid.Empty || JoinedOn == default || EffectiveAt == default ||
            EffectiveAt.Offset != TimeSpan.Zero || ProbationEndsOn < JoinedOn)
            throw new ArgumentException("Valid explicit UTC joining and probation boundaries are required.");
        Guid[] required = [DepartmentId, DesignationId, StaffCategoryId, EmploymentTypeId, ReportingManagerEmployeeId];
        if (required.Contains(Guid.Empty) || BranchId == Guid.Empty || InitialSiteId == Guid.Empty ||
            ReportingManagerEmployeeId == employeeId || BranchId.HasValue && InitialSiteId.HasValue)
            throw new ArgumentException("New employment requires distinct, valid assignment references.");
        const decimal maximum = 9999999999999999.99m;
        if (BaseAmount < 0 || GrossAmount < BaseAmount || GrossAmount > maximum ||
            decimal.Round(BaseAmount, 2) != BaseAmount || decimal.Round(GrossAmount, 2) != GrossAmount ||
            Currency is not { Length: 3 } || Currency.Any(c => c is < 'A' or > 'Z') ||
            !Enum.IsDefined(WageBasis) || !Enum.IsDefined(PaymentMethod))
            throw new ArgumentException("New compensation requires explicit supported two-decimal terms.");
        if ((MobileAccessRequested || WebAccessRequested) && ApprovedLoginPhoneNumber is null)
            throw new ArgumentException("Requested channels require a separately approved login phone.");
        if (ApprovedLoginPhoneNumber is not null)
            _ = LoginPhoneNumber.RequireCanonical(ApprovedLoginPhoneNumber, nameof(ApprovedLoginPhoneNumber));
    }
}

public sealed record EmploymentEpisodeApproval(Guid RequestId, Guid SubmissionId, Guid DecisionId,
    Guid RequesterAccountId, Guid RequesterEmployeeId, Guid ReviewerAccountId, Guid ReviewerEmployeeId,
    Guid ReviewerRoleAssignmentId, SystemRoleKind ReviewerSystemRole, DateTimeOffset SubmittedAt,
    DateTimeOffset DecidedAt, string PolicyVersion, string PolicyFingerprint, string SnapshotSha256, string Reason);
