using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmploymentEpisodes;

/// <summary>Unmapped immutable new-employment evidence. Construction never changes employee, account or access.</summary>
public sealed class EmployeeEmploymentEpisode : IAppendOnlyEntity
{
    private EmployeeEmploymentEpisode() { }
    public Guid Id { get; private init; }
    public Guid EmployeeId { get; private init; }
    public long EmployeeNumber { get; private init; }
    public DateOnly FirstJoinedOn { get; private init; }
    public Guid OriginalOnboardingDecisionId { get; private init; }
    public Guid PreviousEpisodeId { get; private init; }
    public int Sequence { get; private init; }
    public Guid PreviousTerminalStatusChangeId { get; private init; }
    public DateTimeOffset PreviousEndedAt { get; private init; }
    public EmploymentBanEvidence BanEvidence { get; private init; } = null!;
    public EmploymentEpisodeTerms Terms { get; private init; } = null!;
    public EmploymentEpisodeApproval Approval { get; private init; } = null!;

    // This factory validates evidence structure. Application must verify current authority,
    // policy, source versions and every consumer boundary before persistence/application.
    public static EmployeeEmploymentEpisode CreateRehire(EmploymentEpisodeIdentity identity,
        EmploymentEpisodeTerms terms, EmploymentEpisodeApproval approval, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(approval);
        var banFailure = EmploymentEpisodeEligibilityRules.BlockingCode(identity.BanEvidence, identity.EmployeeId, now);
        if (banFailure is not null) throw new InvalidOperationException(banFailure);
        Guid[] identityIds = [identity.EmployeeId, identity.OriginalOnboardingDecisionId,
            identity.PreviousEpisodeId, identity.TerminalStatusChangeId];
        if (identityIds.Contains(Guid.Empty) || identity.EmployeeNumber < 1 || identity.PreviousEpisodeSequence < 1 ||
            identity.FirstJoinedOn == default || identity.PreviousEndedAt == default ||
            identity.PreviousEndedAt.Offset != TimeSpan.Zero ||
            identity.TerminalStatus is not (EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired))
            throw new ArgumentException("Exact stable identity and ended employment evidence are required.");
        terms.Validate(identity.EmployeeId);
        if (terms.JoinedOn < identity.FirstJoinedOn || terms.EffectiveAt <= identity.PreviousEndedAt)
            throw new InvalidOperationException("New employment must follow the ended episode without overlap.");
        Guid[] approvalIds = [approval.RequestId, approval.SubmissionId, approval.DecisionId,
            approval.RequesterAccountId, approval.RequesterEmployeeId, approval.ReviewerAccountId,
            approval.ReviewerEmployeeId, approval.ReviewerRoleAssignmentId];
        if (approvalIds.Contains(Guid.Empty) || approval.DecisionId == identity.OriginalOnboardingDecisionId ||
            approval.RequesterAccountId == approval.ReviewerAccountId ||
            approval.RequesterEmployeeId == approval.ReviewerEmployeeId ||
            approval.RequesterEmployeeId == identity.EmployeeId || approval.ReviewerEmployeeId == identity.EmployeeId ||
            approval.ReviewerSystemRole is not (SystemRoleKind.Admin or SystemRoleKind.SuperAdmin) ||
            approval.SubmittedAt == default || approval.DecidedAt != now || now.Offset != TimeSpan.Zero ||
            approval.SubmittedAt.Offset != TimeSpan.Zero || approval.DecidedAt < approval.SubmittedAt ||
            approval.DecidedAt >= terms.EffectiveAt || !Text(approval.PolicyVersion, 100) ||
            !Sha256(approval.PolicyFingerprint) || !Sha256(approval.SnapshotSha256) || !Text(approval.Reason, 2000))
            throw new InvalidOperationException("New employment requires its own independent approved submission evidence.");
        return new()
        {
            Id = Guid.NewGuid(), EmployeeId = identity.EmployeeId, EmployeeNumber = identity.EmployeeNumber,
            FirstJoinedOn = identity.FirstJoinedOn, OriginalOnboardingDecisionId = identity.OriginalOnboardingDecisionId,
            PreviousEpisodeId = identity.PreviousEpisodeId, Sequence = checked(identity.PreviousEpisodeSequence + 1),
            PreviousTerminalStatusChangeId = identity.TerminalStatusChangeId, PreviousEndedAt = identity.PreviousEndedAt,
            BanEvidence = identity.BanEvidence with { Sources = Array.AsReadOnly(identity.BanEvidence.Sources.ToArray()) },
            Terms = terms, Approval = approval
        };
    }

    public static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    public static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() && value.Length <= maximum && !value.Any(char.IsControl);
}
