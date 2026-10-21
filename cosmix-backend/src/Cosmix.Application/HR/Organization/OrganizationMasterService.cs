using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Security;

namespace Cosmix.Application.HR.Organization;

public sealed class OrganizationMasterService(IOrganizationRepository repository, OrganizationOperation operation)
{
    public Task<PagedResult<OrganizationMasterResponse>> ListAsync(OrganizationMasterKind kind,
        OrganizationListRequest request, CancellationToken ct) => operation.ReadAsync(Permissions.OrganizationRead, token =>
        {
            CheckKind(kind); OrganizationOperation.Validate(request);
            return repository.ListAsync(kind, request, token);
        }, ct);

    public Task<OrganizationMasterResponse> GetAsync(OrganizationMasterKind kind, Guid id, CancellationToken ct) =>
        operation.ReadAsync(Permissions.OrganizationRead, async token =>
        {
            CheckKind(kind);
            return await repository.GetAsync(kind, id, token) ?? throw OrganizationOperation.Missing();
        }, ct);

    public Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(OrganizationMasterKind kind, Guid id,
        OrganizationListRequest request, CancellationToken ct) => operation.ReadAsync(Permissions.OrganizationRead, async token =>
        {
            CheckKind(kind); OrganizationOperation.Validate(request);
            if (await repository.GetAsync(kind, id, token) is null) throw OrganizationOperation.Missing();
            return await repository.HistoryAsync(kind.ToString(), id, request, token);
        }, ct);

    public Task<OrganizationMasterResponse> CreateAsync(OrganizationMasterKind kind, CreateOrganizationMasterRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        CheckKind(kind); OrganizationOperation.Validate(request);
        var input = new CreateOrganizationMasterRequest { Code = request.Code.ToUpperInvariant(), Name = request.Name.Trim(),
            CityId = request.CityId, Province = request.Province?.Trim() };
        CheckFields(kind, input.Name, input.CityId, input.Province);
        return operation.WriteAsync(Permissions.OrganizationManage, kind.ToString(), $"organization.{kind}.create", key,
            input, "Created", context, async token =>
            {
                var city = await ValidateCityAsync(kind, input.CityId, null, token);
                var result = await repository.CreateAsync(kind, input, city, token);
                return (result.Id, (object?)null, result);
            }, ct);
    }

    public Task<OrganizationMasterResponse> UpdateAsync(OrganizationMasterKind kind, Guid id,
        UpdateOrganizationMasterRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        CheckKind(kind); OrganizationOperation.Validate(request); OrganizationOperation.Version(request.ExpectedVersion);
        var input = new UpdateOrganizationMasterRequest { Name = request.Name.Trim(), CityId = request.CityId,
            Province = request.Province?.Trim(), ExpectedVersion = request.ExpectedVersion, Reason = request.Reason.Trim() };
        CheckFields(kind, input.Name, input.CityId, input.Province);
        return operation.WriteAsync(Permissions.OrganizationManage, kind.ToString(), $"organization.{kind}.update", key,
            new { Id = id, Input = input }, input.Reason, context, async token =>
            {
                var before = await repository.GetAsync(kind, id, token) ?? throw OrganizationOperation.Missing();
                OrganizationOperation.ExpectVersion(before.Version, input.ExpectedVersion);
                var city = await ValidateCityAsync(kind, input.CityId, before.CityId, token);
                var result = await repository.UpdateAsync(kind, id, input, city, token);
                return (id, (object?)before, result);
            }, ct);
    }

    public Task<OrganizationMasterResponse> SetStatusAsync(OrganizationMasterKind kind, Guid id,
        SetOrganizationStatusRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        CheckKind(kind); OrganizationOperation.Validate(request); OrganizationOperation.Version(request.ExpectedVersion);
        var input = new SetOrganizationStatusRequest { IsActive = request.IsActive, ExpectedVersion = request.ExpectedVersion, Reason = request.Reason.Trim() };
        return operation.WriteAsync(Permissions.OrganizationManage, kind.ToString(), $"organization.{kind}.status", key,
            new { Id = id, Input = input }, input.Reason, context, async token =>
            {
                var before = await repository.GetAsync(kind, id, token) ?? throw OrganizationOperation.Missing();
                OrganizationOperation.ExpectVersion(before.Version, input.ExpectedVersion);
                if (input.IsActive == true && kind == OrganizationMasterKind.Branch)
                    await ValidateCityAsync(kind, before.CityId, null, token);
                var result = await repository.SetStatusAsync(kind, id, input, token);
                return (id, (object?)before, result);
            }, ct);
    }

    private async Task<string?> ValidateCityAsync(OrganizationMasterKind kind, Guid? id, Guid? previous, CancellationToken ct)
    {
        if (kind != OrganizationMasterKind.Branch) return null;
        if (!id.HasValue || id == Guid.Empty) throw OrganizationOperation.Invalid("office_city_required");
        var city = await repository.GetAsync(OrganizationMasterKind.City, id.Value, ct);
        if (city is null || (!city.IsActive && previous != id)) throw OrganizationOperation.Invalid("active_city_required");
        return city.Name;
    }
    private static void CheckFields(OrganizationMasterKind kind, string name, Guid? city, string? province)
    {
        if (string.IsNullOrWhiteSpace(name) || (kind == OrganizationMasterKind.City && name.Length > 100) ||
            (kind != OrganizationMasterKind.Branch && city.HasValue) ||
            (kind != OrganizationMasterKind.City && province is not null)) throw OrganizationOperation.Invalid();
    }
    private static void CheckKind(OrganizationMasterKind kind)
    {
        if (!Enum.IsDefined(kind)) throw OrganizationOperation.Invalid();
    }
}
