using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;
using Microsoft.AspNetCore.Http.Features;

namespace Cosmix.Api.Development;

// Admission only: normal authentication, CSRF, permissions and service invariants still execute.
internal sealed class HrVehicleV2Boundary
{
    internal const string Auth = "/api/v1/auth/web/";
    internal const string SeedPath = "/api/v1/local-testing/vehicle-v2/seed";
    internal const string Vehicles = "/api/v1/hr/vehicles";
    internal const string Maintenance = "/api/v1/me/vehicles/maintenance";
    internal const string KeyPrefix = HrVehicleV2Guard.Marker + "-";
    private static readonly string Trip = Vehicles + "/trips/" + HrVehicleV2Fixture.Ids[5];
    private static readonly string Fuel = Vehicles + "/fuel-claims/" + HrVehicleV2Fixture.Ids[7];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly object LoginKey = new(), CommandKey = new();
    private readonly object gate = new();
    private readonly Dictionary<string, Login> browsers = new(StringComparer.Ordinal);
    private readonly HashSet<string> emails = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> attempts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> resources = new(StringComparer.Ordinal);
    private readonly HashSet<string> completed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> payloads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> admittedKeys = new(StringComparer.Ordinal);
    private Guid vehicleId;
    private int requests, inFlight;
    private string? activeOperation, tripFingerprint, fuelFingerprint;
    private bool uncertain, seedAttempted, seeded;
    private readonly bool continuation;
    private sealed class Login(HrVehicleV2Guard.Identity identity)
    {
        internal readonly HrVehicleV2Guard.Identity Identity = identity;
        internal Guid Challenge, Session;
        internal bool Busy, Dispatched;
        internal int OtpReads, Verifications, Logouts;
    }
    private sealed record Command(string Operation, string Key, bool Negative, Guid Actor);

    public HrVehicleV2Boundary(IConfiguration config)
    {
        continuation = HrVehicleV2ContinuationGuard.IsSelected;
        if (continuation) HrVehicleV2ContinuationGuard.ValidateConfiguration(config, HrVehicleV2ContinuationGuard.SettingsPath);
        else HrVehicleV2Guard.ValidateConfiguration(config, HrVehicleV2Guard.SettingsPath);
    }

