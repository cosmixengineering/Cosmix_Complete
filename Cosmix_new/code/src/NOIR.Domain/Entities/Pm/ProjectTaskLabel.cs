namespace NOIR.Domain.Entities.Pm;

/// <summary>
/// Junction entity for the many-to-many relationship between ProjectTask and TaskLabel.
/// </summary>
public class ProjectTaskLabel : TenantEntity<Guid>
{
    public Guid TaskId { get; private set; }
    public Guid LabelId { get; private set; }

    // Navigation properties
    public virtual ProjectTask? Task { get; private set; }
    public virtual TaskLabel? Label { get; private set; }

    // Private constructor for EF Core
    private ProjectTaskLabel() : base() { }

    private ProjectTaskLabel(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new task-label association.
    /// </summary>
    public static ProjectTaskLabel Create(
        Guid taskId,
        Guid labelId,
        string? tenantId)
    {
        return new ProjectTaskLabel(Guid.NewGuid(), tenantId)
        {
            TaskId = taskId,
            LabelId = labelId
        };
    }
}
