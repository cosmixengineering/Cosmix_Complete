namespace NOIR.Domain.Entities.Stock;

/// <summary>
/// A purchase requisition raised by a department/user to procure products.
/// </summary>
public class StockIndent : TenantAggregateRoot<Guid>
{
    private readonly List<StockIndentItem> _items = [];

    private StockIndent()
    {
    }

    private StockIndent(Guid id, string? tenantId)
        : base(id, tenantId)
    {
    }

    public string IndentNumber { get; private set; } = string.Empty;

    public Guid? RequestedByUserId { get; private set; }

    public DateOnly? NeededByDate { get; private set; }

    public string? Department { get; private set; }

    public string? Notes { get; private set; }

    public IndentStatus Status { get; private set; }

    public string? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? RejectReason { get; private set; }

    public string? ProjectId { get; private set; }

    public IReadOnlyCollection<StockIndentItem> Items => _items;

    public static StockIndent Create(
        string indentNumber,
        Guid? requestedByUserId,
        DateOnly? neededByDate,
        string? department,
        string? notes,
        string? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indentNumber);

        return new StockIndent(Guid.NewGuid(), tenantId)
        {
            IndentNumber = indentNumber.Trim(),
            RequestedByUserId = requestedByUserId,
            NeededByDate = neededByDate,
            Department = department?.Trim(),
            Notes = notes?.Trim(),
            Status = IndentStatus.Draft
        };
    }

    public void AddItem(StockIndentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != IndentStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot add items to an indent in {Status} status.");
        }

        _items.Add(item);
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status != IndentStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot remove items from an indent in {Status} status.");
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Indent item with ID '{itemId}' was not found.");

        _items.Remove(item);
    }

    public void UpdateHeader(DateOnly? neededByDate, string? department, string? notes)
    {
        if (Status != IndentStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update an indent in {Status} status.");
        }

        NeededByDate = neededByDate;
        Department = department?.Trim();
        Notes = notes?.Trim();
    }

    public void Submit()
    {
        if (Status != IndentStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot submit indent in {Status} status.");
        }

        Status = IndentStatus.Submitted;
    }

    public void Approve(string? approvedByUserId)
    {
        if (Status != IndentStatus.Draft && Status != IndentStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot approve indent in {Status} status.");
        }

        Status = IndentStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTimeOffset.UtcNow;
        RejectReason = null;
    }

    public void Reject(string? reason)
    {
        if (Status != IndentStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot reject indent in {Status} status.");
        }

        Status = IndentStatus.Rejected;
        RejectReason = reason?.Trim();
    }

    public void Cancel()
    {
        if (Status != IndentStatus.Draft && Status != IndentStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot cancel indent in {Status} status.");
        }

        Status = IndentStatus.Cancelled;
    }
}
