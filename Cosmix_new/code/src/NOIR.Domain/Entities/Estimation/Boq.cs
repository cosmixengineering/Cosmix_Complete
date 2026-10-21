namespace NOIR.Domain.Entities.Estimation;

/// <summary>
/// Bill of Quantities aggregate root.
/// Tracks estimate header, line items, markup, revisions, and approval workflow.
/// </summary>
public class Boq : TenantAggregateRoot<Guid>
{
    private readonly List<BoqItem> _items = [];
    private readonly List<BoqRevision> _revisions = [];

    private Boq() : base() { }

    private Boq(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>Optional customer reference (no navigation to avoid coupling).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Optional project reference (no navigation to avoid coupling).</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>Unique BOQ number per tenant (e.g., "BOQ-20260101-0001").</summary>
    public string BoqNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public BoqStatus Status { get; private set; }

    public string Currency { get; private set; } = "USD";

    /// <summary>Sum of all line item amounts, before markup.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Markup percentage applied on top of the subtotal.</summary>
    public decimal MarkupPercent { get; private set; }

    /// <summary>Computed markup amount: Subtotal * MarkupPercent / 100.</summary>
    public decimal MarkupAmount { get; private set; }

    /// <summary>Final total: Subtotal + MarkupAmount.</summary>
    public decimal TotalAmount { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<BoqItem> Items => _items;

    public IReadOnlyCollection<BoqRevision> Revisions => _revisions;

    /// <summary>
    /// Creates a new BOQ in Draft status.
    /// </summary>
    public static Boq Create(
        string boqNumber,
        string title,
        Guid? customerId,
        Guid? projectId,
        string currency,
        string? notes,
        string? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boqNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Boq(Guid.NewGuid(), tenantId)
        {
            BoqNumber = boqNumber.Trim(),
            Title = title.Trim(),
            CustomerId = customerId,
            ProjectId = projectId,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim(),
            Notes = notes?.Trim(),
            Status = BoqStatus.Draft,
            Subtotal = 0,
            MarkupPercent = 0,
            MarkupAmount = 0,
            TotalAmount = 0
        };
    }

    /// <summary>
    /// Updates the BOQ header fields (title, description, references, markup, notes).
    /// </summary>
    public void UpdateHeader(
        string title,
        string? description,
        Guid? customerId,
        Guid? projectId,
        decimal markupPercent,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Description = description?.Trim();
        CustomerId = customerId;
        ProjectId = projectId;
        MarkupPercent = markupPercent;
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Adds a line item to the BOQ.
    /// </summary>
    public void AddItem(BoqItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);
    }

    /// <summary>
    /// Removes a line item from the BOQ.
    /// </summary>
    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"BOQ item '{itemId}' was not found.");

        _items.Remove(item);
    }

    /// <summary>
    /// Recalculates subtotal, markup amount, and total from the current line items.
    /// </summary>
    public void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.Amount);
        MarkupAmount = Math.Round(Subtotal * MarkupPercent / 100, 2);
        TotalAmount = Subtotal + MarkupAmount;
    }

    /// <summary>
    /// Submits the BOQ for review. Only Draft BOQs can be submitted.
    /// </summary>
    public void SubmitForReview()
    {
        if (Status != BoqStatus.Draft)
            throw new InvalidOperationException($"Cannot submit a BOQ in {Status} status for review.");

        Status = BoqStatus.UnderReview;
    }

    /// <summary>
    /// Approves the BOQ. Only BOQs under review can be approved.
    /// </summary>
    public void Approve()
    {
        if (Status != BoqStatus.UnderReview)
            throw new InvalidOperationException($"Cannot approve a BOQ in {Status} status.");

        Status = BoqStatus.Approved;
    }

    /// <summary>
    /// Rejects the BOQ with an optional reason. Only BOQs under review can be rejected.
    /// </summary>
    public void Reject(string? reason)
    {
        if (Status != BoqStatus.UnderReview)
            throw new InvalidOperationException($"Cannot reject a BOQ in {Status} status.");

        Status = BoqStatus.Rejected;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrEmpty(Notes)
                ? reason.Trim()
                : $"{Notes}\n---\n{reason.Trim()}";
        }
    }

    /// <summary>
    /// Attaches a revision snapshot to the BOQ.
    /// </summary>
    public void AddRevision(BoqRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        _revisions.Add(revision);
    }
}
