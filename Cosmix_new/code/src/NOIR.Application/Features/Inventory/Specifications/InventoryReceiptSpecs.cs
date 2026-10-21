namespace NOIR.Application.Features.Inventory.Specifications;

/// <summary>
/// Specification to get an inventory receipt by ID with items loaded.
/// </summary>
public sealed class InventoryReceiptByIdSpec : Specification<InventoryReceipt>
{
    public InventoryReceiptByIdSpec(Guid receiptId)
    {
        Query.Where(r => r.Id == receiptId)
            .Include(r => r.Items)
            .TagWith("InventoryReceiptById");
    }
}

/// <summary>
/// Specification to get an inventory receipt by ID for update (with tracking).
/// </summary>
public sealed class InventoryReceiptByIdForUpdateSpec : Specification<InventoryReceipt>
{
    public InventoryReceiptByIdForUpdateSpec(Guid receiptId)
    {
        Query.Where(r => r.Id == receiptId)
            .Include(r => r.Items)
            .AsTracking()
            .TagWith("InventoryReceiptByIdForUpdate");
    }
}

/// <summary>
/// Specification to get inventory receipts with pagination.
/// </summary>
public sealed class InventoryReceiptsListSpec : Specification<InventoryReceipt>
{
    public InventoryReceiptsListSpec(
        int skip = 0,
        int take = 20,
        InventoryReceiptType? type = null,
        InventoryReceiptStatus? status = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        Query.TagWith("InventoryReceiptsList");

        if (type.HasValue)
            Query.Where(r => r.Type == type.Value);

        if (status.HasValue)
            Query.Where(r => r.Status == status.Value);

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "receiptnumber":
                if (isDescending) Query.OrderByDescending(r => r.ReceiptNumber);
                else Query.OrderBy(r => r.ReceiptNumber);
                break;
            case "type":
                if (isDescending) Query.OrderByDescending(r => r.Type);
                else Query.OrderBy(r => r.Type);
                break;
            case "status":
                if (isDescending) Query.OrderByDescending(r => r.Status);
                else Query.OrderBy(r => r.Status);
                break;
            case "items":
            case "itemcount":
                if (isDescending) Query.OrderByDescending(r => r.Items.Count);
                else Query.OrderBy(r => r.Items.Count);
                break;
            case "totalquantity":
                if (isDescending) Query.OrderByDescending(r => r.Items.Sum(i => i.Quantity));
                else Query.OrderBy(r => r.Items.Sum(i => i.Quantity));
                break;
            case "totalcost":
                if (isDescending) Query.OrderByDescending(r => r.Items.Sum(i => i.Quantity * i.UnitCost));
                else Query.OrderBy(r => r.Items.Sum(i => i.Quantity * i.UnitCost));
                break;
            case "date":
            case "createdat":
                if (isDescending) Query.OrderByDescending(r => r.CreatedAt);
                else Query.OrderBy(r => r.CreatedAt);
                break;
            case "createdby":
            case "creator":
                if (isDescending) Query.OrderByDescending(r => r.CreatedBy);
                else Query.OrderBy(r => r.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending) Query.OrderByDescending(r => r.ModifiedBy);
                else Query.OrderBy(r => r.ModifiedBy);
                break;
            default:
                Query.OrderByDescending(r => r.CreatedAt);
                break;
        }

        Query.Include(r => r.Items)
            .Skip(skip)
            .Take(take);
    }
}

/// <summary>
/// Specification to count inventory receipts matching criteria.
/// </summary>
public sealed class InventoryReceiptsCountSpec : Specification<InventoryReceipt>
{
    public InventoryReceiptsCountSpec(
        InventoryReceiptType? type = null,
        InventoryReceiptStatus? status = null)
    {
        Query.TagWith("InventoryReceiptsCount");

        if (type.HasValue)
            Query.Where(r => r.Type == type.Value);

        if (status.HasValue)
            Query.Where(r => r.Status == status.Value);
    }
}

/// <summary>
/// Specification to get the latest receipt number for today (for sequence generation).
/// </summary>
public sealed class LatestReceiptNumberTodaySpec : Specification<InventoryReceipt>
{
    public LatestReceiptNumberTodaySpec(string prefix)
    {
        Query.Where(r => r.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReceiptNumber)
            .TagWith("LatestReceiptNumberToday");
    }
}
