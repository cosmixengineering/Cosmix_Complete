namespace Cosmix.Domain.HR.StatutoryContributions;

/// <summary>Pakistan statutory contribution schemes.</summary>
public enum StatutoryScheme
{
    /// <summary>Employees Old-Age Benefits Institution (national).</summary>
    EOBI = 1,
    /// <summary>Punjab Employees Social Security Institution.</summary>
    PESSI = 2,
    /// <summary>Sindh Employees Social Security Institution.</summary>
    SESSI = 3
}

public enum ContributionState
{
    Draft = 1,
    Approved = 2,
    Posted = 3
}
