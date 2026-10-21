# Initial HR and attendance models

Scope authorized by the project manager: continue the existing four-project backend on
develop and create its first HR and Attendance model classes. Source: SRS v1.2 sections
5, 6, 7 and 14. This is a model and persistence increment, not completed business APIs.

## Design

- Keep Domain free of EF and use feature-local entities with explicit constructors.
- HR: employee draft profile, addresses/emergency contact, controlled organization data,
  effective assignments, employment status history and private document metadata.
- Attendance: sites/geofence versions, shifts/versions, employee shift assignments and
  immutable raw punch evidence. Employee identity remains owned by HR.
- Use UTC DateTimeOffset for instants and DateOnly/TimeOnly for business dates/local times.
  Assignment intervals are start-inclusive and end-exclusive; null end means open-ended.
  Immutable shift/geofence versions have effective start dates; the latest applicable start
  selects a version. Version retirement is future workflow work.
- Incomplete employee drafts are valid. Activation, approval routing, authorization,
  encryption/key management and validated file/face provider workflows remain separate work.
- SQL Server generates employee numbers from a sequence. Foreign keys restrict deletion;
  raw punches also reject tracked update/delete through every SaveChanges overload.
- SQL mapping includes lengths, decimal precision, indexes, rowversion on editable records,
  enum/date constraints and no seeded business data. No guessed policy values or pay formulas.
- Generate one new migration without altering InitialFoundation or applying to an existing
  database. There is one context/model snapshot; this task owns those shared files.

## Execution

- [x] Add HR entities and feature mappings.
- [x] Add Attendance entities and feature mappings.
- [x] Integrate DbSets, save guards and sequence; generate and inspect migration SQL.
- [x] Build Release, check migration/model consistency, and run Development API smoke checks.
- [x] Review changes and update implementation/setup documentation.

## Verification and boundaries

AGENTS.md explicitly says test projects were removed at the PM's request. Do not recreate
them. Use build/tooling and temporary checks outside the solution; report limitations.
No endpoints, generic repositories, test projects, provider stubs, production data, package
upgrades, remote push or business database changes are part of this increment.

Review nullable drafts, server number generation, restrictive relationships, cross-midnight
shift representation, valid date ranges, optional checkout face evidence and punch immutability.
Uniqueness alone is not a complete idempotency implementation. Overlap/hierarchy validation
needs transactional use cases. Raw punch evidence can record failed verification; a stored
event must never be interpreted as accepted attendance without a separate validated workflow.
