using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.StatutoryContributions;

public sealed class StatutoryContribution
{
    private StatutoryContribution() { }

    public StatutoryContribution(StatutoryScheme scheme, DateOnly contributionMonth,
        decimal employeeContributionAmount, decimal employerContributionAmount,
        string currency, string createdByUserId, DateTimeOffset createdAt,
        string? payableAccountCode = null, string? notes = null, string? employerRegistrationNumber = null,
        string? employeeItemsJson = null, bool hasEmployeeEntries = false)
    {
        Id = Guid.NewGuid();
        Scheme = Enum.IsDefined(scheme) ? scheme : throw new ArgumentOutOfRangeException(nameof(scheme));
        ContributionMonth = new DateOnly(contributionMonth.Year, contributionMonth.Month, 1);
        EmployeeContributionAmount = HrModelGuard.Money(employeeContributionAmount, nameof(employeeContributionAmount));
        EmployerContributionAmount = HrModelGuard.Money(employerContributionAmount, nameof(employerContributionAmount));
        HasEmployeeEntries = hasEmployeeEntries || !string.IsNullOrWhiteSpace(employeeItemsJson);
        if (!HasEmployeeEntries && EmployeeContributionAmount == 0 && EmployerContributionAmount == 0)
            throw new ArgumentException("At least one contribution amount must be greater than zero.");
        Currency = HrModelGuard.Currency(currency);
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
        PayableAccountCode = ModelGuard.Optional(payableAccountCode, 50, nameof(payableAccountCode));
        Notes = ModelGuard.Optional(notes, 500, nameof(notes));
        EmployerRegistrationNumber = ModelGuard.Optional(employerRegistrationNumber, 100, nameof(employerRegistrationNumber));
        EmployeeItemsJson = employeeItemsJson;
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
    public Guid? JournalEntryId { get; private set; }
    public string? EmployeeItemsJson { get; private set; }
    public bool HasEmployeeEntries { get; private set; }
    public ICollection<StatutoryContributionEntry> Entries { get; private set; } = new List<StatutoryContributionEntry>();
    public ICollection<StatutoryEmployeePosting> EmployeePostings { get; private set; } = new List<StatutoryEmployeePosting>();
    public Guid? ObligationId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void SetEmployeeItemsJson(string json)
    {
        if (State != ContributionState.Draft) throw new InvalidOperationException("Approved contribution evidence is immutable.");
        EmployeeItemsJson = json;
        HasEmployeeEntries = !string.IsNullOrWhiteSpace(json) || Entries.Count > 0;
    }

    public void RecordPosting(Guid obligationId, Guid? journalId, string payableAccountCode, string actor, DateTimeOffset at)
    {
        if (State != ContributionState.Approved || (ObligationId.HasValue && ObligationId != obligationId))
            throw new InvalidOperationException("Only an approved contribution can bind its matching payroll obligation.");
        ValidateEntries();
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
        PayableAccountCode = ModelGuard.Required(payableAccountCode, 50, nameof(payableAccountCode));
        Post(actor, at, journalId);
    }

    public void BindObligation(Guid obligationId)
    {
        if (State != ContributionState.Draft)
            throw new InvalidOperationException("Only draft contributions can bind an obligation before approval.");
        if (ObligationId.HasValue && ObligationId.Value == obligationId)
            return;
        if (ObligationId.HasValue && ObligationId.Value != obligationId)
            throw new InvalidOperationException("Statutory contribution is already linked to a different obligation.");
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
    }

    public void CompleteGroupedPosting(string payableAccountCode, string actor, DateTimeOffset at)
    {
        if (State != ContributionState.Approved || ObligationId.HasValue)
            throw new InvalidOperationException("Only unbound approved contributions can complete across payroll groups.");
        ValidateEntries();
        PayableAccountCode = ModelGuard.Required(payableAccountCode, 50, nameof(payableAccountCode));
        // Each obligation/journal remains in immutable employee posting rows; no single group
        // is misrepresented as having posted the whole month's contribution.
        Post(actor, at);
    }

    public void Approve(string actorUserId, DateTimeOffset at)
    {
        if (State != ContributionState.Draft)
            throw new InvalidOperationException("Only draft contributions can be approved.");
        if (string.Equals(actorUserId, CreatedByUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The creator cannot approve their own statutory contribution (separation of duties).");
        ValidateEntries();
        ApprovedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        ApprovedAt = ModelGuard.Utc(at);
        State = ContributionState.Approved;
    }

    public void Post(string actorUserId, DateTimeOffset at, Guid? journalEntryId = null)
    {
        if (State == ContributionState.Posted)
            throw new InvalidOperationException("Statutory contribution has already been posted to accounting.");
        if (State != ContributionState.Approved)
            throw new InvalidOperationException("Only approved contributions can be posted to accounting.");
        if (string.IsNullOrWhiteSpace(PayableAccountCode))
            throw new InvalidOperationException("Payable account code is required before posting to accounting.");
        PostedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        PostedAt = ModelGuard.Utc(at);
        JournalEntryId = journalEntryId;
        State = ContributionState.Posted;
    }

    public void ValidateEntries()
    {
        if (!HasEmployeeEntries || Entries.Count == 0 || Entries.Any(x => x.ContributionId != Id) ||
            Entries.Select(x => x.EmployeeId).Distinct().Count() != Entries.Count ||
            Entries.Sum(x => x.EmployeeContributionAmount) != EmployeeContributionAmount ||
            Entries.Sum(x => x.EmployerContributionAmount) != EmployerContributionAmount)
            throw new InvalidOperationException("Unique actual employee entries must reconcile exactly with contribution totals.");
    }
}
