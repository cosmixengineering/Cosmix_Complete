using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Api.Development;

// No deletion interface and no old-root write path. References alone never select a directory.
internal sealed class HrVehicleV2ContinuationFiles : IPrivateFileStore
{
    internal const string ManifestPath = @"D:\CosmixVerification\vehicle-v2-continuation-c537fea73e3c41b483e5d5ef4a2e064c.json";
    internal const string ManifestSha256 = "F7E273D086484CD633B941C477F25F09F14281B861963E6908C99B0F41B98E95";
    private readonly IPrivateFileStore oldStore, newStore;
    private readonly HashSet<(PrivateFileReference Reference, string Purpose)> oldReferences;
    private readonly HashSet<(PrivateFileReference Reference, string Purpose)> newReferences = [];
    private readonly Dictionary<string, int> remaining = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private bool failed;
    private readonly AsyncLocal<Func<CancellationToken, Task>?> authorize = new();
    private readonly AsyncLocal<string?> operation = new();
    private readonly AsyncLocal<Guid?> adjustment = new();

    internal HrVehicleV2ContinuationFiles(IPrivateFileStore oldStore, IPrivateFileStore newStore,
        IEnumerable<(PrivateFileReference Reference, string Purpose)> references)
    {
        this.oldStore = oldStore; this.newStore = newStore;
        oldReferences = references.ToHashSet();
        if (oldReferences.Count != 10 || oldReferences.Any(x => x.Reference.KeyReference != "hr-vehicle-v2"))
            throw HrVehicleV2Guard.Refuse();
        remaining.Add($"vehicle:service:{HrVehicleV2ContinuationGuard.VehicleId:N}", 7);
        remaining.Add($"vehicle:trip:correction:{HrVehicleV2Fixture.Ids[5]:N}", 1);
        remaining.Add($"vehicle:fuel:reconciliation:{HrVehicleV2Fixture.Ids[7]:N}", 1);
        foreach (var (actor, name, count) in new[]
        {
            (HrVehicleV2Guard.HrAccount, "vehicle.service.submit", 7),
            (HrVehicleV2Guard.AdminAccount, "vehicle.service.approve", 8),
            (HrVehicleV2Guard.AdminAccount, "vehicle.trip.correction.submit", 1),
            (HrVehicleV2Guard.SuperAdminAccount, "vehicle.trip.correction.review", 1),
            (HrVehicleV2Guard.AdminAccount, "vehicle.fuel.reconciliation.submit", 1),
            (HrVehicleV2Guard.SuperAdminAccount, "vehicle.fuel.reconciliation.review", 1)
        })
            foreach (var kind in new[] { "result", "audit" })
                remaining.Add($"attendance:operation:{actor:N}:{name}:{kind}", count);
        remaining.Add("adjustment-decisions", 2);
    }

