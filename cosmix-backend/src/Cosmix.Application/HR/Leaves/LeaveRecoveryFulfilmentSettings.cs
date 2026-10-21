using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.Leaves;

public enum RecoveryQualifyingWork { Pending = 0, OutsideScheduledDuty = 1 }
public enum RecoveryCreditBasis { Pending = 0, ExactWorkedMinutes = 1 }
public enum RecoveryReviewAuthority { Pending = 0, FixedAdmin = 1 }
public enum RecoveryParentChangeHandling { Pending = 0, BlockClaimsRetainAllocations = 1 }
public enum RecoverySourceChangeHandling { Pending = 0, RejectChangedSourcesRetainAllocations = 1 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveRecoveryFulfilmentSettings(
    [property: JsonRequired] RecoveryQualifyingWork QualifyingWork,
    [property: JsonRequired] RecoveryCreditBasis CreditBasis,
    [property: JsonRequired] RecoveryReviewAuthority ReviewAuthority,
    [property: JsonRequired] bool AllowPartialFulfilment,
    [property: JsonRequired] int MinimumClaimMinutes,
    [property: JsonRequired] int MaximumClaimMinutes,
    [property: JsonRequired] int MaximumClaimsPerCommitment,
    [property: JsonRequired] int SubmissionWindowDays,
    [property: JsonRequired] RecoveryParentChangeHandling ParentChangeHandling,
    [property: JsonRequired] RecoverySourceChangeHandling SourceChangeHandling)
{
    public void Validate()
    {
        if (!Enum.IsDefined(QualifyingWork) || !Enum.IsDefined(CreditBasis) || !Enum.IsDefined(ReviewAuthority) ||
            !Enum.IsDefined(ParentChangeHandling) || !Enum.IsDefined(SourceChangeHandling) ||
            MinimumClaimMinutes is < 1 or > 1440 || MaximumClaimMinutes < MinimumClaimMinutes || MaximumClaimMinutes > 1440 ||
            MaximumClaimsPerCommitment is < 1 or > 1000 || SubmissionWindowDays is < 0 or > 366)
            throw AttendanceOperation.Invalid("invalid_recovery_fulfilment_settings");
    }

    public void RequireConfigured()
    {
        Validate();
        if (QualifyingWork == RecoveryQualifyingWork.Pending || CreditBasis == RecoveryCreditBasis.Pending ||
            ReviewAuthority == RecoveryReviewAuthority.Pending || ParentChangeHandling == RecoveryParentChangeHandling.Pending ||
            SourceChangeHandling == RecoverySourceChangeHandling.Pending)
            throw AttendanceOperation.Conflict("leave_recovery_fulfilment_policy_pending");
    }
}
