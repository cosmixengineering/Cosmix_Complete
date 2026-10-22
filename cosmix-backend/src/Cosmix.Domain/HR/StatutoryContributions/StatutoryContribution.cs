using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.StatutoryContributions;

public sealed class StatutoryContribution
{
    private StatutoryContribution() { }

    public StatutoryContribution(StatutoryScheme scheme, DateOnly contributionMonth,
        decimal employeeContributionAmount, decimal employerContributionAmount,
        string currency, string createdByUserId, DateTimeOffset createdAt,
        string? payableAccountCode = null, string? notes = null, string? employerRegistrationNumber = null)
    {
        Id = Guid.NewGuid();
        Scheme = Enum.IsDefined(scheme) ? scheme : throw new ArgumentOutOfRangeException(nameof(scheme));
        ContributionMonth = new DateOnly(contributionMonth.Year, contributionMonth.Month, 1);
        EmployeeContributionAmount = HrModelGuard.Money(employeeContributionAmount, nameof(employeeContributionAmount));
        EmployerContributionAmount = HrModelGuard.Money(employerContributionAmount, nameof(employerContributionAmount));
        if (EmployeeContributionAmount == 0 && EmployerContributionAmount == 0)
            throw new ArgumentException("At least one contribution amount must be greater than zero.");
        Currency = HrModelGuard.Currency(currency);
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
        PayableAccountCode = ModelGuard.Optional(payableAccountCode, 50, nameof(payableAccountCode));
        Notes = ModelGuard.Optional(notes, 500, nameof(notes));
        EmployerRegistrationNumber = ModelGuard.Optional(employerRegistrationNumber, 100, nameof(employerRegistrationNumber));
        State = ContributionState.Draft;
    }

    public Guid Id { get; private set; }
    public StatutoryScheme Scheme { get; private set; }
    public string? EmployerRegistrationNumber { get; private set; }
    public DateOnly ContributionMonth { get; private set; }
    public decimal EmployeeContributionAmount { get; private set; }
    public decimal EmployerContributionAmount { get; private set; }
    public decimal TotalContributionAmount => EmployeeContributionAmount + EmployerContributionAmount;
    public string Currency { get; private set; } = string.Empty;
    public ContributionState State { get; private set; }
    public string? PayableAccountCode { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public string? PostedByUserId { get; private set; }

    public void Approve(string actorUserId, DateTimeOffset at)
    {
        if (State != ContributionState.Draft)
            throw new InvalidOperationException("Only draft contributions can be approved.");
        if (string.Equals(actorUserId, CreatedByUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The creator cannot approve their own statutory contribution (separation of duties).");
        ApprovedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ApprovedAt = ModelGuard.Utc(at);
        State = ContributionState.Approved;
    }

    public void Post(string actorUserId, DateTimeOffset at)
    {
        if (State != ContributionState.Approved)
            throw new InvalidOperationException("Only approved contributions can be posted to accounting.");
        if (string.IsNullOrWhiteSpace(PayableAccountCode))
            throw new InvalidOperationException("Payable account code is required before posting to accounting.");
        PostedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        PostedAt = ModelGuard.Utc(at);
        State = ContributionState.Posted;
    }
}
