using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Api.Development;

internal static class HrVehicleTestingDatabase
{
    // Read-only startup checks, on the connection already verified by the shared identity/schema guard.
    internal static async Task VerifyAsync(CosmixDbContext db, CancellationToken ct)
    {
        var accounts = await db.UserAccounts.AsNoTracking().Select(a => new { a.Id, a.EmployeeId, a.LoginName }).ToArrayAsync(ct);
        if (accounts.Length != 3 ||
            accounts.Count(a => a.Id == HrVehicleTestingGuard.AdminAccount && a.EmployeeId == HrVehicleTestingGuard.AdminEmployee &&
                a.LoginName == "admin@example.invalid") != 1 ||
            accounts.Count(a => a.Id == HrVehicleTestingGuard.HrAccount && a.EmployeeId == HrVehicleTestingGuard.HrEmployee &&
                a.LoginName == "hr@example.invalid") != 1 ||
            await db.Set<Vehicle>().AsNoTracking().AnyAsync(v => v.RegistrationNumber == HrVehicleTestingGuard.Marker, ct) ||
            await db.Set<VehicleCustody>().AsNoTracking().AnyAsync(c => c.EmployeeId == HrVehicleTestingGuard.HrEmployee, ct) ||
            await db.Set<IdempotencyReceipt>().AsNoTracking().AnyAsync(r => r.Key.StartsWith(HrVehicleTestingGuard.Marker), ct))
            throw HrBusinessTestingGuard.Refuse();
    }
}
