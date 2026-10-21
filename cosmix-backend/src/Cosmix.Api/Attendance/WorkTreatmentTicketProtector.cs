using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Cosmix.Api.Attendance;

public sealed class WorkTreatmentTicketProtector(IDataProtectionProvider provider, TimeProvider clock) : IWorkTreatmentProposalProtector
{
    private const int MaximumTokenLength = 4096;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions Json = new()
    { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly IDataProtector protector = provider.CreateProtector("attendance.work-treatment.source.v1");

    public string Protect(WorkTreatmentTicket ticket) {
        if (ticket.ExpiresAtUtc <= clock.GetUtcNow()) throw AttendanceOperation.Invalid("invalid_work_source_ticket");
        var envelope = new Envelope(1, ticket.ExpiresAtUtc.Subtract(Lifetime), ticket);
        Validate(envelope);
        var token = protector.Protect(JsonSerializer.Serialize(envelope, Json));
        if (token.Length > MaximumTokenLength) throw new InvalidOperationException("Work source ticket exceeds its output bound.");
        return token;
    }
    public WorkTreatmentTicket Unprotect(string token) {
        Envelope envelope;
        try {
            if (token is null || token.Length is < 1 or > MaximumTokenLength ||
                WebEncoders.Base64UrlEncode(WebEncoders.Base64UrlDecode(token)) != token)
                throw AttendanceOperation.Invalid("invalid_work_source_ticket");
            envelope = JsonSerializer.Deserialize<Envelope>(protector.Unprotect(token), Json)
                ?? throw AttendanceOperation.Invalid("invalid_work_source_ticket");
        } catch (Exception failure) when (failure is CryptographicException or FormatException or JsonException or ArgumentException) {
            throw AttendanceOperation.Invalid("invalid_work_source_ticket");
        }
        Validate(envelope);
        // The workflow enforces expiry for new writes; retained receipt replay still verifies exact session binding.
        return envelope.Ticket;
    }
    private void Validate(Envelope envelope) {
        var t = envelope.Ticket;
        if (envelope.Version != 1 || t is null || t.AccountId == Guid.Empty || t.SessionId == Guid.Empty || t.EmployeeId == Guid.Empty ||
            !Enum.IsDefined(t.Kind) || t.From == default || t.To < t.From || t.To == DateOnly.MaxValue || t.To.DayNumber - t.From.DayNumber > 30 ||
            !WorkTreatmentRules.HashValid(t.Fingerprint) || envelope.IssuedAtUtc == default || envelope.IssuedAtUtc.Offset != TimeSpan.Zero ||
            envelope.IssuedAtUtc > clock.GetUtcNow() || t.ExpiresAtUtc.Offset != TimeSpan.Zero || t.ExpiresAtUtc - envelope.IssuedAtUtc != Lifetime)
            throw AttendanceOperation.Invalid("invalid_work_source_ticket");
    }
    private sealed record Envelope(int Version, DateTimeOffset IssuedAtUtc, WorkTreatmentTicket Ticket);
}
