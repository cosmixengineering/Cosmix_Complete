# C01 Advanced Attendance Policy Handoff

This slice adds typed family publication and resolution using the existing
Policy, PolicyVersion, PolicyAssignment and PolicyAssignmentEnd tables. It uses
PolicyCategory.EmployeeException (5), the exact controlled
AdvancedAttendanceRulesCodec.Code(family), and attendance.advanced.v1.

## Parent Integration

- Frozen consumer: IAdvancedAttendancePolicyReader.ReadAsync(employeeId, family,
  businessDate, ct). Successful state is **configured**, aligned with C04.
- Extension: IAdvancedPolicyPublicationRepository inherits the frozen
  IAdvancedAttendancePolicyRepository. Its own new members are DatedSourceAsync,
  StaffCategoryCodesExistAsync, family-scoped HasTargetOverlapAsync and
  Add(PolicyAssignmentEnd). The proposal was announced before wiring.
- DatedSourceAsync supplies the actual employee membership's immutable master
  category code through AdvancedPolicyStaffCategorySource, without extending
  the ordinary membership DTO or changing the frozen contracts.
- Application entrypoint: AddAdvancedAttendancePoliciesApplication().
- Infrastructure entrypoint: AddAdvancedAttendancePoliciesInfrastructure(). Both
  repository interfaces resolve to one scoped AdvancedPolicyRepository.
- Parent owns actual root DI/catalog wiring. Existing Configure, Publish and
  ApplicabilityRead permissions are reused. No permission grant is created.

## Exact Source Hooks

| File and line | Contract or implementation hook |
| --- | --- |
| AdvancedPolicyReader.cs:9 | Frozen ReadAsync(employeeId, family, date, ct) consumer. |
| AdvancedPolicyReader.cs:19 | Pure Resolve with historical membership/category/provenance source. |
| AdvancedPolicyReader.cs:69 | Rechecks actual direct authority and approval fields. |
| AdvancedPolicyRegistration.cs:12 | IAdvancedPolicyPublicationRepository extension; frozen contracts unchanged. |
| AdvancedPolicyRegistration.cs:23 | AddAdvancedAttendancePoliciesApplication; scoped reader and service. |
| AdvancedPolicyService.cs:37 | Direct publication; absent versus existing revision/rowversion guards. |
| AdvancedPolicyService.cs:70 | Future explicit assignment inside the selected version period. |
| AdvancedPolicyService.cs:83 | Append-only exclusive end. |
| AdvancedPolicyService.cs:97 | Atomic end and replacement of the same controlled target. |
| AdvancedPolicyRepository.cs:48 | FamilyQuery adds requested controlled code before bounds. |
| AdvancedPolicyRepository.cs:54 | Employee/date membership scope before Take(3). |
| AdvancedPolicyRepository.cs:59 | Actual dated membership-to-category Code join before Take(3). |
| AdvancedPolicyRepository.cs:67 | Employee/date/family applicability before Take(129). |
| AdvancedPolicyRepository.cs:100 | Family-scoped target-overlap predicate, excluding only the replaced assignment. |
| AdvancedPolicyRepository.cs:126 | Actual current transaction must have Serializable isolation for mutations/overlap guards. |
| AdvancedPolicyInfrastructureRegistration.cs:9 | AddAdvancedAttendancePoliciesInfrastructure; same scoped instance for both repository interfaces. |
| AdvancedAttendancePoliciesController.cs:16 | Eight authenticated no-store MVC actions under api/v1/hr/advanced-attendance-policies. |

Paths for these basenames are listed below. The ordinary policy resolver,
category1 repository, root source producers and taxonomy sources have no C01
implementation edits. The private-reference fields participate only in source
hashes and the existing protected operation audit, not public source details.

## Behavior

Direct publication requires current web MFA and fixed system-role authority on
the same matching All-scope Publish grant, through AttendanceOperation. An absent
family requires ExpectedRevision 0 and explicit ExpectedVersion null; existing
families require matching revision and rowversion. Dates must be future in
Pakistan and unlocked. Category codes must exist as active configured masters.
There is no second reviewer. Rules and reason are retained in the existing
protected audit/receipt workflow. Policy versions remain immutable.

