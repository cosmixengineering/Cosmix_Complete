namespace Cosmix.Domain.HR.Onboarding;

public enum OnboardingStatus { Draft = 0, PendingApproval = 1, Approved = 2, Returned = 3, Rejected = 4 }
public enum OnboardingDecision { Approve = 1, ReturnForCorrection = 2, Reject = 3 }
