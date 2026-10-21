using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

public sealed class EmployeeEmergencyContact : IEmployeeProfileRecord
{
    private EmployeeEmergencyContact() { }

    public EmployeeEmergencyContact(Guid employeeId, string? name, string? relationship,
        string? phone, string? alternatePhone)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        SetDetails(name, relationship, phone, alternatePhone);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public string? Name { get; private set; }
    public string? Relationship { get; private set; }
    public string? Phone { get; private set; }
    public string? AlternatePhone { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void SetDetails(string? name, string? relationship, string? phone, string? alternatePhone)
    {
        var newName = ModelGuard.Optional(name, 200, nameof(name));
        var newRelationship = ModelGuard.Optional(relationship, 100, nameof(relationship));
        var newPhone = ModelGuard.Optional(phone, 30, nameof(phone));
        var newAlternatePhone = ModelGuard.Optional(alternatePhone, 30, nameof(alternatePhone));
        Name = newName;
        Relationship = newRelationship;
        Phone = newPhone;
        AlternatePhone = newAlternatePhone;
    }
}