Explicit employee, department, staff-category and site assignments are future,
bounded by their version period, and checked for same-target overlap within their
family. Role targets remain unavailable. End and replacement append immutable
ends; originals are retained. Writes require the actual outer serializable
transaction. Existing policy revision/rowversion guards and existing version and
ending uniqueness constraints remain in force. No SQL concurrency acceptance is
claimed from standalone simulations or query translation.

Repository source queries apply family/category/code, employee and historical
date scope before bounded materialization: membership/category rows 3,
applicable assignments 129 with a 128-result limit, version history 1025 with a
1024-result limit, and assignment history 4097 with a 4096-result limit.

The reader returns configured, missing, invalid_family, invalid_configuration,
conflict, historical_membership_unavailable,
historical_staff_category_unavailable or staff_category_not_applicable.
Equivalent canonical rules can coexist while retaining every assignment identity;
different interpretations have no inferred precedence. The employee's actual
dated category code must be explicitly applicable. Generic legacy approved
requests do not establish direct publication authority.

Digests retain exact stored JSON, membership/category identity, policy identity,
and all version/assignment/end authority, approval, actor, date, reason and
private-reference source fields. Private references are used internally in the
hash and are excluded from returned resolution/version DTOs. Existing ordinary
schema1-3 rules, resolver, repository, category1 reads and fingerprints are not
edited. No financial, work-credit, KPI or benefit effects are applied here.

## Owned Files

1. src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyService.cs
2. src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyReader.cs
3. src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyRegistration.cs
4. src/Cosmix.Infrastructure/Repositories/HR/AdvancedPolicyRepository.cs
5. src/Cosmix.Infrastructure/Repositories/HR/AdvancedPolicyInfrastructureRegistration.cs
6. src/Cosmix.Api/Controllers/HR/AdvancedAttendancePoliciesController.cs
7. docs/attendance-gap-c/C01VerifyRuntime.cs
8. docs/attendance-gap-c/C01VerifyPublication.cs
9. docs/attendance-gap-c/C01VerifyPersistence.cs
10. docs/attendance-gap-c/C01VerifyRegisteredJson.cs
11. docs/attendance-gap-c/C01Verify.ps1
12. docs/attendance-gap-c/C01Implementation.md
13. docs/attendance-gap-c/C01VerifySourceProof.cs
14. docs/attendance-gap-c/C01VerifySourceProof.ps1

## Verification Status

At the initial C01 source freeze there were **zero executed behavioral checks**.
That checkpoint was source-only: recovery/build holds and unrelated compilation
errors prevented execution. It did not establish working runtime behavior,
repository translation, registered endpoints, or SQL acceptance.

After the parent supplied a fresh green Application artifact and a new serial
slot, two C01 suites executed against current source with distinct D: runfile
identities. All six production SHA256 hashes were frozen before execution and
compared afterward.

| Executed evidence | Actual result | Limit |
| --- | --- | --- |
| C01VerifyRuntime.cs | PASS 41 | Pure reader resolution, missing/ambiguous/conflicting rules, actual dated category code, exact JSON/provenance hash sensitivity and safe DTOs. |
| C01VerifyPublication.cs | PASS 35 | Actual service and AttendanceOperation, with in-memory auth/data/private-storage boundary simulator. No real SQL transaction contention or HTTP authentication. |
| C01VerifySourceProof.cs, Application-only | PASS 30, 5 compiled documents | Actual supplied Domain/Application PE metadata and companion portable PDB hashes; current source checksums match compiled documents. Infrastructure/API unverified. |
| C01VerifyPersistence.cs | NOT EXECUTED | Infrastructure compilation stopped at C03 WorkTreatmentRepository.cs:48, CS1525 invalid expression before &&. |
| C01VerifyRegisteredJson.cs | NOT EXECUTED | Held for integrated Infrastructure/API compile freeze and a new parent serial slot. |

The 76 runtime assertions are separate from the 30 binary/source assertions.
Parent-reported typed-family/KPI/calculator checks are not counted as C01 results.
No C01 full-solution Release success, SQL connection/command, provider or hosted
HTTP acceptance is claimed.

