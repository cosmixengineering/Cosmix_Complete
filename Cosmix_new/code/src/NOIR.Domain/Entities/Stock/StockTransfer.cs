namespace NOIR.Domain.Entities.Stock;

/// <summary>
/// A movement of stock from one location to another, with an approval workflow.
/// </summary>
public class StockTransfer : TenantAggregateRoot<Guid>
{
    private readonly List<StockTransferItem> _items = [];

    private StockTransfer()
    {
    }

    private StockTransfer(Guid id, string? tenantId)
        : base(id, tenantId)
    {
    }

    public string TransferNumber { get; private set; } = string.Empty;

    public string FromLocation { get; private set; } = string.Empty;

    public string ToLocation { get; private set; } = string.Empty;

    public DateOnly? TransferDate { get; private set; }

    public string? Notes { get; private set; }

    public TransferStatus Status { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? RejectReason { get; private set; }

    public IReadOnlyCollection<StockTransferItem> Items => _items;

    public static StockTransfer Create(
        string transferNumber,
        string fromLocation,
        string toLocation,
        DateOnly? transferDate,
        string? notes,
        string? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transferNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(toLocation);

        return new StockTransfer(Guid.NewGuid(), tenantId)
        {
            TransferNumber = transferNumber.Trim(),
            FromLocation = fromLocation.Trim(),
            ToLocation = toLocation.Trim(),
            TransferDate = transferDate,
            Notes = notes?.Trim(),
            Status = TransferStatus.Draft
        };
    }

    public void AddItem(StockTransferItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != TransferStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot add items to a transfer in {Status} status.");
        }

        _items.Add(item);
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status != TransferStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot remove items from a transfer in {Status} status.");
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Transfer item with ID '{itemId}' was not found.");

        _items.Remove(item);
    }

    public void UpdateHeader(string? fromLocation, string? toLocation, DateOnly? transferDate, string? notes)
    {
        if (Status != TransferStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update a transfer in {Status} status.");
        }

        if (!string.IsNullOrWhiteSpace(fromLocation))
        {
            FromLocation = fromLocation.Trim();
        }

        if (!string.IsNullOrWhiteSpace(toLocation))
        {
            ToLocation = toLocation.Trim();
        }

        TransferDate = transferDate;
        Notes = notes?.Trim();
    }

    public void Submit()
    {
        if (Status != TransferStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot submit transfer in {Status} status.");
        }

        Status = TransferStatus.Submitted;
    }

    public void Approve(Guid? approvedByUserId)
    {
        if (Status != TransferStatus.Draft && Status != TransferStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot approve transfer in {Status} status.");
        }

        Status = TransferStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTimeOffset.UtcNow;
        RejectReason = null;
    }

    public void Reject(string? reason)
    {
        if (Status != TransferStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot reject transfer in {Status} status.");
        }

        Status = TransferStatus.Rejected;
        RejectReason = reason?.Trim();
    }

    public void Cancel()
    {
        if (Status != TransferStatus.Draft && Status != TransferStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot cancel transfer in {Status} status.");
        }

        Status = TransferStatus.Cancelled;
    }

    public void Complete()
    {
        if (Status != TransferStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot complete transfer in {Status} status.");
        }

        Status = TransferStatus.Completed;
    }
}
