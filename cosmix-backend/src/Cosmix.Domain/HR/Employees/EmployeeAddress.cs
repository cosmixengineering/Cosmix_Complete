using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

public enum EmployeeAddressType
{
    Current = 1,
    Permanent = 2
}

public sealed class EmployeeAddress : IEmployeeProfileRecord
{
    private EmployeeAddress() { }

    public EmployeeAddress(Guid employeeId, EmployeeAddressType addressType,
        string? addressLine1, string? addressLine2, string? city, string? province, string? postalCode,
        Guid? cityId = null)
    {
        if (!Enum.IsDefined(addressType))
            throw new ArgumentOutOfRangeException(nameof(addressType));
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        AddressType = addressType;
        CityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : null;
        SetDetails(addressLine1, addressLine2, city, province, postalCode);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public EmployeeAddressType AddressType { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public Guid? CityId { get; private set; }
    public Cosmix.Domain.HR.Branches.City? CityReference { get; private set; }
    public string? Province { get; private set; }
    public string? PostalCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void UpdateDetails(string? addressLine1, string? addressLine2, string? city,
        string? province, string? postalCode, Guid? cityId)
    {
        var newCityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : (Guid?)null;
        SetDetails(addressLine1, addressLine2, city, province, postalCode);
        CityId = newCityId;
    }

    public void SetDetails(string? addressLine1, string? addressLine2, string? city,
        string? province, string? postalCode)
    {
        var line1 = ModelGuard.Optional(addressLine1, 250, nameof(addressLine1));
        var line2 = ModelGuard.Optional(addressLine2, 250, nameof(addressLine2));
        var newCity = ModelGuard.Optional(city, 100, nameof(city));
        var newProvince = ModelGuard.Optional(province, 100, nameof(province));
        var newPostalCode = ModelGuard.Optional(postalCode, 20, nameof(postalCode));
        AddressLine1 = line1;
        AddressLine2 = line2;
        City = newCity;
        Province = newProvince;
        PostalCode = newPostalCode;
    }
}
