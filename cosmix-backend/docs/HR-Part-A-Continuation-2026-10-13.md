# HR Part A continuation status

Developer handover and remaining acceptance gates


Developer handover | 13 October 2026 | SRS version 1.2 | Part A continuation


This addendum records the Part A continuation against the original A to G completion plan. It gives developers the implemented behavior, executed evidence and remaining gates. Source checks, local operational checks and complete deployed acceptance have different status.


PM note: Is addendum mein har item ka status, saboot aur agla gate line wise diya gaya hai. Developer completed source ko reuse karein. Asal policy approval aur live acceptance ko sirf un ke verified evidence ke baad complete mark karein.


Status meaning: SOURCE VERIFIED means implemented code passed the stated checks. LOCAL VERIFIED means an actual local component or isolated restore passed within its stated scope. PENDING means the named business input, integration or acceptance is still required.


Overall status: taxonomy/policy source and HR plus Attendance consumers are verified; rehire is source preparation. Local scanner, private restore, SQL restore and selected original A23/B24 upgrades passed. HTTP, genuine policy/provider and rehire activation gates remain; Part A and the whole backend are not operationally complete.

## Current checkpoint

### Integration and review checkpoint

**A-C01 | Status: SOURCE VERIFIED with live gates pending**

1. Passed after Attendance taxonomy integration: full solution Release build across all four projects, 0 warnings and 0 errors (57.79 seconds).
2. The earlier five scoped findings were corrected and independently rechecked. Fresh narrow review found no high-impact issue in the Attendance taxonomy integration scope.
3. The corrected HR taxonomy consumer run passed 25 checks and the foundation baseline passed 68. The new Attendance source check counts and remaining operational boundaries are recorded under A-C03.
4. Passed: 45 current model/DI/default-authority checks include seven Attendance consumers with the real taxonomy resolver injected. All 24 frozen migrations retain their hashes. B24's reviewed three new tables, ten nullable columns and 44 Up operations remain unchanged; no new schema or SQL was required for this source integration.
5. Owner: integration lead and Part B migration owner. Retain the frozen A23 migration range; later model/schema work requires its own reviewed handoff.
6. Known fixture gap: older VerifyPublication.cs fails to compile with CS7036; its constructor omits IOfficeRosterRepository and OfficeRosterImpactValidator. It is not counted as passed.
## Taxonomy publication and HR consumption

### Publish approved dated staff mappings

**A-C02 | Status: SOURCE VERIFIED actual values pending**

1. Implemented immutable typed proposal, independent fixed Admin decision, publication/history, protected private proofs and requester cancellation on existing policy/approval/audit/receipt storage.
2. Strict raw JSON, exact category codes, required applicability/eligibility booleans, current authorization on retry, authority windows and predecessor/date chains fail closed.
3. Evidence: 92 actual feature/domain/controller checks passed with synthetic collaborators. These checks do not prove SQL rollback or authenticated deployed HTTP.
4. Exact OFFICE/SITE baseline remains before the first verified publication and for historical dates before its effective start. An invalid, inactive, expired or missing effective mapping cannot revive baseline.
5. Next gate: management supplies actual category meanings, effective dates, eligibility mappings and approved publication authority. No invented taxonomy values or approval grants are seeded.
### Consume taxonomy in HR and Attendance

**A-C03 | Status: SOURCE VERIFIED**

1. Guarded arbitrary category metadata may be created/reactivated; approved dated mapping independently controls employee eligibility.
2. Hiring freezes separate joining-date and activation-date version/fingerprint evidence. Review revalidates both; past joining dates may cross a publication boundary with compatible location/schedule semantics.
3. Office requires Branch. The legacy SITE baseline keeps activation Site optional; new approved mappings may require Site before activation. Scheduled Site duty requires Site. Amendments and assignment use dated guards.
4. Old unpinned hiring snapshots remain usable only with exact legacy taxonomy compatibility and absent foundation-field authority.
5. Rosters and Office sources retain exact category, actual publication ID/fingerprint, location, schedule and outside-OT classification. Only exact legacy OFFICE/SITE omits the nullable proof. Dated/range and after-IO checks fail closed across every occupied duty date.
6. Broader Site OT needs OutsideOvertimeInheritance and actual global/employee eligibility, closed valid duty, approved duration/compensation and independent approval. Every Office mapping is denied. Nine-hour duration/formula is retained; other duty interpretations need an approved supported rule. Checks include OT beyond roster end.
7. Calculation and payroll source pins/fingerprints include the roster taxonomy proof; manual corrections retain the same historical pin. Part B calculation schema-5 checks and historical evidence are preserved.
8. Fresh scoped checks passed: roster 30, Site scheduling 171, Office source 16, OT 109/rules 26/DI 8, calculation 193, payroll producer 59/service 80. They use actual compiled consumers with synthetic collaborators; they do not prove hosted HTTP or financial acceptance.
9. Next gate: publish genuine broader mappings and required policies, then complete guarded workflow acceptance. Existing-employee category conversion remains a separate approved workflow. Rehire, provider and host gates remain below.
## Foundation policies and required documents

