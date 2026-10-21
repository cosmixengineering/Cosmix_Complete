namespace NOIR.Domain.Entities.Complaints;

public enum ComplaintPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ComplaintStatus
{
    New = 1,
    UnderReview = 2,
    Assigned = 3,
    Resolved = 4,
    Closed = 5
}
