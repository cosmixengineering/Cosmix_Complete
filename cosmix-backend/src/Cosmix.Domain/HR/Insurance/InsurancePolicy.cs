using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Domain.HR.Insurance;

/// <summary>
/// Immutable coverage for exactly one employee or vehicle, with an inclusive expiry date.
/// Renewals and corrections create successor records; services select the applicable coverage
/// and authorize access to the optional private evidence file.
/// </summary>
public sealed class InsurancePolicy : IAppendOnlyEntity
{
    private InsurancePolicy() { }

    public InsurancePolicy(Guid? employeeId, Guid? vehicleId, string providerName, string policyNumber,
        DateOnly coverageStartDate, DateOnly expiryDate, Guid? storedFileId, string recordedByUserId,
        DateTimeOffset recordedAt, InsurancePolicy? supersedesPolicy = null)
    {
        if (employeeId.HasValue == vehicleId.HasValue)
            throw new ArgumentException("Insurance coverage requires exactly one employee or vehicle owner.");
        if (expiryDate < coverageStartDate)
            throw new ArgumentException("Inclusive expiry cannot precede the coverage start.", nameof(expiryDate));
        if (supersedesPolicy is not null)
        {
            if (supersedesPolicy.EmployeeId != employeeId || supersedesPolicy.VehicleId != vehicleId)
                throw new ArgumentException("The predecessor must belong to the same owner.", nameof(supersedesPolicy));
            if (recordedAt < supersedesPolicy.RecordedAt)
                throw new ArgumentException("A successor cannot be recorded before its predecessor.", nameof(recordedAt));
        }

        Id = Guid.NewGuid();
        EmployeeId = employeeId.HasValue ? ModelGuard.Id(employeeId.Value, nameof(employeeId)) : null;
        VehicleId = vehicleId.HasValue ? ModelGuard.Id(vehicleId.Value, nameof(vehicleId)) : null;
        ProviderName = ModelGuard.Required(providerName, 200, nameof(providerName));
        PolicyNumber = ModelGuard.Required(policyNumber, 100, nameof(policyNumber));
        CoverageStartDate = coverageStartDate;
        ExpiryDate = expiryDate;
        StoredFileId = storedFileId.HasValue ? ModelGuard.Id(storedFileId.Value, nameof(storedFileId)) : null;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        SupersedesPolicyId = supersedesPolicy?.Id;
    }

    public Guid Id { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string PolicyNumber { get; private set; } = string.Empty;
    public DateOnly CoverageStartDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public Guid? StoredFileId { get; private set; }
    public StoredFile? StoredFile { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid? SupersedesPolicyId { get; private set; }
    public InsurancePolicy? SupersedesPolicy { get; private set; }
}
