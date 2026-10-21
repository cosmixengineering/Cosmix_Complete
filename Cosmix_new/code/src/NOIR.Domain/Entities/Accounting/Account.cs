namespace NOIR.Domain.Entities.Accounting;

/// <summary>
/// General ledger account in the chart of accounts.
/// Accounts form an optional hierarchy via ParentId and are classified by AccountType.
/// </summary>
public class Account : TenantAggregateRoot<Guid>
{
    // Identity
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }

    // Hierarchy
    public Guid? ParentId { get; private set; }

    // State
    public bool IsActive { get; private set; } = true;
    public bool IsSystem { get; private set; }
    public string? Description { get; private set; }

    // Private constructor for EF Core
    private Account() : base() { }

    private Account(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new chart of accounts entry.
    /// </summary>
    public static Account Create(
        string code,
        string name,
        AccountType type,
        Guid? parentId,
        bool isSystem,
        string? description,
        string? tenantId = null)
    {
        return new Account(Guid.NewGuid(), tenantId)
        {
            Code = code,
            Name = name,
            Type = type,
            ParentId = parentId,
            IsActive = true,
            IsSystem = isSystem,
            Description = description
        };
    }

    /// <summary>
    /// Updates the account's core attributes. Type and system flag are immutable.
    /// </summary>
    public void Update(string code, string name, Guid? parentId, string? description)
    {
        Code = code;
        Name = name;
        ParentId = parentId;
        Description = description;
    }

    /// <summary>
    /// Activates the account so it can be used in journal lines.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the account to prevent new journal postings against it.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
