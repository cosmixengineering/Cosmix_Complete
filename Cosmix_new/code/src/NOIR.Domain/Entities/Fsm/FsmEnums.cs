namespace NOIR.Domain.Entities.Fsm;

public enum JobType
{
    Installation = 1,
    Commissioning = 2,
    Maintenance = 3,
    Repair = 4,
    Inspection = 5,
    ServiceVisit = 6
}

public enum JobStatus
{
    Pending = 1,
    Assigned = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}
