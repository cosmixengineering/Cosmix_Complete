using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Attendance.FaceVerification;

public enum EnrollmentFailure
{
    PolicyNotConfirmed, ProviderNotConfigured, TemplateStorageNotConfigured, TemplateStorageUnavailable, EnrollmentChanged,
    EmployeeIneligible, DeviceIneligible, ProviderRejected, ProviderInvalidResult, ProviderUnavailable,
    CallerCancelled, FinalizationDenied, FinalizationConflict, InternalFailure
}
public sealed record EnrollmentFailureDescription(string Stage, string Code, int Status);
public sealed record EnrollmentStart(Guid ReceiptId, Guid ActorAccountId, string ActorRoleCode, Guid EmployeeId,
    Guid RequestedDeviceId, string RequestSha256, string FinalizationProofSha256, string Provider, string ProviderVersion,
    DateTimeOffset StartedAtUtc, string CorrelationId, string ActorSessionReference, string IpAddress,
    IReadOnlyList<AccessResource> Resources);
// FinalizationProof is process-owned, returned only by the original creator and never persisted/logged or sent through HTTP.
public sealed record EnrollmentHandle(Guid ReceiptId, string FinalizationProof);
public sealed record EnrollmentTerminal(Guid StartReceiptId, Guid EmployeeId, Guid RequestedDeviceId,
    string Provider, string ProviderVersion, DateTimeOffset CompletedAtUtc, string Stage, string Code, int Status,
    FaceEnrollmentResponse? Enrollment, FaceEnrollmentResponse? PriorEnrollment = null);
public sealed record EnrollmentExecution(EnrollmentStart Start, EnrollmentHandle? Handle, EnrollmentTerminal? Terminal, bool Created);

public interface IFaceEnrollmentJournalRepository
{
    void RequireTransaction();
    Task<IdempotencyReceipt?> FindAsync(string actor, string operation, string key, CancellationToken ct);
    Task<IdempotencyReceipt?> StartAsync(Guid id, CancellationToken ct);
    void Add(IdempotencyReceipt receipt);
    void Add(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken ct);
    Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

/// <summary>Durable enrollment lifecycle metadata only. Terminal arbitration shares the auth transaction lock.
/// No template, capture, token, provider exception text or client-provided authority is journalled.</summary>
public sealed class FaceEnrollmentJournal(IFaceEnrollmentJournalRepository repository, IPrivateFileStore files, TimeProvider clock)
{
    public const string StartOperation = "attendance.face.enrollment.start.v1";
    public const string TerminalOperation = "attendance.face.enrollment.terminal.v1";
    private const string StartResult = "face-enrollment-start-v1", TerminalResult = "face-enrollment-terminal-v1";

