using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.VehicleFuel;

public sealed class Vehicle
{
    private Vehicle() { }

    public Vehicle(string registrationNumber, string vehicleType, string makeModel, string? assetReference)
    {
        Id = Guid.NewGuid();
        RegistrationNumber = ModelGuard.Code(registrationNumber, 40);
        VehicleType = ModelGuard.Required(vehicleType, 60, nameof(vehicleType));
        MakeModel = ModelGuard.Required(makeModel, 150, nameof(makeModel));
        AssetReference = ModelGuard.Optional(assetReference, 100, nameof(assetReference));
    }

    public Guid Id { get; private set; }
    public string RegistrationNumber { get; private set; } = string.Empty;
    public string VehicleType { get; private set; } = string.Empty;
    public string MakeModel { get; private set; } = string.Empty;
    public string? AssetReference { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
