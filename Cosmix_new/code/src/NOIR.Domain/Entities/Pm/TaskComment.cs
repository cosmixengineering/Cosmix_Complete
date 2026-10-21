namespace NOIR.Domain.Entities.Pm;

/// <summary>
/// A comment on a project task.
/// </summary>
public class TaskComment : TenantEntity<Guid>
{
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsEdited { get; private set; }

    // Navigation properties
    public virtual ProjectTask? Task { get; private set; }
    public virtual Employee? Author { get; private set; }

    // Private constructor for EF Core
    private TaskComment() : base() { }

    private TaskComment(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new task comment.
    /// </summary>
    public static TaskComment Create(
        Guid taskId,
        Guid authorId,
        string content,
        string? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new TaskComment(Guid.NewGuid(), tenantId)
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content.Trim(),
            IsEdited = false
        };
    }

    /// <summary>
    /// Edits the comment content.
    /// </summary>
    public void Edit(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Content = content.Trim();
        IsEdited = true;
    }
}
