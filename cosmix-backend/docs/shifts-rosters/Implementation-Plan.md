# Shifts and Individual Rosters — local implementation

The user approved the in-chat eight-step plan on 8 October 2026: inspect retained local code, deliver shift APIs/immutable schedules/individual assignment lifecycle, effective roster and My Shift, verify overnight site consistency, preserve history and locked periods, enforce scoped authority/concurrency/audit/retries, then document actual verification and remaining work.

Constraints: work in the current Desktop checkout; no network Git/GitHub, branch/worktree creation, merges, commits, PRs, migration generation/application, model snapshot or DbContext edits. Preserve the completed uncommitted geofence feature. Policy configuration, calculations, group precedence, temporary changes, face verification and punches remain separate.

Ruling: reuse only relevant feature files from the already-local reviewed checkpoint `82b656763bc81ae8200af6811ee2ad1178e17b9d` (base `70adaf7`) as source material. Apply source changes without a Git merge or branch switch. Reconcile the three shared registration/catalog files by additive edits. Historical handoff verification is not current evidence; rerun local checks and fix defects.

- [x] Inspect SRS, current baseline and retained feature implementation.
- [x] Add feature source files and reconcile registrations while preserving geofences.
- [x] Reproduce and fix overnight next-day HR site-transfer resolution, including exclusive midnight/rest-day cases.
- [x] Verify shift validation, immutable versions, assignment replacement/ending/overlaps, eligibility boundaries, locks, stale writes, scoped authority, replay and self-scope using standalone harnesses (no test project).
- [x] Run Release build, real EF model/DI checks, and local OpenAPI/anonymous HTTP checks for scheduling and geofences.
- [x] Independent review, focused fixes and new verification; publish current feature documentation and completion/remaining-work summary.

Review focus: later HR site changes affecting an existing overnight duty; alternate team/site scope across dated assignments; same-day/locked history rewriting; permission revoked before retry; shared registration loss.

Operational boundary: new scheduling permission definitions do not grant access automatically. Existing immutable system-role/bootstrap rules are preserved. Successful authenticated deployment needs an explicitly approved provisioning path; no grant-all or authorization bypass is introduced for verification.

Completed verification: zero-warning/error Release build; 41 schedule/resolver, 111 operation, 140 service, 52 EF/DI and 34 scheduling HTTP checks; 14 preserved-geofence HTTP checks. Independent final review found no actionable defect. See [current evidence and limits](../Shifts-Rosters-Verification.md).

Additional rulings from review: effective reads authorize all actual occupied dates inside their existing transaction before returning data; end/replace authorize the original assignment history and future timeline because that history is returned. The exclusive boundary after an occupied DateOnly.MaxValue is represented by null rather than overflowing. Real SQL and encrypted disk replay remain acceptance work; no database or outside-repository private-storage fixtures were created.
