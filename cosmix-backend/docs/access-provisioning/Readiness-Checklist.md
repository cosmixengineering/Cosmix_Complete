# Local acceptance readiness — 9 October 2026

The user explicitly approved fresh isolated `Cosmix_Testing` setup/migration, synthetic
fixtures, private keys/files and actual acceptance. The approved root is
`C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`; it now contains retained
private evidence. Do not recreate, reset, replace keys or blindly rerun fixture/provisioning
scripts. See the [isolated acceptance plan](Isolated-Acceptance-Plan.md) and
[verification record](Verification.md) for the exact scope and result boundaries.

## Current execution checkpoint

| Item | Executed result |
|---|---|
| Target and schema | Guarded local SQL inspection established that `Cosmix_Testing` was absent; approved setup created it with exactly three synthetic accounts and applied all 13 migrations. Production and the original development database were not upgraded. |
| Backup and storage | Verified backup retained before attendance fixtures; new private settings/keys and an owned `https://localhost:7249` host. No existing environment's keys/accounts were substituted. |
| Access authority and authentication | **65 checks passed** through real password/OTP, persisted web sessions and CSRF. Fixed Admin configured first scheduling grants; only SuperAdmin modified protected administrator access. Explicit All/Site/Self grants and stale versions were verified. |
| Encrypted storage | **13 checks passed** across write and fresh-process read: round-trip, wrong purpose/key/version/digest, tamper and missing-key failures. Only dedicated synthetic objects were used. |
| SQL rollback | **9 checks passed** for bounded scheduling fault injection using a real current Admin/MFA session and independent SQL reads. Business, audit and receipt changes rolled back; **two encrypted orphan objects** remain privately indexed. |
| Main scheduling | **62 HTTP checks passed**, then **83 independent SQL/private-evidence checks** including 12 decrypted audit objects. |
| Main restart replay | **26 additional assertions** across 13 operations passed after an owned host restart and fresh login; main cumulative count **88**. |
| Transfer boundaries | **9 supplemental grant checks + 42 run checks + 16 replay assertions passed**; boundary cumulative **58**, including HR transfer at +20 days, overnight rejection, exact-midnight adjacency and holiday pinning. Replay used a fresh login on the same API host, not a host restart. |
| Revocation | Genuine SuperAdmin revoked only the original run Admin Site-A roster grant; old Admin session returned **401**. Fresh-MFA Admin received **403** on nine scoped receipt replays; main cumulative **97**. Other grants and immutable history remain retained. |
| Competing metadata writes | **9 checks passed** with distinct idempotency keys: exactly one HTTP 200 and one HTTP 409; GET confirmed the winner and immutable schedules stayed unchanged. |
| SQL constraints | **13 migrations applied**, all foreign keys enabled/trusted and DBCC reported no constraint violations on isolated `Cosmix_Testing`. |
| Final build and process | Solution Release build passed with zero warnings/errors; owned acceptance host PID 2876 is stopped. Private evidence and intentionally retained fixtures/grants remain. |

These are separate assertion counts and bounded evidence scopes, not an aggregate claim that
the whole backend or every deployment/concurrency case is complete. Permission grants were
explicit web API writes, never direct SQL grants. No real employee/account, production provider,
Git operation, automatic reset/restore or unowned host was part of this acceptance.

## Remaining execution

Retain immutable fixtures, encrypted objects, orphan index, manifests and verified backup.
The owned acceptance process is stopped; retain the recorded grants/rows and private evidence.
No automatic cleanup or restore is implied. The bounded competing-write
result does not imply that every concurrent scheduling case has been exercised.

`LocalAcceptanceRevocation.ps1` completed the original run's exact Site-A grant revocation
using current API versions. Do not rerun it or revoke unrelated grants as cleanup; the original
grant is already revoked and the other synthetic grants intentionally remain.

Real mobile proof, carrier delivery, scanner detection, production throughput and unexecuted
concurrency scenarios require their own evidence. The existing LocalTesting adapter provides
random browser-bound web OTPs without sending to a carrier. Its viewer supports `WebLogin`
only. Scanner acceptance still needs harmless clean/standard antimalware samples and actual
unavailable/timeout/malformed-response cases. Scheduling private evidence does not establish
employee-document malware scanning. Provider choice, production policies and real owner
provisioning remain management decisions.

## Metadata inventory remains read-only

```powershell
.\scripts\Test-AcceptanceReadiness.ps1
.\scripts\Test-AcceptanceReadiness.ps1 -AsJson
```

The inventory reads fixed file/configuration metadata and parses known settings in memory.
It never starts a tool/server, connects to SQL/network, reads key/account contents, outputs
private values or writes files. Its `BehavioralAcceptanceEstablished: false` describes the
inventory's evidence boundary, not the separately executed acceptance above. It is not an
executable approval gate and does not fail its process for pending acceptance.

The original offline run found SDK/SqlLocalDB/Release-file metadata and a redirected
`LOCALAPPDATA`; the old documented `Cosmix\LocalTesting` settings/accounts were absent.
That inventory did not prove database absence or provider readiness. The later approved
target inspection and fresh setup established the current isolated state independently.
PowerShell parsing and the offline fixed-output contract passed without SQL/private writes.
