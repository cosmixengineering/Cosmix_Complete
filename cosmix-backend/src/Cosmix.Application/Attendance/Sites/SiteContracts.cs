using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;

namespace Cosmix.Application.Attendance.Sites;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateSiteRequest
{
    [Required, RegularExpression("^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$")] public string Code { get; init; } = "";
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required, StringLength(500)] public string Address { get; init; } = "";
    [Required] public Guid? CityId { get; init; }
    [StringLength(200)] public string? ClientName { get; init; }
    public Guid? ProjectId { get; init; }
    [Required] public DateOnly StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    public Guid? SupervisorEmployeeId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateSiteRequest
{
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required, StringLength(500)] public string Address { get; init; } = "";
    [Required] public Guid? CityId { get; init; }
    [StringLength(200)] public string? ClientName { get; init; }
    public Guid? ProjectId { get; init; }
    [Required] public DateOnly StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    public Guid? SupervisorEmployeeId { get; init; }
    [Required, StringLength(12, MinimumLength = 12)] public string ExpectedVersion { get; init; } = "";
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}

public sealed record SiteDetails(string Name, string Address, Guid CityId, string? ClientName,
    Guid? ProjectId, DateOnly StartsOn, DateOnly? EndsOn, Guid? SupervisorEmployeeId);
public sealed record SiteResponse(Guid Id, string Code, string Name, string? Address, Guid? CityId,
    string? CityName, string? ClientName, Guid? ProjectId, DateOnly? StartsOn, DateOnly? EndsOn,
    Guid? SupervisorEmployeeId, bool IsActive, string Version);

public interface ISiteRepository
{
    Task<PagedResult<SiteResponse>> ListAsync(OrganizationListRequest request, CancellationToken ct);
    Task<SiteResponse?> GetAsync(Guid id, CancellationToken ct);
    Task<SiteResponse> CreateAsync(string code, SiteDetails details, string cityName, CancellationToken ct);
    Task<SiteResponse> UpdateAsync(Guid id, SiteDetails details, string cityName, byte[] expectedVersion, CancellationToken ct);
    Task<SiteResponse> SetStatusAsync(Guid id, bool active, byte[] expectedVersion, CancellationToken ct);
    Task<bool> IsActiveProjectAsync(Guid id, CancellationToken ct);
    Task<bool> IsEligibleSupervisorAsync(Guid id, CancellationToken ct);
}
