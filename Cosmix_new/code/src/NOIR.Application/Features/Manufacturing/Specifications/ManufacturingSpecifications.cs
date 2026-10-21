namespace NOIR.Application.Features.Manufacturing.Specifications;

/// <summary>
/// Specification to find a bill of material by ID with its items.
/// </summary>
public sealed class BillOfMaterialByIdSpec : Specification<BillOfMaterial>
{
    public BillOfMaterialByIdSpec(Guid id)
    {
        Query.Where(b => b.Id == id)
             .Include(b => b.Items)
             .TagWith("GetBillOfMaterialById");
    }
}

/// <summary>
/// Specification to find a bill of material by ID for update (with tracking).
/// </summary>
public sealed class BillOfMaterialByIdForUpdateSpec : Specification<BillOfMaterial>
{
    public BillOfMaterialByIdForUpdateSpec(Guid id)
    {
        Query.Where(b => b.Id == id)
             .Include(b => b.Items)
             .AsTracking()
             .TagWith("GetBillOfMaterialByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if a BOM code already exists (for uniqueness validation).
/// </summary>
public sealed class BillOfMaterialCodeExistsSpec : Specification<BillOfMaterial>
{
    public BillOfMaterialCodeExistsSpec(string code, Guid? excludeId = null)
    {
        Query.Where(b => b.Code == code);

        if (excludeId.HasValue)
        {
            Query.Where(b => b.Id != excludeId.Value);
        }

        Query.TagWith("CheckBillOfMaterialCodeExists");
    }
}

/// <summary>
/// Specification for paged bill of material listing with filters.
/// </summary>
public sealed class BillOfMaterialsByFilterSpec : Specification<BillOfMaterial>
{
    public BillOfMaterialsByFilterSpec(string? search, bool? isActive, int page = 1, int pageSize = 20)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(b => b.Code.Contains(search) || b.Name.Contains(search));
        }

        if (isActive.HasValue)
        {
            Query.Where(b => b.IsActive == isActive.Value);
        }

        Query.Include(b => b.Items)
             .OrderByDescending(b => b.CreatedAt)
             .ThenBy(b => b.Code)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetBillOfMaterialsPaged");
    }
}

/// <summary>
/// Specification for counting bills of material with filters (for pagination).
/// </summary>
public sealed class BillOfMaterialsCountSpec : Specification<BillOfMaterial>
{
    public BillOfMaterialsCountSpec(string? search, bool? isActive)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(b => b.Code.Contains(search) || b.Name.Contains(search));
        }

        if (isActive.HasValue)
        {
            Query.Where(b => b.IsActive == isActive.Value);
        }

        Query.TagWith("CountBillOfMaterials");
    }
}

/// <summary>
/// Specification to get all component items of a bill of material.
/// </summary>
public sealed class BillOfMaterialItemsByBomSpec : Specification<BillOfMaterialItem>
{
    public BillOfMaterialItemsByBomSpec(Guid bomId)
    {
        Query.Where(i => i.BillOfMaterialId == bomId)
             .OrderBy(i => i.ProductId)
             .TagWith("GetBillOfMaterialItemsByBom");
    }
}

/// <summary>
/// Specification to find a production order by ID with its items.
/// </summary>
public sealed class ProductionOrderByIdSpec : Specification<ProductionOrder>
{
    public ProductionOrderByIdSpec(Guid id)
    {
        Query.Where(o => o.Id == id)
             .Include(o => o.Items)
             .TagWith("GetProductionOrderById");
    }
}

/// <summary>
/// Specification to find a production order by ID for update (with tracking).
/// </summary>
public sealed class ProductionOrderByIdForUpdateSpec : Specification<ProductionOrder>
{
    public ProductionOrderByIdForUpdateSpec(Guid id)
    {
        Query.Where(o => o.Id == id)
             .Include(o => o.Items)
             .AsTracking()
             .TagWith("GetProductionOrderByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if an order number already exists (for uniqueness validation).
/// </summary>
public sealed class ProductionOrderNumberExistsSpec : Specification<ProductionOrder>
{
    public ProductionOrderNumberExistsSpec(string orderNumber, Guid? excludeId = null)
    {
        Query.Where(o => o.OrderNumber == orderNumber);

        if (excludeId.HasValue)
        {
            Query.Where(o => o.Id != excludeId.Value);
        }

        Query.TagWith("CheckProductionOrderNumberExists");
    }
}

/// <summary>
/// Specification for paged production order listing with filters.
/// </summary>
public sealed class ProductionOrdersByFilterSpec : Specification<ProductionOrder>
{
    public ProductionOrdersByFilterSpec(
        string? search,
        ProductionOrderStatus? status,
        int page = 1,
        int pageSize = 20)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(o => o.OrderNumber.Contains(search) || (o.Notes != null && o.Notes.Contains(search)));
        }

        if (status.HasValue)
        {
            Query.Where(o => o.Status == status.Value);
        }

        Query.OrderByDescending(o => o.CreatedAt)
             .ThenBy(o => o.OrderNumber)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetProductionOrdersPaged");
    }
}

/// <summary>
/// Specification for counting production orders with filters (for pagination).
/// </summary>
public sealed class ProductionOrdersCountSpec : Specification<ProductionOrder>
{
    public ProductionOrdersCountSpec(string? search, ProductionOrderStatus? status)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(o => o.OrderNumber.Contains(search) || (o.Notes != null && o.Notes.Contains(search)));
        }

        if (status.HasValue)
        {
            Query.Where(o => o.Status == status.Value);
        }

        Query.TagWith("CountProductionOrders");
    }
}

/// <summary>
/// Specification to get material requirement items of a production order.
/// </summary>
public sealed class ProductionOrderItemsByOrderSpec : Specification<ProductionOrderItem>
{
    public ProductionOrderItemsByOrderSpec(Guid orderId)
    {
        Query.Where(i => i.ProductionOrderId == orderId)
             .OrderBy(i => i.ProductId)
             .TagWith("GetProductionOrderItemsByOrder");
    }
}

/// <summary>
/// Specification to check whether production orders reference a bill of material.
/// </summary>
public sealed class ProductionOrdersByBomIdSpec : Specification<ProductionOrder>
{
    public ProductionOrdersByBomIdSpec(Guid billOfMaterialId)
    {
        Query.Where(o => o.BillOfMaterialId == billOfMaterialId)
             .TagWith("CheckProductionOrdersByBomId");
    }
}
