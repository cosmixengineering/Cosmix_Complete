using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.VehicleFuel;

public sealed class VehicleCustody
{
    private VehicleCustody() { }

    public VehicleCustody(Guid vehicleId, Guid employeeId, DateTimeOffset issuedAt, decimal issueOdometerKm, string issueConditionPrivateReference, string recordedByUserId)
    {
        Id = Guid.NewGuid();
        VehicleId = ModelGuard.Id(vehicleId, nameof(vehicleId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        IssuedAt = ModelGuard.Utc(issuedAt);
        IssueOdometerKm = HrModelGuard.Distance(issueOdometerKm, nameof(issueOdometerKm));
        IssueConditionPrivateReference = ModelGuard.Required(issueConditionPrivateReference, 500, nameof(issueConditionPrivateReference));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public decimal IssueOdometerKm { get; private set; }
    public string IssueConditionPrivateReference { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public Vehicle Vehicle { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public DateTimeOffset? ReturnedAt { get; private set; }
    public decimal? ReturnOdometerKm { get; private set; }
    public string? ReturnConditionPrivateReference { get; private set; }
    public string? ReturnedToUserId { get; private set; }

    public void RecordReturn(DateTimeOffset returnedAt, decimal odometerKm, string conditionReference, string receiverUserId)
    {
        if (ReturnedAt.HasValue || returnedAt < IssuedAt || odometerKm < IssueOdometerKm) throw new InvalidOperationException("Vehicle return must follow issue with a nondecreasing odometer.");
        var distance = HrModelGuard.Distance(odometerKm, nameof(odometerKm));
        var condition = ModelGuard.Required(conditionReference, 500, nameof(conditionReference));
        var receiver = ModelGuard.Required(receiverUserId, 200, nameof(receiverUserId));
        ReturnedAt = ModelGuard.Utc(returnedAt); ReturnOdometerKm = distance;
        ReturnConditionPrivateReference = condition; ReturnedToUserId = receiver;
    }
}
