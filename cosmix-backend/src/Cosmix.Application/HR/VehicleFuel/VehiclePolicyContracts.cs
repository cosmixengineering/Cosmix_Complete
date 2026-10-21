using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.HR.VehicleFuel;

public static class VehiclePolicyPermissions
{
    public const string Manage = "attendance.vehicle.policy.manage";
    // The composition owner must register this definition; no role receives a grant here.
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Manage, "Read, validate and publish dated HR vehicle policies as fixed Admin/SuperAdmin with explicit All scope", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVehiclePolicyRequest([property: JsonRequired] string Code,
    [property: JsonRequired] string Name, [property: JsonRequired] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ValidateVehiclePolicyRequest([property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] JsonElement Rules);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewVehiclePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] JsonElement Rules);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishVehiclePolicyRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] DateOnly EffectiveFrom,
    [property: JsonRequired] DateOnly? EffectiveTo, [property: JsonRequired] JsonElement Rules,
    [property: JsonRequired] string ExpectedPreviewFingerprint, [property: JsonRequired] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehiclePolicyRules([property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] decimal? StandardFuelKmPerLiter,
    [property: JsonRequired] decimal? ClaimFuelPricePerLiter,
    [property: JsonRequired] decimal? TuningKm, [property: JsonRequired] decimal? OverhaulKm,
    [property: JsonRequired] VehicleWorkflowPolicy Workflow,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] VehicleAdjustmentPolicy? Adjustments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] VehiclePolicySuccessor? Succession = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehiclePolicySuccessor([property: JsonRequired] Guid PreviousPolicyVersionId,
    [property: JsonRequired] string AuthorityReference);

public sealed record VehiclePolicyError(string Path, string Code);
public sealed record VehiclePolicyValidation(bool IsValid, string? CanonicalJson, string? ConfigurationSha256,
    IReadOnlyList<VehiclePolicyError> Errors);
public sealed record VehiclePolicyView(Guid Id, string Code, string Name, bool IsActive, int Revision, string Version);
public sealed record VehiclePolicyVersionView(Guid Id, Guid PolicyId, int Number, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, string RulesSchemaVersion, string RulesJson, string ConfigurationSha256,
    DateTimeOffset PublishedAt, string PublishedByUserId, string Authority);
public sealed record VehiclePolicyPreview(bool CanPublish, int Revision, string Version, string? CanonicalJson,
    string? ConfigurationSha256, string PreviewFingerprint, IReadOnlyList<VehiclePolicyError> Errors);
public sealed record VehiclePolicyPublicationResult(VehiclePolicyView Policy, VehiclePolicyVersionView Published);
