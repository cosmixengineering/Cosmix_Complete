using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Sites;

public sealed class Site
{
    private Site() { }

    public Site(string code, string name, string? address = null, Guid? supervisorEmployeeId = null,
        string? city = null, string? clientName = null, Guid? projectId = null,
        DateOnly? startsOn = null, DateOnly? endsOn = null, Guid? cityId = null)
    {
        if (endsOn.HasValue && (!startsOn.HasValue || endsOn < startsOn))
            throw new ArgumentException("A site end date requires a valid start date.", nameof(endsOn));
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code);
        Name = ModelGuard.Required(name, 150, nameof(name));
        Address = ModelGuard.Optional(address, 500, nameof(address));
        City = ModelGuard.Optional(city, 100, nameof(city));
        CityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : null;
        ClientName = ModelGuard.Optional(clientName, 200, nameof(clientName));
        ProjectId = projectId.HasValue ? ModelGuard.Id(projectId.Value, nameof(projectId)) : null;
        StartsOn = startsOn;
        EndsOn = endsOn;
        SupervisorEmployeeId = supervisorEmployeeId.HasValue
            ? ModelGuard.Id(supervisorEmployeeId.Value, nameof(supervisorEmployeeId)) : null;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public Guid? CityId { get; private set; }
    public Cosmix.Domain.HR.Branches.City? CityReference { get; private set; }
    public string? ClientName { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Cosmix.Domain.Authentication.Scopes.Project? Project { get; private set; }
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public Guid? SupervisorEmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee? Supervisor { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string? address, Guid? supervisorEmployeeId, string? city,
        string? clientName, Guid? projectId, DateOnly? startsOn, DateOnly? endsOn, Guid? cityId)
    {
        if (endsOn.HasValue && (!startsOn.HasValue || endsOn < startsOn))
            throw new ArgumentException("A site end date requires a valid start date.", nameof(endsOn));

        var validatedName = ModelGuard.Required(name, 150, nameof(name));
        var validatedAddress = ModelGuard.Optional(address, 500, nameof(address));
        var validatedCity = ModelGuard.Optional(city, 100, nameof(city));
        Guid? validatedCityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : null;
        var validatedClientName = ModelGuard.Optional(clientName, 200, nameof(clientName));
        Guid? validatedProjectId = projectId.HasValue ? ModelGuard.Id(projectId.Value, nameof(projectId)) : null;
        Guid? validatedSupervisorEmployeeId = supervisorEmployeeId.HasValue
            ? ModelGuard.Id(supervisorEmployeeId.Value, nameof(supervisorEmployeeId)) : null;

        Name = validatedName;
        Address = validatedAddress;
        City = validatedCity;
        CityId = validatedCityId;
        ClientName = validatedClientName;
        ProjectId = validatedProjectId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        SupervisorEmployeeId = validatedSupervisorEmployeeId;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
