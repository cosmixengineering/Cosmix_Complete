# Part B24 Source Handoff

13 October 2026. This is a model/migration-source handoff, not full Attendance,
biometric, authenticated HTTP, client or production acceptance.

## Frozen Source Range

Retained baseline: `20261013121357_CompleteHrFoundationPartA` (A23).
New migration: `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims` (B24).
Office22 and A23 files are unchanged. The final snapshot advances to B24.

| Artifact | SHA256 |
| --- | --- |
| B24 migration | `145F45652CEC957D267F103C1F3A8349CBEE6DDF6C37C4C5A405862AB9802C96` |
| B24 designer | `BA2ADD9B72E1D886769F376236028BACE97CBCEAB0C28F2041EB350A2F27F60F` |
| B24 snapshot | `D12E3C2783B08C1021C587137C3B56B1E838B2EC4ECE41323A002279B539D78E` |
| A23-to-B24 idempotent SQL | `A0EEFC4D51E1BF1A0FA4BB970A8DCE8094A336EF6D9219BAD54A599136B525F2` |

The 44 forward operations add only three Attendance tables and ten nullable
columns with their constraints, derived keys, indexes and restrict references:

- Office boundary versions using real Branch identities.
- Independent capture-validation policy versions and exact final raw evidence.
- Raw office branch/roster/boundary (three optional columns).
- Interpreted duty office branch/roster/member/boundary/calendar (five).
- Offline zone encrypted written-claim reference and expiry (two).

No automatic permissions/roles/accounts/policy values, business-row backfill,
table/column deletion or opaque forward SQL is introduced. The downgrade throws
before any destructive operation; no automatic preservation downgrade is provided.

## Verification

Actual EF design services generated the later migration under a forbidden SQL
connection interceptor and exact A23 source/snapshot guards. The two newly
generated files were placed into the existing Infrastructure migration directory
with byte/hash preservation. This did not rewrite any prior migration.

`VerifyBMigration.cs`: 15 checks, exact A23 target-model difference, 44 operations,
snapshot agreement, scope/nullable/no-seed/forward-routing checks, retained A23
hashes, downgrade refusal and zero connection attempts. Actual Dev/Prod DI:
26 checks. Current four-project Release build: zero warnings and zero errors.

The coordinated Part A root reports its existing independent reviewer inspected
all four exact hashes, model/designer agreement, SQL Server idempotent/dynamic
new-column constraints, legacy Zone branches and restrict references without a
blocking finding. This is a delegated review result, not a parent-executed SQL run.

## Ownership

The B model window is frozen and handed back. Any later EF-visible change needs
a later independent migration; never change B24/Office22/A23 after application.
Part A root alone owns its approved current target, backup/restore, clone/original
schema application and target-safe SQL acceptance. B parent performed no SQL writes.
Use the actual inspected baseline, never an old recorded migration count.

B03 controller/schema5 calculation and B06 non-EF verification remain separately
owned while their source handoffs complete. Their active files are not offered for
concurrent taxonomy edits. Broader proposed taxonomy mapping remains a separate
coordinated consumer change; do not replace confirmed OFFICE/site history or
enable office OT through an unapproved mapping.

## Limits

Real CompreFace/model/device deployment, spoof acceptance, biometric destruction/
hold/backup/key authority, approved zone deployment/delay/retention and authenticated
HTTP/SQL acceptance remain separate gates. Legacy local-host account-count guards
must not be weakened or existing accounts deleted to manufacture acceptance.
Source/migration review and build success do not establish complete workflows.

## Later SQL-Owner Application Evidence - 13 October 2026

This supersedes pending schema-application statements, not source-only verification
boundaries. B independently read the SQL owner's sanitized clone/original B24 result
manifests. Both report 24 migrations and preservation of original row identifiers and
source facts; the five accounts and fourteen employees were retained. SQL SHA256
matches the frozen table above and `ProductionAccessed=false` on both targets.
The clone additionally reports a verified idempotent repeat; the original explicitly
reports no repeat. No B-parent SQL/fixture/host/provider writes occurred.

The actual root-owned selected baseline was 12, not the older recorded 22. The root
verified fresh backups and a true isolated restore, preserved records through A23,
then applied B24 to clone and selected original under its own reviewed target guards.
See `docs/HR-Part-A-Continuation-2026-10-13.md` for the SQL-owner record. Do not infer
the source laptop or another target's state from these results.

Real host acceptance remains blocked: the actual legacy fixture verifier expects
exactly three synthetic accounts and its profile pins an old pipe, while five accounts
are intentionally preserved. Deleting extras, reseeding, weakening the guard or
silently substituting a different target is not an accepted test setup.
