namespace NOIR.Domain.Entities.Accounting;

/// <summary>
/// Classification of a general ledger account per the accounting equation.
/// </summary>
public enum AccountType
{
    /// <summary>Resources owned by the business (e.g., cash, inventory).</summary>
    Asset = 0,

    /// <summary>Obligations owed to external parties (e.g., loans, payables).</summary>
    Liability = 1,

    /// <summary>Owner's residual interest (e.g., retained earnings, capital).</summary>
    Equity = 2,

    /// <summary>Income earned from operations (e.g., sales, service revenue).</summary>
    Revenue = 3,

    /// <summary>Costs incurred to generate revenue (e.g., rent, salaries).</summary>
    Expense = 4
}

/// <summary>
/// Lifecycle status of a journal entry.
/// Draft entries are editable; Posted entries are locked and affect balances;
/// Void entries are reversed markers that never delete financial history.
/// </summary>
public enum JournalEntryStatus
{
    /// <summary>Entry is being prepared and can still be modified.</summary>
    Draft = 0,

    /// <summary>Entry is finalized and reflected in account balances.</summary>
    Posted = 1,

    /// <summary>Posted entry was voided; retained for audit trail.</summary>
    Void = 2
}
