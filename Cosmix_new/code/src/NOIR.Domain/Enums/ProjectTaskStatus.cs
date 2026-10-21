namespace NOIR.Domain.Enums;

/// <summary>
/// Status of a project task.
/// </summary>
public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3,
    Cancelled = 4
}
