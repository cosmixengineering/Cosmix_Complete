using Cosmix.Application.Authentication;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed record MyVehicleActivityItem(Guid Id, string Kind, string Title, string State,
    DateTimeOffset AtUtc, Guid? VehicleId, string? RegistrationNumber, decimal? Amount = null, string? Currency = null);
public sealed record MyVehicleActivityResponse(VehicleFuelPage<MyVehicleActivityItem> Trips,
    VehicleFuelPage<MyVehicleActivityItem> FuelClaims, VehicleFuelPage<MyVehicleActivityItem> Maintenance,
    VehicleFuelPage<MyVehicleActivityItem> Incidents);

public sealed partial class VehicleFuelService
{
    public Task<MyVehicleActivityResponse> MyActivityAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(VehicleFuelPermissions.Self, "vehicle.activity.self", null, context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                var skip = (page - 1) * pageSize;
                // Every query applies self identity before counting and pagination.
                var trips = await repository.PageTripsAsync(employeeId, null, null, skip, pageSize, token);
                var fuel = await repository.PageFuelClaimsAsync(employeeId, null, skip, pageSize, token);
                var service = await repository.PageServiceRecordsAsync(null, employeeId, null, skip, pageSize, token);
                var incidents = await repository.PageOwnIncidentsAsync(employeeId, skip, pageSize, token);
                VehicleFuelPage<MyVehicleActivityItem> Page(IEnumerable<MyVehicleActivityItem> items, int count) =>
                    new(items.ToArray(), page, pageSize, count, skip + pageSize < count);
                return new MyVehicleActivityResponse(
                    Page(trips.Items.Select(x => new MyVehicleActivityItem(x.Id, "Trip", $"{x.OriginLabel} → {x.DestinationLabel}",
                        x.ApprovalRequest?.State.ToString() ?? "Unavailable", x.StartedAt, x.VehicleId, x.Vehicle?.RegistrationNumber)), trips.TotalCount),
                    Page(fuel.Items.Select(x => new MyVehicleActivityItem(x.Id, "Fuel", "Fuel claim",
                        x.ApprovalRequest?.State.ToString() ?? "Unavailable", x.CreatedAt, null, null, x.ClaimedAmount, x.Currency)), fuel.TotalCount),
                    Page(service.Items.Select(x => new MyVehicleActivityItem(x.Id, "Maintenance", x.ServiceKind.ToString(),
                        x.ApprovalRequest?.State.ToString() ?? "Unavailable", x.CreatedAt, x.VehicleId, x.Vehicle?.RegistrationNumber,
                        x.ClaimedAmount, x.Currency)), service.TotalCount),
                    Page(incidents.Items.Select(x => new MyVehicleActivityItem(x.Id, "Incident", "Vehicle incident",
                        x.ApprovalRequest?.State.ToString() ?? "Unavailable", x.OccurredAt, x.VehicleCustody.VehicleId,
                        x.VehicleCustody.Vehicle?.RegistrationNumber)), incidents.TotalCount));
            }, ct, self: true);
    }
}
