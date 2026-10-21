namespace NOIR.Application.Features.Stock.Specifications;

/// <summary>
/// Loads a stock indent with items by id (tracked reads).
/// </summary>
public sealed class StockIndentByIdSpec : Specification<StockIndent>
{
    public StockIndentByIdSpec(Guid id)
    {
        Query.Where(i => i.Id == id)
            .Include(i => i.Items)
            .TagWith("GetStockIndentById");
    }
}

/// <summary>
/// Loads a stock indent with items by id for mutations (tracked).
/// </summary>
public sealed class StockIndentByIdForUpdateSpec : Specification<StockIndent>
{
    public StockIndentByIdForUpdateSpec(Guid id)
    {
        Query.Where(i => i.Id == id)
            .Include(i => i.Items)
            .AsTracking()
            .TagWith("GetStockIndentByIdForUpdate");
    }
}

/// <summary>
/// Checks whether an indent number is already used in the tenant.
/// </summary>
public sealed class StockIndentNumberExistsSpec : Specification<StockIndent>
{
    public StockIndentNumberExistsSpec(string indentNumber, Guid? excludeId = null)
    {
        Query.Where(i => i.IndentNumber == indentNumber);

        if (excludeId.HasValue)
        {
            Query.Where(i => i.Id != excludeId.Value);
        }

        Query.TagWith("CheckStockIndentNumberExists");
    }
}

/// <summary>
/// Filters and pages stock indents; usable for both list and count.
/// </summary>
public sealed class StockIndentsByFilterSpec : Specification<StockIndent>
{
    public StockIndentsByFilterSpec(string? search, IndentStatus? status, int page = 1, int pageSize = 20)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            Query.Where(i => i.IndentNumber.Contains(term)
                || (i.Department != null && i.Department.Contains(term)));
        }

        if (status.HasValue)
        {
            Query.Where(i => i.Status == status.Value);
        }

        Query.Include(i => i.Items)
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.IndentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .TagWith("GetStockIndentsFiltered");
    }
}

/// <summary>
/// Loads a stock transfer with items by id (tracked reads).
/// </summary>
public sealed class StockTransferByIdSpec : Specification<StockTransfer>
{
    public StockTransferByIdSpec(Guid id)
    {
        Query.Where(t => t.Id == id)
            .Include(t => t.Items)
            .TagWith("GetStockTransferById");
    }
}

/// <summary>
/// Loads a stock transfer with items by id for mutations (tracked).
/// </summary>
public sealed class StockTransferByIdForUpdateSpec : Specification<StockTransfer>
{
    public StockTransferByIdForUpdateSpec(Guid id)
    {
        Query.Where(t => t.Id == id)
            .Include(t => t.Items)
            .AsTracking()
            .TagWith("GetStockTransferByIdForUpdate");
    }
}

/// <summary>
/// Checks whether a transfer number is already used in the tenant.
/// </summary>
public sealed class StockTransferNumberExistsSpec : Specification<StockTransfer>
{
    public StockTransferNumberExistsSpec(string transferNumber, Guid? excludeId = null)
    {
        Query.Where(t => t.TransferNumber == transferNumber);

        if (excludeId.HasValue)
        {
            Query.Where(t => t.Id != excludeId.Value);
        }

        Query.TagWith("CheckStockTransferNumberExists");
    }
}

/// <summary>
/// Filters and pages stock transfers; usable for both list and count.
/// </summary>
public sealed class StockTransfersByFilterSpec : Specification<StockTransfer>
{
    public StockTransfersByFilterSpec(
        string? search,
        TransferStatus? status,
        string? fromLocation = null,
        string? toLocation = null,
        int page = 1,
        int pageSize = 20)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            Query.Where(t => t.TransferNumber.Contains(term)
                || t.FromLocation.Contains(term)
                || t.ToLocation.Contains(term));
        }

        if (status.HasValue)
        {
            Query.Where(t => t.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(fromLocation))
        {
            Query.Where(t => t.FromLocation == fromLocation);
        }

        if (!string.IsNullOrWhiteSpace(toLocation))
        {
            Query.Where(t => t.ToLocation == toLocation);
        }

        Query.Include(t => t.Items)
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.TransferNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .TagWith("GetStockTransfersFiltered");
    }
}