Current successful runtime logs are under
D:/CodexTemp/cosmix-c01-4c988ce96cc948f8bec3ac9f0e7d29c7/Runtime.log and
Publication.log. The blocked Infrastructure attempt is under
D:/CodexTemp/cosmix-c01-06bb3eaac4ee4ff9b9380e40f9ad8567/Persistence.log.
Successful actual-binary proof is under
D:/CodexTemp/cosmix-c01-proof-b7d8324b0db5446290f7f4a9fa509f7f/SourceProof.log.
The earlier proof attempt failed because its checker incorrectly hashed a
portable PDB with its content ID populated; its corrected deterministic-checksum
calculation zeroes that 20-byte field in an in-memory copy before hashing.
No production source or supplied binary was modified to make that proof pass.

The parent supplied binaries checked were exactly:

- D:/CosmixVerification/part-c-parent-20261013/kpi-workflow-green/bin/release/Cosmix.Domain.dll
  SHA256 3E66DDC62D96310076D4CA53B8A6809A419A64985B512FD540DF51B6F8B1AF9F,
  MVID a0196d6f-6cd5-4aad-a139-55b711769d82.
- D:/CosmixVerification/part-c-parent-20261013/kpi-workflow-green/bin/release/Cosmix.Application.dll
  SHA256 08776614B6CB83993777035733A8A90C0CB123948D50B6B989FDC44CE2512CA1,
  MVID a3794557-d54f-40df-ae00-0b1d2b75b0d7.

The proof checks actual PE assembly identity, expected C01 type presence,
CodeView/PDB identity, the compiler's portable-PDB SHA256 checksum, each exact
source document path and each source SHA256 checksum. It compiles only its own
standalone check, with no project references and no production rebuild. The
default full proof requires all four product DLL/PDB pairs in a supplied parent
binary directory. Application-only mode explicitly leaves Infrastructure/API
unverified. Prepared all-four-assembly proof is not counted as executed.

Example after a fresh all-four-assembly green artifact and parent slot release:

```powershell
./docs/attendance-gap-c/C01VerifySourceProof.ps1 -BuildsReleased -BinaryDirectory 'D:/PARENT-SUPPLIED-FRESH-API-BIN'
./docs/attendance-gap-c/C01Verify.ps1 -BuildsReleased -SkipBuild -Checks Runtime,Publication,Persistence,RegisteredJson
```

The path above is an explicit placeholder for the parent-supplied artifact,
not a claimed existing path. C01 slot is returned and no further compilation
will start until the parent supplies fresh compile-green evidence and a slot.

## Earlier Blocked Attempts

The released C01 full Release attempt restored and built Domain, then stopped
with zero warnings and four Application errors in in-progress C05 files:

- WaitingReviews.cs:128: CS0833 duplicate anonymous property names.
- WaitingWorkflowService.cs:16: CS9113 unread access constructor parameter.
- WaitingWorkflowService.cs:267: CS0833 duplicate anonymous property names.
- WaitingRegistration.cs:11: CS0246 missing WaitingPerformanceEffectReader.

Subsequent isolated runtime attempts were also blocked by in-progress C03
WorkTreatmentCreditReader.cs:7, CS0535 missing CheckExclusiveAsync. No full-build
success or behavioral verification is claimed. The build slot was returned to
the parent after all C01 processes had stopped.

Full-build evidence is under
D:/CodexTemp/cosmix-c01-ce5d563b5c7347eb831421569a8c414c/Release.log;
the subsequent blocked attempts have unique bf66714ab1914e5b98ce4e3a66d38e06 and
41f262c9f70941389a690aaa1c402b3a run roots under D:/CodexTemp.

After an explicit parent build-slot release, C01Verify.ps1 -BuildsReleased
performs a strict Release build unless -SkipBuild is selected. The four suites
cover reader/publication behavior, EF
ToQueryString checks with an interceptor that forbids connection attempts, and
registered DI/MVC/OpenAPI/strict JSON checks without starting a host. Each run
uses a distinct D: root and runfile identity. TEMP, CLI home and NuGet temporary
outputs are redirected to D:. A 350 MB C: reserve is required before the run.
Every edit of an existing nonempty C01-owned file was preceded by a D: CreateNew
backup and SHA256 equality verification.

No EF model/configuration/migration, root producer/roster/OT, core DI/catalog,
ordinary repository/resolver or A/B files were edited by this C01 owner. No
SQL commands/connections, host start, provider activation, employee fixtures,
secrets, Git operations or deletions were performed.