    internal static JsonElement ReadManifest()
    {
        var bytes = File.ReadAllBytes(ManifestPath);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != ManifestSha256) throw HrVehicleV2Guard.Refuse();
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.GetProperty("Schema").GetString() != "vehicle-v2-continuation-readonly-v1") throw HrVehicleV2Guard.Refuse();
        return json.RootElement.Clone();
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IPrivateFileStore>();
        services.RemoveAll<IPunchRetentionPrivateFiles>();
        services.AddSingleton<IPunchRetentionPrivateFiles, DisabledDeletion>();
        services.AddSingleton(p =>
        {
            var configuration = p.GetRequiredService<IConfiguration>();
            HrVehicleV2ContinuationGuard.ValidateConfiguration(configuration, HrVehicleV2ContinuationGuard.SettingsPath);
            var old = new EncryptedPrivateFileStore(new PrivateFileStorageOptions
            {
                Directory = HrVehicleV2Guard.PrivateRoot + @"\files",
                KeyFile = HrVehicleV2Guard.PrivateRoot + @"\keys\files.key", KeyReference = "hr-vehicle-v2"
            });
            var current = new EncryptedPrivateFileStore(configuration.GetSection("PrivateFiles").Get<PrivateFileStorageOptions>()
                ?? throw HrVehicleV2Guard.Refuse());
            var references = ReadManifest().GetProperty("OldReferencePurposeAllowlist").EnumerateArray()
                .Select(x => (x.GetProperty("Reference").Deserialize<PrivateFileReference>() ?? throw HrVehicleV2Guard.Refuse(),
                    x.GetProperty("Purpose").GetString() ?? throw HrVehicleV2Guard.Refuse())).ToArray();
            return new HrVehicleV2ContinuationFiles(old, current, references);
        });
        services.AddSingleton<IPrivateFileStore>(p => p.GetRequiredService<HrVehicleV2ContinuationFiles>());
    }

    // The delegate uses the request's normal authorization repository and transaction.
    internal IDisposable Enter(string name, Func<CancellationToken, Task> reauthorize, Guid? adjustmentId = null)
    {
        if (operation.Value is not null) throw HrVehicleV2Guard.Refuse();
        operation.Value = name; authorize.Value = reauthorize; adjustment.Value = adjustmentId;
        return new Lease(() => { operation.Value = null; authorize.Value = null; adjustment.Value = null; });
    }

    public async Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var check = authorize.Value ?? throw HrVehicleV2Guard.Refuse();
        var name = operation.Value ?? throw HrVehicleV2Guard.Refuse();
        var service = name.StartsWith("service-", StringComparison.Ordinal);
        var review = name.EndsWith("-review", StringComparison.Ordinal);
        var approve = name.EndsWith("-approve", StringComparison.Ordinal);
        var account = service ? approve ? HrVehicleV2Guard.AdminAccount : HrVehicleV2Guard.HrAccount :
            review ? HrVehicleV2Guard.SuperAdminAccount : HrVehicleV2Guard.AdminAccount;
        var op = service ? approve ? "vehicle.service.approve" : "vehicle.service.submit" :
            name.StartsWith("correction", StringComparison.Ordinal) ? "vehicle.trip.correction." + (review ? "review" : "submit") :
            "vehicle.fuel.reconciliation." + (review ? "review" : "submit");
        var artifact = service && !approve ? $"vehicle:service:{HrVehicleV2ContinuationGuard.VehicleId:N}" :
            name == "correction" ? $"vehicle:trip:correction:{HrVehicleV2Fixture.Ids[5]:N}" :
            name == "reconciliation" ? $"vehicle:fuel:reconciliation:{HrVehicleV2Fixture.Ids[7]:N}" :
            review && adjustment.Value.HasValue ? $"vehicle:adjustment:decision:{adjustment.Value.Value:N}" : "";
        if (purpose != artifact && purpose != $"attendance:operation:{account:N}:{op}:result" &&
            purpose != $"attendance:operation:{account:N}:{op}:audit") throw HrVehicleV2Guard.Refuse();
        var bucket = purpose;
        if (purpose.StartsWith("vehicle:adjustment:decision:", StringComparison.Ordinal) &&
            Guid.TryParseExact(purpose["vehicle:adjustment:decision:".Length..], "N", out _) &&
            name is "correction-review" or "reconciliation-review") bucket = "adjustment-decisions";
        lock (gate)
        {
            if (failed || !remaining.TryGetValue(bucket, out var count) || count <= 0) throw HrVehicleV2Guard.Refuse();
            remaining[bucket] = count - 1;
        }
        try
        {
            await check(ct);
            var reference = await newStore.WriteAsync(content, purpose, ct);
            await check(ct);
            if (reference.KeyReference != HrVehicleV2ContinuationGuard.KeyReference) throw HrVehicleV2Guard.Refuse();
            lock (gate) if (!newReferences.Add((reference, purpose))) throw HrVehicleV2Guard.Refuse();
            return reference;
        }
        catch { lock (gate) failed = true; throw; }
    }

    public async Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        IPrivateFileStore store;
        lock (gate)
        {
            if (failed) throw HrVehicleV2Guard.Refuse();
            if (oldReferences.Contains((reference, purpose))) store = oldStore;
            else if (newReferences.Contains((reference, purpose))) store = newStore;
            else throw HrVehicleV2Guard.Refuse();
        }
        var check = authorize.Value ?? throw HrVehicleV2Guard.Refuse();
        await check(ct);
        var bytes = await store.ReadAsync(reference, purpose, ct);
        try { await check(ct); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class DisabledDeletion : IPunchRetentionPrivateFiles
    {
        public Task<bool> DeleteAuthenticatedAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
            => throw HrVehicleV2Guard.Refuse();
    }
}
