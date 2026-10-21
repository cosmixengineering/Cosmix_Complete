# Private employee objects and malware scanning

These adapters provide local encrypted storage and real ClamAV protocol support. They do not implement
HR workflows, file-signature validation, authorization, upload endpoints or download availability.
Infrastructure registers these adapters for the Employee Hiring APIs. Private storage must be configured
outside Git before resolving those workflows; the scanner remains disabled until explicitly configured.

## Contracts and integration

Application contracts are in `Cosmix.Application.Common.Files`. `FileScanResult.State` uses the existing
`Cosmix.Domain.HR.EmployeeDocuments.DocumentScanStatus` enum. Only `Clean` is scan success;
`Pending`, `Rejected` and `Failed` must never enable downloads. The HR caller must additionally enforce
file-type signature validation, employee scope and the domain's document availability predicates.

`EncryptedPrivateFileStore(PrivateFileStorageOptions)` implements `IPrivateFileStore`.
`ClamAvEmployeeFileScanner(ClamAvScannerOptions)` implements `IEmployeeFileScanner`.
Both can be singletons. Options are captured at construction; restart after configuration changes.
The adapters make no network calls during construction and have no loggers.

Example composition-root registration (requires configuration binder and DI namespaces):

```csharp
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;

services.AddSingleton<IPrivateFileStore>(_ =>
{
    try
    {
        return new EncryptedPrivateFileStore(
            configuration.GetSection(PrivateFileStorageOptions.SectionName)
                .Get<PrivateFileStorageOptions>() ?? new());
    }
    catch (Exception)
    {
        throw new AuthenticationFailure("private_file_storage_unavailable", 503);
    }
});
services.AddSingleton<IEmployeeFileScanner>(_ =>
{
    try
    {
        return new ClamAvEmployeeFileScanner(
            configuration.GetSection(ClamAvScannerOptions.SectionName)
                .Get<ClamAvScannerOptions>() ?? new());
    }
    catch (Exception)
    {
        return new ClamAvEmployeeFileScanner(new()); // Disabled: every scan returns Failed.
    }
});
```

## Storage configuration

Section: `PrivateFiles`.

| Option | Requirement/default |
|---|---|
| `Directory` | Existing absolute local private object directory, outside repositories/application binaries/wwwroot |
| `KeyFile` | Existing raw 32-byte AES key file, outside the object directory, separately protected |
| `KeyReference` | Stable non-secret label, 1-80 ASCII letters/digits/dot/underscore/hyphen |
| `MaxSizeBytes` | 10485760 (10 MiB); valid range 1-67108864 |
| `DecryptionKeyFiles` | Optional map of at most 16 prior key references to private 32-byte key files |

Keep the HR API bound at 10 MiB and configure scanner/storage bounds consistently. The adapter's 64 MiB
ceiling is a defensive implementation limit, not authorization to increase the approved API limit.
Provision keys using a cryptographically secure source outside the application. No key is generated
automatically, stored in configuration, or checked into Git. Back up keys and encrypted objects separately.
Windows ACLs may grant access only to the running service identity, SYSTEM and Administrators, with an
owner from that set. Provision inheritable private ACLs on directories. Unix permissions must grant no
group/other access. Parent directory administration must remain trusted. Public/null ACLs, traversal,
UNC/device paths, alternate streams, symlinks and junctions are rejected. The existing bootstrap path
checker is reused without changes, with additional traversal/UNC/null-DACL checks.

Writes create unique 36-character object keys and 32-character object versions using `CreateNew`.
The versioned binary envelope contains a random 12-byte nonce, 16-byte GCM tag and ciphertext.
AES-256-GCM associated data authenticates the purpose and every reference field. Purpose is a stable,
server-selected, non-whitespace ASCII token up to 200 characters; include resource identity when
cross-employee or cross-document substitution must fail. Documents and snapshots need different purposes.
`Sha256` is the uppercase plaintext SHA-256 digest; `SizeBytes` is the plaintext byte count.
Objects are flushed, read back and marked read-only before a successful reference is returned.
There is no overwrite/delete operation. Read-only attributes are not OS WORM protection against admins.