### Publish field lifecycle and record policy authority

**A-C04 | Status: SOURCE VERIFIED actual policy inputs pending**

1. Implemented Fields, Lifecycle and Records publication, immutable history/current reads, independent fixed Admin review and cancellation using existing mapped storage; this module adds no migration.
2. Protected replay reauthorizes; strict JSON/enum/date validation and exact retained decision/private proof bind the effective rules. A future or expired publication is unavailable and never falls back to configuration.
3. Evidence: 81 service/domain checks passed; 18 actual EF query/translation checks passed with zero database connections. These are scoped source checks.
4. Configuration baseline is allowed only when no actual publication exists. Defaults remain unapproved; current read/request/review grants, web MFA, All scope and fixed-role review are required.
5. Next gate: supply approved authority version/date window, field vocabularies and limits, retirement/release flags, record-review mode and exact category document lists. Publication authority for Attendance does not authorize HR policies.
### Enforce policy in hiring and post hire document changes

**A-C05 | Status: SOURCE VERIFIED**

1. Global and category-specific Standard document requirements apply to hiring completeness and request/approval of post hire document changes. Existing minimum identity/profile-photo checks remain.
2. Protected proposal tokens pin immutable policy version identity. Reusing the same human label with different rules cannot bypass a fresh reviewed proposal.
3. Evidence: 19 root policy-consumer checks and 9 post hire document-manifest checks passed with actual services and synthetic ports.
4. Next gate: publish genuine approved values and run SQL/private-file/request/review tests. Asset/SIM custody, certificate renewal, routing/delegation and later A to G business services retain their original separate completion tasks.
## Rehire preparation and permanent blacklist

### Prepare stable identity employment episodes

**A-C06 | Status: SOURCE PREPARATION VERIFIED operational integration pending**

1. New unmapped Domain/Application contracts and pure rules retain the employee ID/number and one account while proposing a new immutable episode, new terms and independent final approval.
2. Evidence: 118 pure preparation checks cover 17 named operational gates; their operational adapters and integration remain pending. The real EF model contains none of the new rehire/episode preparation types.
3. There is no operational rehire API/service/DI registration, mapped persistence, migration or wired consumer. Existing terminal-to-Active readiness denial remains. The proposed Admin approval policy is awaiting management confirmation.
4. Next gate: approve timing/status/probation/tenure/leave/financial treatment, then integrate episode persistence and every consumer under the coordinated migration owner. Preserve original joining/onboarding, salary, assignment, access, attendance and financial evidence.
5. Account setup must revoke old sessions/grants/devices/phone/OTP/reset/face use, retain historical evidence, preserve independent blocks/locks, keep channels disabled and require fresh approved access setup.
6. The 17 gates are stable identity; previous episode; new approval; current policy; employment terms; required documents; authentication proofs; account reset; fresh access; face isolation; attendance duty; historical coverage; policy/entitlements; financial history; atomic persistence/receipts; legacy reconciliation; employment blacklist.
### Enforce nonoverridable permanent blacklist

**A-C07 | Status: SOURCE PREPARATION VERIFIED authoritative resolver pending**

1. A permanent flag or permanent authorized source outcome always denies, including after submission/approval. Status relabels, new-ID attempts, proposed Admin approval and consumer proofs cannot override it.
2. Unknown, ambiguous, missing or unreadable authoritative ban provenance fails closed. Clear requires the trusted complete lifetime register, exact same-employee cases/versions/approvals and retained evidence bindings.
3. SRS SITE-011 annual 120-late behavior may initiate the authorized disciplinary workflow. It does not create an automatic permanent ban or bypass required approval.
4. Next gate: implement the trusted resolver and all 17 current server proof adapters. A future plan or time window alone never authorizes activation; recheck sources and bans inside the transaction at due start.
## Actual scanner and private restore evidence

### Verify installed scanner and current definitions

**A-C08 | Status: LOCAL VERIFIED**