    public async Task<EnrollmentExecution> BeginAsync(AttendanceActor actor, Guid employeeId, Guid requestedDevice,
        string key, string fingerprint, FaceProviderCapability capability, IReadOnlyList<AccessResource> resources,
        AuthRequestContext context, CancellationToken ct)
    {
        repository.RequireTransaction();
        var existing = await repository.FindAsync(actor.Identity.AccountId.ToString(), StartOperation, key, ct);
        if (existing is not null)
        {
            if (existing.RequestSha256 != fingerprint) throw AttendanceOperation.Conflict("idempotency_key_reused");
            if (existing.ExpiresAt <= clock.GetUtcNow()) throw AttendanceOperation.Conflict("idempotency_key_expired");
            var stored = await ReadStart(existing, ct);
            if (stored.ActorAccountId != actor.Identity.AccountId || stored.EmployeeId != employeeId || stored.RequestedDeviceId != requestedDevice)
                throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
            return new(stored, null, await Terminal(stored, ct), false);
        }
        var proof = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        // Existing attendance receipt lifetime is 24 hours. This is retry expiry, never audit/biometric deletion policy.
        var receipt = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), StartOperation, key, fingerprint, now, now.AddHours(24));
        var start = new EnrollmentStart(receipt.Id, actor.Identity.AccountId, actor.RoleCode, employeeId, requestedDevice,
            fingerprint, FaceService.Digest(proof), SafeIdentity(capability.Provider), SafeIdentity(capability.Version), now,
            context.CorrelationId, actor.Identity.SessionId.ToString(), context.IpAddress, resources.ToArray());
        var reference = await Write(start, StartPurpose(receipt.Id), ct);
        receipt.Complete(StartResult, reference, clock.GetUtcNow());
        repository.Add(receipt);
        repository.Add(Audit(start, "started", now, reference));
        // Available to same-transaction preflight failure finalization; the outer authorization transaction still owns commit.
        await repository.SaveChangesAsync(ct);
        return new(start, new(receipt.Id, proof), null, true);
    }

    public async Task<EnrollmentTerminal?> OutcomeAsync(EnrollmentHandle handle, CancellationToken ct)
    { repository.RequireTransaction(); return await Terminal(await BoundStart(handle, ct), ct); }

    public async Task<EnrollmentStart> ReadHistoryStartAsync(AuditEvent indexed, Guid employeeId, CancellationToken ct)
    {
        repository.RequireTransaction();
        if (indexed.EventType != "attendance.face.enrollment.started" || indexed.TargetType != "FaceEnrollmentAttempt" ||
            indexed.EmployeeScopeId != employeeId || !Guid.TryParse(indexed.TargetId, out var id))
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        var row = await repository.StartAsync(id, ct) ?? throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        var start = await ReadStart(row, ct);
        if (start.EmployeeId != employeeId || start.StartedAtUtc != indexed.OccurredAt ||
            start.ActorAccountId.ToString() != indexed.ActorUserId || start.ActorRoleCode != indexed.ActorRoleCode ||
            start.CorrelationId != indexed.CorrelationId || start.RequestedDeviceId.ToString() != indexed.DeviceReference ||
            row.ProtectedResultReference != indexed.ProtectedChangesReference)
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        return start;
    }

    public async Task<EnrollmentTerminal?> ReadHistoryOutcomeAsync(EnrollmentStart start, CancellationToken ct)
    {
        repository.RequireTransaction();
        var terminal = await Terminal(start, ct);
        if (terminal is null) return null;
        var known = terminal.Status == 200
            ? terminal.Stage == "succeeded" && terminal.Code == "face_enrollment_verified" &&
              terminal.Enrollment is { IsActive: true } enrollment && enrollment.EmployeeId == start.EmployeeId &&
              enrollment.Provider == start.Provider && enrollment.ProviderVersion == start.ProviderVersion
            : Enum.GetValues<EnrollmentFailure>().Select(Describe).Any(x =>
                x.Stage == terminal.Stage && x.Code == terminal.Code && x.Status == terminal.Status);
        if (!known) throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        return terminal;
    }

    public async Task<EnrollmentTerminal> CompleteSuccessAsync(EnrollmentHandle handle, FaceEnrollmentResponse response,
        FaceEnrollmentResponse? prior, CancellationToken ct)
    {
        repository.RequireTransaction();
        var start = await BoundStart(handle, ct);
        var existing = await Terminal(start, ct);
        if (existing is not null) return existing;
        if (clock.GetUtcNow() >= start.StartedAtUtc.AddHours(24)) throw AttendanceOperation.Conflict("idempotency_key_expired");
        if (response.EmployeeId != start.EmployeeId || response.Provider != start.Provider || response.ProviderVersion != start.ProviderVersion ||
            response.Id == Guid.Empty || !response.IsActive) throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        return await StageTerminal(start, new(start.ReceiptId, start.EmployeeId, start.RequestedDeviceId, start.Provider,
            start.ProviderVersion, clock.GetUtcNow(), "succeeded", "face_enrollment_verified", 200, response, prior), ct);
    }

    public async Task<EnrollmentTerminal> CompleteFailureInTransactionAsync(EnrollmentHandle handle, EnrollmentFailure failure, CancellationToken ct)
    {
        repository.RequireTransaction();
        var start = await BoundStart(handle, ct);
        var existing = await Terminal(start, ct);
        if (existing is not null) return existing;
        var description = Describe(failure);
        return await StageTerminal(start, new(start.ReceiptId, start.EmployeeId, start.RequestedDeviceId, start.Provider,
            start.ProviderVersion, clock.GetUtcNow(), description.Stage, description.Code, description.Status, null), ct);
    }

    // Called after the business transaction has unwound. The proof comes only from our authorized persisted start.
    // This path does not accept an actor/employee/provider/success payload and cannot mutate business rows.
    public Task<EnrollmentTerminal> CompleteFailureAsync(EnrollmentHandle handle, EnrollmentFailure failure, CancellationToken ct) =>
        repository.InAuditTransactionAsync(async token =>
        {
            var terminal = await CompleteFailureInTransactionAsync(handle, failure, token);
            await repository.SaveChangesAsync(token);
            return terminal;
        }, ct);

    private async Task<EnrollmentStart> BoundStart(EnrollmentHandle handle, CancellationToken ct)
    {
        var row = await repository.StartAsync(handle.ReceiptId, ct) ?? throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        var start = await ReadStart(row, ct);
        if (string.IsNullOrWhiteSpace(handle.FinalizationProof) ||
            !FaceService.SecureDigestEquals(FaceService.Digest(handle.FinalizationProof), start.FinalizationProofSha256))
            throw AttendanceOperation.Denied();
        return start;
    }
    private async Task<EnrollmentStart> ReadStart(IdempotencyReceipt row, CancellationToken ct)
    {
        if (row.Operation != StartOperation || row.ResultCode != StartResult || row.CompletedAt is null || row.ProtectedResultReference is null)
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        var start = await Read<EnrollmentStart>(row.ProtectedResultReference, StartPurpose(row.Id), ct);
        if (start.ReceiptId != row.Id || start.ActorAccountId.ToString() != row.ActorUserId || start.RequestSha256 != row.RequestSha256 ||
            start.StartedAtUtc != row.CreatedAt || start.EmployeeId == Guid.Empty || start.RequestedDeviceId == Guid.Empty ||
            start.Resources is not { Count: > 0 } || start.Resources.Any(x => x is null || x.EmployeeId != start.EmployeeId) ||
            start.FinalizationProofSha256 is not { Length: 64 } ||
            !FaceReadiness.ValidIdentity(start.Provider) || !FaceReadiness.ValidIdentity(start.ProviderVersion))
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        return start;
    }
    private async Task<EnrollmentTerminal?> Terminal(EnrollmentStart start, CancellationToken ct)
    {
        var row = await repository.FindAsync(start.ActorAccountId.ToString(), TerminalOperation, start.ReceiptId.ToString("N"), ct);
        if (row is null) return null;
        if (row.ResultCode != TerminalResult || row.CompletedAt is null || row.RequestSha256 != start.RequestSha256 || row.ProtectedResultReference is null)
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        var terminal = await Read<EnrollmentTerminal>(row.ProtectedResultReference, TerminalPurpose(start.ReceiptId), ct);
        if (terminal.StartReceiptId != start.ReceiptId || terminal.EmployeeId != start.EmployeeId || terminal.RequestedDeviceId != start.RequestedDeviceId ||
            terminal.Provider != start.Provider || terminal.ProviderVersion != start.ProviderVersion || terminal.CompletedAtUtc < start.StartedAtUtc ||
            terminal.CompletedAtUtc != row.CreatedAt || terminal.Status == 200 != (terminal.Enrollment is not null))
            throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        return terminal;
    }
    private async Task<EnrollmentTerminal> StageTerminal(EnrollmentStart start, EnrollmentTerminal terminal, CancellationToken ct)
    {
        var reference = await Write(terminal, TerminalPurpose(start.ReceiptId), ct);
        var row = new IdempotencyReceipt(start.ActorAccountId.ToString(), TerminalOperation, start.ReceiptId.ToString("N"),
            start.RequestSha256, terminal.CompletedAtUtc, terminal.CompletedAtUtc.AddHours(24));
        row.Complete(TerminalResult, reference, clock.GetUtcNow());
        repository.Add(row); repository.Add(Audit(start, terminal.Stage, terminal.CompletedAtUtc, reference));
        return terminal;
    }
    private async Task<string> Write<T>(T value, string purpose, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), purpose, ct));
    private async Task<T> Read<T>(string reference, string purpose, CancellationToken ct)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<PrivateFileReference>(reference) ?? throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(parsed, purpose, ct)) ?? throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable");
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("face_enrollment_journal_unavailable"); }
    }
    private static AuditEvent Audit(EnrollmentStart start, string stage, DateTimeOffset time, string reference) =>
        new("attendance.face.enrollment." + stage, start.ActorAccountId.ToString(), start.ActorRoleCode, "FaceEnrollmentAttempt",
            start.ReceiptId.ToString(), time, start.CorrelationId, start.EmployeeId, reference, start.IpAddress, start.RequestedDeviceId.ToString());
    private static string SafeIdentity(string value) => FaceReadiness.ValidIdentity(value) ? value : "unconfigured";
    private static string StartPurpose(Guid id) => $"attendance:face:enrollment:{id:N}:start";
    private static string TerminalPurpose(Guid id) => $"attendance:face:enrollment:{id:N}:terminal";
    public static EnrollmentFailureDescription Describe(EnrollmentFailure failure) => failure switch
    {
        EnrollmentFailure.PolicyNotConfirmed => new("provider_not_run", "face_policy_not_confirmed", 503),
        EnrollmentFailure.ProviderNotConfigured => new("provider_not_run", "face_provider_not_configured", 503),
        EnrollmentFailure.TemplateStorageNotConfigured => new("provider_not_run", "face_template_storage_unavailable", 503),
        EnrollmentFailure.TemplateStorageUnavailable => new("storage_unavailable", "face_template_storage_unavailable", 503),
        EnrollmentFailure.EnrollmentChanged => new("precondition_rejected", "face_enrollment_version_conflict", 409),
        EnrollmentFailure.EmployeeIneligible => new("precondition_rejected", "face_employee_ineligible", 409),
        EnrollmentFailure.DeviceIneligible => new("precondition_rejected", "face_device_ineligible", 409),
        EnrollmentFailure.ProviderRejected => new("provider_rejected", "face_enrollment_not_verified", 409),
        EnrollmentFailure.ProviderInvalidResult => new("provider_result_rejected", "face_enrollment_not_verified", 409),
        EnrollmentFailure.ProviderUnavailable => new("provider_unavailable", "face_provider_unavailable", 503),
        EnrollmentFailure.CallerCancelled => new("request_cancelled", "face_enrollment_cancelled", 503),
        EnrollmentFailure.FinalizationDenied => new("finalization_denied", "access_denied", 403),
        EnrollmentFailure.FinalizationConflict => new("finalization_conflict", "face_enrollment_finalization_conflict", 409),
        _ => new("internal_failure", "face_enrollment_unavailable", 503)
    };
}
