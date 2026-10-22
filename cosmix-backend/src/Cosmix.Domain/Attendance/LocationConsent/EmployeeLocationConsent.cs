using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.Attendance.LocationConsent;

public sealed class EmployeeLocationConsent : IAppendOnlyEntity
{
    private EmployeeLocationConsent() { }

    public EmployeeLocationConsent(Guid employeeId, Guid noticePolicyId, int noticePolicyVersion,
        DateTimeOffset acceptedAtUtc, string? deviceMetadata = null, string? ipAddress = null)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        NoticePolicyId = ModelGuard.Id(noticePolicyId, nameof(noticePolicyId));
        NoticePolicyVersion = noticePolicyVersion > 0 ? noticePolicyVersion : throw new ArgumentOutOfRangeException(nameof(noticePolicyVersion));
        AcceptedAtUtc = ModelGuard.Utc(acceptedAtUtc);
        DeviceMetadata = ModelGuard.Optional(deviceMetadata, 500, nameof(deviceMetadata));
        IpAddress = ModelGuard.Optional(ipAddress, 45, nameof(ipAddress));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Guid NoticePolicyId { get; private set; }
    public LocationNoticePolicy NoticePolicy { get; private set; } = null!;
    public int NoticePolicyVersion { get; private set; }
    public DateTimeOffset AcceptedAtUtc { get; private set; }
    public string? DeviceMetadata { get; private set; }
    public string? IpAddress { get; private set; }
}