    // Called only after the continuation startup reconciliation has succeeded, never from HTTP.
    internal void InitializeContinuation()
    {
        lock (gate)
        {
            if (!continuation || !HrVehicleV2ContinuationGuard.IsSelected || seeded || requests != 0 || uncertain)
                throw HrVehicleV2Guard.Refuse();
            vehicleId = HrVehicleV2ContinuationGuard.VehicleId;
            resources.Add("vehicle", vehicleId);
            resources.Add("service-tuning-1", HrVehicleV2ContinuationGuard.PendingServiceId);
            completed.Add("vehicle");
            completed.Add("service-tuning-1");
            seedAttempted = seeded = true;
        }
    }

    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (Interlocked.Increment(ref requests) > (continuation ? 100 : 160)) { context.Response.StatusCode = 503; return; }
        var request = context.Request;
        var path = request.Path.Value ?? "";
        lock (gate) if (uncertain) { context.Response.StatusCode = 503; return; }
        if (!(continuation ? HrVehicleV2ContinuationGuard.IsSelected : HrVehicleV2Guard.IsSelected) ||
            continuation && (!seeded || DateTimeOffset.UtcNow >= new DateTimeOffset(2026, 10, 26, 19, 0, 0, TimeSpan.Zero)) ||
            !LocalTesting.IsLocal(context) || request.Host.Value != "localhost:7481" ||
            request.PathBase.HasValue || request.QueryString.HasValue ||
            context.Features.Get<IHttpRequestFeature>()?.RawTarget != path ||
            new[] { "Authorization", "Content-Encoding", "Transfer-Encoding", "Range" }.Any(request.Headers.ContainsKey))
        { context.Response.StatusCode = 404; return; }
        try
        {
            if (request.Method == "GET")
            {
                lock (gate)
                    if (request.ContentLength is > 0 || context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
                        request.Headers.ContainsKey("Idempotency-Key") || !ReadAllowed(path))
                    { context.Response.StatusCode = 404; return; }
                using var readEvidence = continuation && path.StartsWith(Vehicles, StringComparison.Ordinal)
                    ? context.RequestServices.GetRequiredService<HrVehicleV2ContinuationFiles>().Enter(
                        path.StartsWith(Trip, StringComparison.Ordinal) ? "correction" : "reconciliation",
                        ct => HrVehicleV2ContinuationAuthority.RequireAsync(context,
                            path.StartsWith(Trip, StringComparison.Ordinal) ? "correction" : "reconciliation", ct)) : null;
                await CaptureAsync(context, next, root =>
                {
                    if (path == Trip + "/attribution-correction-context")
                    {
                        if (Id(root, "tripId") != HrVehicleV2Fixture.Ids[5] || Id(root, "employeeId") != HrVehicleV2Guard.HrEmployee)
                            throw HrVehicleV2Guard.Refuse();
                        tripFingerprint = Text(root, "sourceFingerprint") ?? throw HrVehicleV2Guard.Refuse();
                    }
                    if (path == Fuel + "/reconciliation-context")
                    {
                        if (Id(root, "resourceId") != HrVehicleV2Fixture.Ids[7] || Id(root, "tripId") != HrVehicleV2Fixture.Ids[5] ||
                            Id(root, "employeeId") != HrVehicleV2Guard.HrEmployee) throw HrVehicleV2Guard.Refuse();
                        fuelFingerprint = Text(root, "sourceFingerprint") ?? throw HrVehicleV2Guard.Refuse();
                    }
                });
                return;
            }
            bool mayBeEmpty;
            lock (gate) mayBeEmpty = path == Auth + "logout" || resources.Any(r =>
                r.Key.StartsWith("service-", StringComparison.Ordinal) && path == Vehicles + "/maintenance/" + r.Value + "/approve");
            var emptyBody = mayBeEmpty && request.ContentLength is null or 0 &&
                context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true;
            if (request.Method != "POST" || request.ContentLength is > 16384 ||
                !emptyBody && request.ContentType is not ("application/json" or "application/json; charset=utf-8"))
            { context.Response.StatusCode = 400; return; }
            var original = request.Body;
            var bytes = new byte[16385]; var count = 0;
            while (count < bytes.Length)
            {
                var read = await original.ReadAsync(bytes.AsMemory(count), context.RequestAborted);
                if (read == 0) break;
                count += read;
            }
            if (count > 16384) { context.Response.StatusCode = 400; return; }
            if (count == 0 && mayBeEmpty) { bytes[0] = (byte)'{'; bytes[1] = (byte)'}'; count = 2; }
            using var body = new MemoryStream(bytes, 0, count);
            using var document = JsonDocument.Parse(body);
            body.Position = 0; request.Body = body;
            try
            {
                if (path.StartsWith(Auth, StringComparison.Ordinal)) await AuthenticationAsync(context, next, path, document.RootElement);
                else await BusinessAsync(context, next, path, document.RootElement);
            }
            finally { request.Body = original; }
        }
        catch (Exception)
        {
            lock (gate) uncertain = true;
            if (context.Response.HasStarted) context.Abort();
            else { context.Response.Clear(); context.Response.StatusCode = 503; }
        }
    }

    internal async Task AuthorizeActorAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value;
        if (path is Auth + "csrf" or Auth + "login" or Auth + "otp/verify" or Auth + "local-testing/otp") { await next(context); return; }
        if (context.Items[CurrentAuthentication.ItemKey] is not AuthenticatedAccount actor)
        { context.Response.StatusCode = 401; return; }
        lock (gate)
        {
            var cookie = context.Request.Cookies[WebSessionCookies.Browser];
            if (uncertain || cookie is null || !browsers.TryGetValue(cookie, out var login) || login.Session == Guid.Empty ||
                login.Session != actor.SessionId || login.Identity.Account != actor.AccountId || login.Identity.Employee != actor.EmployeeId ||
                actor.Client != SessionClient.Web || !actor.MfaSatisfied ||
                context.Items[CommandKey] is Command command && command.Actor != actor.AccountId)
            { context.Response.StatusCode = 403; return; }
        }
        if (continuation && context.Items[CommandKey] is Command admitted && !admitted.Negative)
            await HrVehicleV2ContinuationAuthority.RequireAsync(context, admitted.Operation, context.RequestAborted);
        await next(context);
    }

    internal async Task PrepareOtpAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path == Auth + "local-testing/otp" && context.Items[LoginKey] is Login login)
        {
            lock (gate)
            {
                if (uncertain || login.Dispatched) throw HrVehicleV2Guard.Refuse();
                login.Dispatched = true;
            }
            await HrVehicleV2Host.DispatchOtpAsync(context, login.Challenge, login.Identity);
        }
        await next(context);
    }

    internal HrVehicleV2Guard.Identity RequireOtpIdentity(HttpContext context, Guid challenge)
    {
        lock (gate)
        {
            if (uncertain || context.Items[LoginKey] is not Login login || !login.Dispatched ||
                login.Challenge != challenge || challenge == Guid.Empty || login.Session != Guid.Empty) throw HrVehicleV2Guard.Refuse();
            return login.Identity;
        }
    }

    private async Task AuthenticationAsync(HttpContext context, RequestDelegate next, string path, JsonElement input)
    {
        var cookie = context.Request.Cookies[WebSessionCookies.Browser];
        if (cookie is not { Length: 43 } || context.Request.Headers.ContainsKey("Idempotency-Key"))
        { context.Response.StatusCode = 400; return; }
        Login login;
        lock (gate)
        {
            if (path == Auth + "login")
            {
                var identity = HrVehicleV2Guard.Actors.SingleOrDefault(a => a.Email == Text(input, "email"));
                if (!Fields(input, "email", "password") || identity is null || browsers.ContainsKey(cookie) || emails.Count >= 3 ||
                    emails.Contains(identity.Email) || Text(input, "password") is not { Length: > 0 and <= 1024 })
                { context.Response.StatusCode = 403; return; }
                emails.Add(identity.Email); login = new(identity); browsers.Add(cookie, login);
            }
            else
            {
                if (!browsers.TryGetValue(cookie, out var state) || state.Busy) { context.Response.StatusCode = 403; return; }
                login = state;
                if (path == Auth + "logout")
                {
                    if (!Fields(input) || login.Session == Guid.Empty || login.Logouts++ >= 2) { context.Response.StatusCode = 400; return; }
                }
                else if (path is Auth + "otp/verify" or Auth + "local-testing/otp")
                {
                    var verify = path == Auth + "otp/verify";
                    if (!(verify ? Fields(input, "challengeId", "code") : Fields(input, "challengeId")) ||
                        Text(input, "challengeId") != login.Challenge.ToString("D") || login.Challenge == Guid.Empty || login.Session != Guid.Empty ||
                        verify && (!login.Dispatched || Text(input, "code") is not { Length: 6 } code || !code.All(char.IsAsciiDigit)) ||
                        (verify ? login.Verifications++ >= 2 : login.OtpReads++ != 0)) { context.Response.StatusCode = 400; return; }
                }
                else { context.Response.StatusCode = 404; return; }
            }
            login.Busy = true; context.Items[LoginKey] = login;
        }
        try
        {
            await CaptureAsync(context, next, root =>
            {
                if (path == Auth + "login")
                {
                    if (context.Response.StatusCode != 202) throw HrVehicleV2Guard.Refuse();
                    login.Challenge = Id(root, "challengeId");
                }
                if (path == Auth + "otp/verify") login.Session = Id(root, "sessionId");
            }, path == Auth + "logout");
        }
        finally { lock (gate) login.Busy = false; }
    }

    private bool ReadAllowed(string path) => path is Auth + "csrf" or "/api/v1/me/permissions" || seeded &&
        (path == Trip + "/attribution-correction-context" || path == Trip + "/attribution-corrections" ||
         path == Fuel + "/reconciliation-context" || path == Fuel + "/reconciliations");

    private async Task BusinessAsync(HttpContext context, RequestDelegate next, string path, JsonElement input)
    {
        Command? command;
        lock (gate)
        {
            command = Admit(context, path, input);
            if (command is null || inFlight > 0 && activeOperation != command.Operation)
            { context.Response.StatusCode = 400; return; }
            activeOperation = command.Operation; inFlight++; context.Items[CommandKey] = command;
        }
        try
        {
            Guid? adjustmentId;
            lock (gate) adjustmentId = command.Operation.EndsWith("-review", StringComparison.Ordinal)
                ? resources.GetValueOrDefault(command.Operation[..^7]) : null;
            using var evidence = continuation ? context.RequestServices.GetRequiredService<HrVehicleV2ContinuationFiles>()
                .Enter(command.Operation, ct => HrVehicleV2ContinuationAuthority.RequireAsync(context, command.Operation, ct), adjustmentId) : null;
            await CaptureAsync(context, next, root =>
            {
                if (command.Negative) throw HrVehicleV2Guard.Refuse();
                if (command.Operation == "seed") return;
                var id = Id(root, command.Operation.StartsWith("service-", StringComparison.Ordinal) || command.Operation == "vehicle" ? "id" : "requestId");
                var resource = command.Operation.Replace("-approve", "", StringComparison.Ordinal).Replace("-review", "", StringComparison.Ordinal);
                if (resources.TryGetValue(resource, out var prior) && prior != id) throw HrVehicleV2Guard.Refuse();
                resources[resource] = id;
                if (command.Operation == "vehicle")
                {
                    if (Text(root, "registrationNumber") != HrVehicleV2Guard.Marker || Text(root, "vehicleType") != "Motorcycle") throw HrVehicleV2Guard.Refuse();
                    vehicleId = id;
                }
                completed.Add(command.Operation);
            });
        }
        finally { lock (gate) if (--inFlight == 0) activeOperation = null; }
    }

    private Command? Admit(HttpContext context, string path, JsonElement input)
    {
        if (uncertain || continuation && (path == SeedPath || path == Vehicles)) return null;
        if (path == SeedPath)
        {
            if (seedAttempted || !Fields(input, "vehicleId") || vehicleId == Guid.Empty || Text(input, "vehicleId") != vehicleId.ToString("D") ||
                context.Request.Headers.ContainsKey("Idempotency-Key") || inFlight != 0) return null;
            return new("seed", "", false, HrVehicleV2Guard.SuperAdminAccount);
        }
        var header = context.Request.Headers["Idempotency-Key"];
        if (header.Count != 1 || header[0] is not string key || !key.StartsWith(KeyPrefix, StringComparison.Ordinal) || key.Length > 100) return null;
        var operation = key[KeyPrefix.Length..]; var probe = "";
        foreach (var suffix in new[] { "-duplicate", "-stale", "-self" })
            if (operation.EndsWith(suffix, StringComparison.Ordinal)) { probe = suffix; operation = operation[..^suffix.Length]; break; }
        if (operation == "create") operation = "vehicle";
        for (var day = 17; day <= 20; day++)
            foreach (var (kind, name) in new[] { ("Tuning", "tuning"), ("EngineOverhaul", "overhaul") })
                foreach (var suffix in new[] { "", "-approve" })
                    if (operation == $"service-{kind}-2026-09-{day}{suffix}") operation = $"service-{name}-{day - 16}{suffix}";
        // Aliases never open a second idempotency namespace for the same logical command.
        var canonicalKey = operation + probe;
        if (continuation && (operation == "vehicle" ||
            !completed.Contains("service-tuning-1-approve") && operation != "service-tuning-1-approve")) return null;
        if (continuation && operation == "service-tuning-1" &&
            (probe.Length != 0 || !completed.Contains("service-tuning-4-approve") || !completed.Contains("service-overhaul-4-approve") ||
             key != KeyPrefix + "service-Tuning-2026-09-17" ||
             Text(input, "evidenceReference") != HrVehicleV2Guard.Marker + "-17-Tuning" ||
             !input.TryGetProperty("odometerKm", out var km) || km.GetRawText() != "1200" ||
             !input.TryGetProperty("approvedOutstationKm", out var outstation) || outstation.GetRawText() != "200" ||
             !input.TryGetProperty("claimedAmount", out var amount) || amount.GetRawText() != "50")) return null;
        if (admittedKeys.TryGetValue(canonicalKey, out var admittedKey) && admittedKey != key) return null;
        if (attempts.GetValueOrDefault(key) >= (probe.Length == 0 ? 6 : 2)) return null;
        var actor = HrVehicleV2Guard.AdminAccount;
        var valid = false;
        if (operation == "vehicle")
            valid = probe.Length == 0 && path == Vehicles && Fields(input, "registrationNumber", "vehicleType", "makeModel", "assetReference") &&
                Text(input, "registrationNumber") == HrVehicleV2Guard.Marker && Text(input, "vehicleType") == "Motorcycle" &&
                Text(input, "makeModel") == HrVehicleV2Guard.Model && Text(input, "assetReference") == HrVehicleV2Guard.Marker;
        else if (!seeded) return null;
        else if (operation.StartsWith("service-", StringComparison.Ordinal))
        {
            var parts = operation.Split('-');
            if (parts.Length is not (3 or 4) || parts[1] is not ("tuning" or "overhaul") ||
                !int.TryParse(parts[2], out var ordinal) || ordinal is < 1 or > 4 || parts[2] != ordinal.ToString()) return null;
            var slot = $"service-{parts[1]}-{ordinal}";
            var approve = parts.Length == 4 && parts[3] == "approve";
            if (parts.Length == 4 && !approve) return null;
            actor = approve && probe != "-self" ? HrVehicleV2Guard.AdminAccount : HrVehicleV2Guard.HrAccount;
            if (approve)
                valid = (probe is "" or "-duplicate" or "-self") && resources.TryGetValue(slot, out var service) &&
                    path == Vehicles + "/maintenance/" + service + "/approve" && Fields(input);
            else
            {
                if (probe.Length != 0 || path != Maintenance || !Fields(input, "vehicleId", "serviceKind", "serviceDate", "odometerKm",
                    "approvedOutstationKm", "authorizedWorkshopReference", "evidenceReference", "claimedAmount", "currency")) return null;
                var value = input.Deserialize<SubmitServiceRecordRequest>(Json)!;
                valid = value.VehicleId == vehicleId && value.ServiceKind == (parts[1] == "tuning" ? VehicleServiceKind.Tuning : VehicleServiceKind.EngineOverhaul) &&
                    value.ServiceDate == new DateOnly(2026, 9, 16 + ordinal) && value.OdometerKm == 1200 && value.ApprovedOutstationKm == 200 &&
                    value.AuthorizedWorkshopReference == HrVehicleV2Fixture.Workshop && Evidence(value.EvidenceReference) &&
                    value.ClaimedAmount == (parts[1] == "tuning" ? 50 : 75) && value.Currency == "PKR" &&
                    (ordinal == 1 || completed.Contains($"service-{parts[1]}-{ordinal - 1}-approve")) &&
                    (ordinal <= 2 || completed.Contains("reconciliation-review"));
            }
        }
        else if (operation is "correction" or "reconciliation")
        {
            if (probe is not ("" or "-duplicate" or "-stale")) return null;
            if (operation == "correction" && path == Trip + "/attribution-corrections" &&
                completed.Contains("service-tuning-2-approve") && completed.Contains("service-overhaul-2-approve") &&
                Fields(input, "expectedTripVersion", "expectedLatestRequestId", "expectedSourceFingerprint", "segments", "evidenceReference", "reason"))
            {
                var value = input.Deserialize<SubmitVehicleTripAttributionCorrectionRequest>(Json)!;
                valid = value.ExpectedLatestRequestId is null && tripFingerprint is not null && value.ExpectedSourceFingerprint == tripFingerprint &&
                    Version(value.ExpectedTripVersion) && (probe != "-stale" || value.ExpectedTripVersion == "AAAAAAAAAAA=") &&
                    Segments(input.GetProperty("segments"), value.Segments) && Evidence(value.EvidenceReference) && Reason(value.Reason);
            }
            if (operation == "reconciliation" && probe != "-stale" && path == Fuel + "/reconciliations" && completed.Contains("correction-review") &&
                Fields(input, "expectedLatestRequestId", "expectedSourceFingerprint", "evidenceReference", "reason"))
            {
                var value = input.Deserialize<SubmitFuelReconciliationRequest>(Json)!;
                valid = value.ExpectedLatestRequestId is null && fuelFingerprint is not null && value.ExpectedSourceFingerprint == fuelFingerprint &&
                    Evidence(value.EvidenceReference) && Reason(value.Reason);
            }
        }
        else if (operation is "correction-review" or "reconciliation-review")
        {
            var resource = operation[..^7];
            actor = probe == "-self" ? HrVehicleV2Guard.AdminAccount : HrVehicleV2Guard.SuperAdminAccount;
            var prefix = resource == "correction" ? Trip + "/attribution-corrections/" : Fuel + "/reconciliations/";
            if (resources.TryGetValue(resource, out var id) && path == prefix + id + "/review" &&
                Fields(input, "expectedVersion", "decision", "evidenceReference", "reason"))
            {
                var value = input.Deserialize<ReviewVehicleAdjustmentRequest>(Json)!;
                valid = value.Decision == ApprovalState.Approved && Version(value.ExpectedVersion) &&
                    (probe != "-stale" || value.ExpectedVersion == "AAAAAAAAAAA=") && Evidence(value.EvidenceReference) && Reason(value.Reason);
            }
        }
        if (!valid) return null;
        admittedKeys.TryAdd(canonicalKey, key);
        // Freeze every idempotency payload before executing it, allowing only an explicit changed-payload conflict probe afterwards.
        var payload = Canonical(input);
        var changed = payloads.TryGetValue(key, out var original) && original != payload;
        if (changed && (!completed.Contains(operation) || inFlight != 0)) return null;
        if (!changed) payloads.TryAdd(key, payload);
        attempts[key] = attempts.GetValueOrDefault(key) + 1;
        return new(operation, key, changed || probe is "-self" or "-stale", actor);
    }

    internal async Task<IResult> SeedAsync(Guid id, Func<Task<HrVehicleV2SeedResult>> seed)
    {
        lock (gate)
        {
            if (continuation || uncertain || seedAttempted || seeded || vehicleId == Guid.Empty || id != vehicleId || activeOperation != "seed" || inFlight != 1)
                throw HrVehicleV2Guard.Refuse();
            seedAttempted = true;
        }
        var result = await seed();
        lock (gate)
        {
            if (result.VehicleId != vehicleId || result.TripId != HrVehicleV2Fixture.Ids[5] || result.FuelClaimId != HrVehicleV2Fixture.Ids[7] ||
                result.SourceRows != 12 || result.PrivateFiles != 5 || !result.PolicyVersionIds.SequenceEqual(HrVehicleV2Fixture.Ids[1..4]))
                throw HrVehicleV2Guard.Refuse();
            seeded = true;
        }
        return Results.Ok(result);
    }

    private static bool Segments(JsonElement json, IReadOnlyList<VehicleTripSegmentRequest>? rows)
    {
        if (rows is not { Count: 2 } || json.ValueKind != JsonValueKind.Array || json.GetArrayLength() != 2) return false;
        for (var i = 0; i < 2; i++)
        {
            var row = rows[i];
            if (!Fields(json[i], "businessDate", "startedAt", "endedAt", "odometerStartKm", "odometerEndKm", "mapDistanceKm", "policyVersionId", "commuteDistanceId", "evidenceReference") ||
                row.BusinessDate != new DateOnly(2026, 9, 15 + i) || row.PolicyVersionId != HrVehicleV2Fixture.Ids[1 + i] || row.CommuteDistanceId is not null ||
                row.StartedAt != new DateTimeOffset(2026, 9, 15, 18 + i, 0, 0, TimeSpan.Zero) ||
                row.EndedAt != (i == 0 ? new DateTimeOffset(2026, 9, 15, 19, 0, 0, TimeSpan.Zero) : new DateTimeOffset(2026, 9, 16, 1, 0, 0, TimeSpan.Zero)) ||
                row.OdometerStartKm != 1000 + 100 * i || row.OdometerEndKm != 1100 + 100 * i || row.MapDistanceKm != 100 || !Evidence(row.EvidenceReference)) return false;
        }
        return true;
    }

    private async Task CaptureAsync(HttpContext context, RequestDelegate next, Action<JsonElement> accept, bool empty = false)
    {
        var original = context.Response.Body;
        using var response = new LimitedResponse(); context.Response.Body = response;
        try
        {
            await next(context);
            lock (gate)
            {
                if (context.Response.StatusCode >= 500) throw HrVehicleV2Guard.Refuse();
                if (context.Response.StatusCode is >= 200 and < 300)
                {
                    if (context.Response.StatusCode == 204 && empty) accept(default);
                    else
                    {
                        if (context.Response.StatusCode is not (200 or 202)) throw HrVehicleV2Guard.Refuse();
                        using var document = JsonDocument.Parse(response.ToArray()); accept(document.RootElement);
                    }
                }
            }
        }
        finally { context.Response.Body = original; }
        response.Position = 0; await response.CopyToAsync(original, context.RequestAborted);
    }

    private static bool Evidence(string? value) => value == HrVehicleV2Guard.Reason || value is { Length: <= 500 } && value.StartsWith(HrVehicleV2Guard.Marker, StringComparison.Ordinal);
    private static string Canonical(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(value, writer);
        return Convert.ToBase64String(stream.ToArray());
        static void Write(JsonElement item, Utf8JsonWriter writer)
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var field in item.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(field.Name); Write(field.Value, writer); }
                writer.WriteEndObject();
            }
            else if (item.ValueKind == JsonValueKind.Array)
            { writer.WriteStartArray(); foreach (var child in item.EnumerateArray()) Write(child, writer); writer.WriteEndArray(); }
            else item.WriteTo(writer);
        }
    }
    private static bool Reason(string? value) => value is HrVehicleV2Guard.Reason or HrVehicleV2Guard.ChangedReason || Evidence(value);
    private static bool Version(string? value) => value is { Length: 12 } && Convert.TryFromBase64String(value, new byte[8], out var length) && length == 8;
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static Guid Id(JsonElement value, string name) => Guid.TryParseExact(Text(value, name), "D", out var id) && id != Guid.Empty ? id : throw HrVehicleV2Guard.Refuse();
    private static bool Fields(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Count() == names.Length && names.All(n => value.EnumerateObject().Count(p => p.Name == n) == 1);
    private sealed class LimitedResponse : MemoryStream
    {
        private void Bound(long end) { if (end > 262144) throw HrVehicleV2Guard.Refuse(); }
        public override void SetLength(long value) { Bound(value); base.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) { Bound(Position + count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Bound(Position + buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Bound(Position + 1); base.WriteByte(value); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
    }
}
