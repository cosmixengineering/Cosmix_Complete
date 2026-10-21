#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyAttendanceCursors.packages.lock.json
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

// Real cursor adapter and ASP.NET Data Protection with ephemeral in-memory keys only.
// No SQL, host, files, account fixtures, role changes or provider calls.
var checks = 0;
void Check(bool condition, string name)
{ if (!condition) throw new Exception(name); checks++; }
void Failure(Action action, int status, string code, string name)
{
    try { action(); throw new Exception(name + ": unexpectedly succeeded"); }
    catch (AuthenticationFailure failure) { Check(failure.StatusCode == status && failure.Code == code, name); }
}
void Invalid(Action action, string name) => Failure(action, 400, "invalid_attendance_cursor", name);
var now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
var clock = new CursorClock(now);
var keys = new EphemeralDataProtectionProvider();
IAttendanceCursorProtector cursor = new AttendanceCursorProtector(keys, clock);
var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
var row = Guid.Parse("22222222-2222-2222-2222-222222222222");
const string purpose = "attendance.corrections.review";
var filter = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("employee=fixture;state=pending")));
var position = new AttendanceCursorPosition(now.AddDays(-1).AddTicks(7), row);
var token = cursor.Protect(actor, purpose, filter, position);
Check(cursor.Unprotect(token, actor, purpose, filter) == position, "round trip retains exact UTC ordering timestamp and ID");
Check(new AttendanceCursorProtector(keys, clock).Unprotect(token, actor, purpose, filter) == position,
    "another adapter instance using the same key ring reads continuation");
Check(cursor.Unprotect(token, actor, purpose, filter.ToLowerInvariant()) == position,
    "equivalent hex casing retains identical filter binding");
Check(!Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)).Contains(row.ToString(), StringComparison.Ordinal),
    "continuation does not expose the possibly denied candidate ID as encoded plaintext");
Invalid(() => cursor.Unprotect(token, Guid.NewGuid(), purpose, filter), "another account cannot reuse continuation");
Invalid(() => cursor.Unprotect(token, actor, "attendance.face.enrollment-attempts", filter), "another feature cannot reuse continuation");
Invalid(() => cursor.Unprotect(token, actor, purpose, new string('A', 64)), "changed filters cannot reuse continuation");
var changed = token.ToCharArray();
changed[changed.Length / 2] = changed[changed.Length / 2] == 'A' ? 'B' : 'A';
Invalid(() => cursor.Unprotect(new string(changed), actor, purpose, filter), "changed ciphertext cannot decode a continuation");
Invalid(() => new AttendanceCursorProtector(new EphemeralDataProtectionProvider(), clock).Unprotect(token, actor, purpose, filter),
    "unavailable original protection keys fail closed");
foreach (var malformed in new string?[] { null, "", " ", "not-a-token", token + "=", token + "\n", new string('A', 4097) })
    Invalid(() => cursor.Unprotect(malformed!, actor, purpose, filter), "malformed or unbounded token rejected");
Invalid(() => cursor.Protect(Guid.Empty, purpose, filter, position), "empty actor cannot issue a cursor");
Invalid(() => cursor.Unprotect(token, Guid.Empty, purpose, filter), "empty actor cannot decode a cursor");
foreach (var badPurpose in new string?[] { null, "", "../other", "HR.review", new string('a', 101) })
    Invalid(() => cursor.Protect(actor, badPurpose!, filter, position), "unbounded or invalid purpose rejected");
foreach (var badFilter in new string?[] { null, "", new string('F', 63), new string('F', 65), new string('Z', 64) })
    Invalid(() => cursor.Protect(actor, purpose, badFilter!, position), "filter requires a complete SHA-256 hex value");
Invalid(() => cursor.Protect(actor, purpose, filter, null!), "missing position rejected");
Invalid(() => cursor.Protect(actor, purpose, filter, position with { Id = Guid.Empty }), "empty row ID rejected");
Invalid(() => cursor.Protect(actor, purpose, filter, position with { CreatedAtUtc = default }), "missing ordering timestamp rejected");
Invalid(() => cursor.Protect(actor, purpose, filter, position with { CreatedAtUtc = now.AddTicks(1) }), "future row cannot issue continuation");
Invalid(() => cursor.Protect(actor, purpose, filter, position with { CreatedAtUtc = now.ToOffset(TimeSpan.FromHours(5)) }),
    "ordering timestamp must be UTC");

clock.Now = now.AddMinutes(30).AddTicks(-1);
Check(cursor.Unprotect(token, actor, purpose, filter) == position, "continuation remains usable just before expiry");
clock.Now = now.AddMinutes(30);
Failure(() => cursor.Unprotect(token, actor, purpose, filter), 409, "attendance_cursor_expired", "exact expiry requires a fresh listing");
Invalid(() => cursor.Unprotect(token, Guid.NewGuid(), purpose, filter), "wrong actor does not learn whether token has expired");
clock.Now = now;

// Independently compose authenticated malformed envelopes, proving validation beyond DP integrity.
var rawProtector = keys.CreateProtector("Cosmix.Attendance.Cursor.v1");
string Envelope(int version = 1, AttendanceCursorPosition? value = null, DateTimeOffset? expiry = null) =>
    rawProtector.Protect(JsonSerializer.Serialize(new
    {
        Version = version, AccountId = actor, Purpose = purpose, FilterFingerprint = filter,
        Position = value ?? position, IssuedAtUtc = now, ExpiresAtUtc = expiry ?? now.AddMinutes(30)
    }));
Invalid(() => cursor.Unprotect(Envelope(version: 2), actor, purpose, filter), "unknown envelope version rejected");
Invalid(() => cursor.Unprotect(Envelope(expiry: now.AddDays(1)), actor, purpose, filter), "authenticated envelope cannot expand technical lifetime");
Invalid(() => cursor.Unprotect(Envelope(value: position with { Id = Guid.Empty }), actor, purpose, filter), "authenticated invalid position rejected");
Invalid(() => cursor.Unprotect(rawProtector.Protect("{}"), actor, purpose, filter), "authenticated missing envelope fields rejected");
Invalid(() => cursor.Unprotect(rawProtector.Protect("not json"), actor, purpose, filter), "authenticated malformed JSON rejected");
Invalid(() => cursor.Unprotect(rawProtector.Protect("{\"Version\":1,\"Unexpected\":true}"), actor, purpose, filter), "unknown envelope fields rejected");
clock.Now = now.AddTicks(-1);
Invalid(() => cursor.Unprotect(token, actor, purpose, filter), "future-issued continuation rejected after a backwards clock change");
clock.Now = now;
try
{
    new AttendanceCursorProtector(new UnavailableCursorKeys(), clock).Protect(actor, purpose, filter, position);
    throw new Exception("unavailable key ring unexpectedly produced a fallback cursor");
}
catch (CryptographicException) { checks++; }
Console.WriteLine($"Attendance cursors: {checks} offline checks passed. Ephemeral keys only; no SQL, host or files.");

sealed class CursorClock(DateTimeOffset now) : TimeProvider
{ public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
sealed class UnavailableCursorKeys : IDataProtectionProvider, IDataProtector
{
    public IDataProtector CreateProtector(string purpose) => this;
    public byte[] Protect(byte[] plaintext) => throw new CryptographicException("Synthetic unavailable key ring.");
    public byte[] Unprotect(byte[] protectedData) => throw new CryptographicException("Synthetic unavailable key ring.");
}