1. Actual installed ClamAV adapter passed 10 local checks: clean bounded synthetic bytes, inert antimalware rejection, actual engine/version identity, unchanged caller bytes, disabled/invalid/oversized denial and cancellation.
2. Official updater refreshed daily definitions to 28152; daemon reload and adapter identified ClamAV 1.5.4. The antimalware marker was streamed in memory; no employee attachment was used.
3. Next gate: authenticated document quarantine/rescan/download, scanner failure during request, retained audit and restart acceptance. Local detection alone does not prove all document types or deployed availability.
### Verify encrypted object and key restore

**A-C09 | Status: LOCAL VERIFIED**

1. Passed: 109 existing encrypted objects and one required key were backed up and restored into a separate restricted private root; all original/restored bytes matched. Seven actual restored-store encryption roundtrip and scanner checks passed; original objects remained unchanged.
2. Private backup roots and keys remain restricted; public evidence contains safe aggregate results only. Existing ciphertext and original key material were preserved.
3. Next gate: exercise restored business receipts/audit/evidence decryption through the authorized API with the corresponding SQL state. This restore result does not establish every historical object purpose or HTTP flow.
### Retain operational process ownership

**A-C10 | Status: LOCAL evidence retained**

1. Scanner execution used a hidden task-owned process with exact PID/creation time and binary/configuration binding. The database clone runner selected the established owner-known backup family and refused occupied destinations.
2. Next gate: retain exact process disposition and host ownership records for acceptance; use bounded owned runs and preserve failed/partial attempts for review.
## Live database acceptance and remaining gates

### Verify SQL restore and selected original migrations

**A-C11 | Status: LOCAL VERIFIED SQL restore and selected original migration 24**

1. Passed: fresh current12 COPY_ONLY/CHECKSUM backup, VERIFYONLY and a true isolated restore completed. The clone reached frozen A23 through 11 reviewed batches with all retained IDs and original non-rowversion column facts matching row-multiset hashes. The selected original Cosmix_Testing then upgraded from 12 to A23. Later B24 clone application, full idempotent repeat and selected-original application all passed, with a fresh current23 COPY_ONLY/CHECKSUM backup and VERIFYONLY. Both targets now have 24 history records, 5 accounts and 14 employees; original source facts and IDs are preserved, and three known private fixtures match uniquely.
2. At the prior SQL checkpoint, 17 actual SQL-backed HR checks passed on the selected original at migration 24: OFFICE/SITE resolve the legacy baseline and an unconfigured broader code is denied. Fields, Lifecycle and Records readers return NONE because genuine values are not published. The real LocalTesting.VerifyDatabaseAsync method raised the expected exact-three-fixture exception with five preserved accounts. No host or HTTP/authentication flow was exercised; the later Attendance source integration made no additional SQL, account/role or provider changes.
3. Current live 24/5/14 facts supersede older retained 22/3/11 records; 12/5/14 was the verified pre-upgrade baseline. Rowversion alone is excluded from retained source-content hashes. Backup VERIFYONLY and true isolated restore were both executed.
### Complete authenticated API and external acceptance

**A-C12 | Status: PENDING named inputs and live gates**

1. Blocked: the real SQL-backed LocalTesting.VerifyDatabaseAsync method was invoked on the selected migration-24 database and raised the expected guard exception: exactly 3 synthetic fixture accounts are required, while 5 accounts are preserved. The legacy host also pins a stale pipe. No host was started or HTTP/authentication flow exercised. Do not delete accounts, reseed or weaken the guard; prepare a reviewed bounded host profile and explicit account classification before HTTP acceptance.
2. Carrier SMS: approved provider/endpoint, private credential and approved real destination are absent. Local SMS fixtures can prove only local delivery and cannot establish carrier success.
3. Face: reviewed CompreFace deployment and ONNX artifacts/contracts remain absent or unverified. Live device/capture/liveness/match/replay/outage acceptance is still required.
4. Finish in order: guarded acceptance host for the preserved 5-account target; SQL/HTTP retry/concurrency/current-revocation and receipt/audit/decryption/restart checks; genuine taxonomy/policy publication; rehire operational integration; carrier/face acceptance when supplied.

Evidence: backend docs/Attendance-Taxonomy-Integration-20261013.md, docs/Staff-Taxonomy.md and docs/Hr-Foundation-Policies.md. Scoped reports are in docs/foundation-policies/Verification.md and audit/hr-srs-2026-10-13/part-a-continuation, including roster-taxonomy and taxonomy-overtime verification. The integration report records the consumer counts and points to the 16-file hash manifest.


Handover rule: record source/build, actual target/history, private storage, provider and client acceptance separately. Close each original A to G task only after its own acceptance criteria pass.