Compact default `System.Text.Json` serialization of each generated reference is checked to fit 400 bytes
(all fields are ASCII). Persist this JSON in the internal document storage field. Do not indent it or
add metadata to it. Keep references, purpose tokens, key paths and encryption metadata out of client DTOs
and logs. References are trusted server metadata, never client-supplied filesystem paths or public URLs.
Configuration, I/O, malformed-reference, missing-key and integrity failures become
`AuthenticationFailure("private_file_storage_unavailable", 503)` without path/input/inner-exception details.
Caller cancellation remains cancellation. The caller owns and must appropriately release returned plaintext.

For rotation, provision a new key file and new `KeyReference`; move the old reference/file pair into
`DecryptionKeyFiles`. Do not reuse a key reference for different key bytes or duplicate the active label
in the retained map. Existing objects keep their original authenticated reference and use the old key
on reads. New writes use only the active key. Keep retained keys until dependent objects and backups
have passed the approved retention period; this adapter does not re-encrypt existing objects.

## Scanner configuration

Section: `EmployeeFileScanner`.

| Option | Requirement/default |
|---|---|
| `Enabled` | false; set true only after deployment acceptance |
| `Host` | Empty by default; explicit literal IP such as 127.0.0.1; no DNS resolution |
| `Port` | 3310; valid range 1-65535 |
| `AllowPrivateNetwork` | false; explicit true permits RFC1918 IPv4 or IPv6 ULA in addition to loopback |
| `TimeoutSeconds` | 15; valid range 1-120; one total deadline for version lookup, upload and response |
| `MaxSizeBytes` | 10485760; valid range 1-67108864 |
| `MaxResponseBytes` | 4096; valid range 128-16384 per command |

Public, unspecified, link-local and multicast destinations are rejected even with private networking
enabled. IPv4-mapped IPv6 addresses are validated by their underlying IPv4 address. Disabled, invalid,
unavailable, timeout, protocol error and unknown results return `Failed`. Only caller cancellation throws.

The adapter follows the [official ClamD protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html):
`zVERSION` obtains bounded engine/database version evidence, then `zINSTREAM` uses a separate connection
with big-endian 32-bit chunk lengths and a zero-length final chunk. Both replies must be exactly one
NUL-terminated record followed by EOF. Only exact `stream: OK` is Clean; a nonempty `stream: ... FOUND`
is Rejected. Truncated, oversized, malformed UTF-8, mixed/multiple or unexpected replies fail closed.
Reported versions must contain engine and database identifiers and fit the domain's 150-character limit.
If VERSION is disabled or unavailable, no file bytes are sent. Detection names/responses are not logged.

Clamd TCP is neither authenticated nor encrypted. Prefer loopback; an explicitly approved private
network must also be isolated and protected by the deployment. Private addressing alone is not transport
security. Enable VERSION, keep signatures current, and set StreamMaxLength/MaxFileSize/MaxScanSize
consistently with accepted content and archive policy. Enable AlertExceedsMax for the limits it covers
and appropriate encrypted archive/document alerts; review all format-specific skip limits against the
[official clamd configuration](https://github.com/Cisco-Talos/clamav/blob/main/etc/clamd.conf.sample).
The adapter cannot inspect server-side scan coverage from an OK response. Real daemon
configuration and malware-detection acceptance remain deployment work, separate from protocol tests.

## Operational reconciliation

Filesystem writes do not join the database transaction. Completed encrypted objects remain immutable
if a later transaction fails or its commit outcome is uncertain. Interrupted writes may also leave
unreferenced/incomplete encrypted objects; failed writes attempt to mark their objects read-only.
There is no automatic deletion or cleanup job. A privileged operator must reconcile opaque filenames
against authoritative database references, transaction outcomes, backups and approved retention/legal
requirements before any deletion. An object being unreferenced at one instant is insufficient evidence
to delete it. Do not delete while uploads or uncertain transactions may still reference the object.

## Verification scope

Temporary tests live outside the repository and use generated synthetic bytes and a loopback fake TCP
server. They cover encryption, tampering, purpose/reference substitution, path/ACL rejection, retained-key
rotation, bounds, cancellation, protocol framing, chunking, EOF requirements, errors and unavailable
scanners. They do not prove production ClamAV detection/configuration, Linux ACL behavior, HR authorization,
HTTP limits or database transaction integration. No real employee content or external scanner is used.
