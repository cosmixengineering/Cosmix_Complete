namespace NOIR.Domain.Enums;

/// <summary>
/// Hardcoded categories for employee tags.
/// Other modules query tags by category for integration.
/// </summary>
public enum EmployeeTagCategory
{
    Team = 0,
    Skill = 1,
    Project = 2,
    Location = 3,
    Seniority = 4,
    Employment = 5,
    Custom = 6
}
