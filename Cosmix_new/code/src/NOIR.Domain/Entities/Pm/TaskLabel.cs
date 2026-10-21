namespace NOIR.Domain.Entities.Pm;

/// <summary>
/// A label that can be attached to tasks within a project.
/// </summary>
public class TaskLabel : TenantEntity<Guid>
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#6366f1";

    // Navigation properties
    public virtual Project? Project { get; private set; }

    // Private constructor for EF Core
    private TaskLabel() : base() { }

    private TaskLabel(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new task label.
    /// </summary>
    public static TaskLabel Create(
        Guid projectId,
        string name,
        string color,
        string? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        return new TaskLabel(Guid.NewGuid(), tenantId)
        {
            ProjectId = projectId,
            Name = name.Trim(),
            Color = color
        };
    }

    /// <summary>
    /// Updates label details.
    /// </summary>
    public void Update(string name, string color)
    {
        Name = name.Trim();
        Color = color;
    }
}
