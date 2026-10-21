# Part A23 upgrade and acceptance runbook

Prepared 2026-10-13 for `20261013121357_CompleteHrFoundationPartA`, immediately after retained `20261013101327_AddOfficeRosterHistory` (Office22). At preparation, the generated [idempotent upgrade SQL](../sql/20261013121357_CompleteHrFoundationPartA.sql) had not been executed.

Continuation checkpoint: the user selected the existing isolated local testing database. Live family/owner/history and three known fixture identities were verified; its actual pre-upgrade history was 12, with five accounts and fourteen employees. Fresh COPY_ONLY/CHECKSUM backup, VERIFYONLY and a true isolated restore passed before the reviewed 12-to-A23 range was applied to the clone and selected original. Separate independently reviewed B24 was then applied to both, after fresh23 backups; the clone also passed a full idempotent B24 rerun. Both now have 24 migration records and all original IDs/non-rowversion source facts are preserved. No production target was accessed. See [the continuation handover](../HR-Part-A-Continuation-2026-10-13.md) for evidence and remaining acceptance gates.

Authenticated HTTP acceptance is still pending: the actual local OTP host guard requires exactly three synthetic accounts and rejects the retained five-account target. Actual management policy values, operational rehire and external SMS/face acceptance also remain unfulfilled. Schema application does not close these gates or establish legacy lifecycle reconciliation.

## Before the upgrade

1. Confirm the intended target, responsible operator, current migration history and application/schema compatibility. Preserve the existing migrations; do not recreate Office22 or mix a later Part B schema change into A23. Use the reviewed pending range for that target rather than treating this Office22-to-A23 script as a fresh-database installer.
2. Capture a backup and verify a restore on an isolated approved target, including private evidence objects and their encryption keys. Record evidence outside the repository without credentials or employee data. A build and an offline script inspection do not prove a restore.
3. Inventory due employee status history, exact onboarding submission/decision provenance and lifecycle approval actor/time evidence. Identify conflicting effective instants, cached-state mismatches and protected system-role holders. Protected owners require the existing separate handover process.
4. For previously suspended or ended employees, inventory every active face enrollment, every unrevoked role assignment/device/session, each unconsumed and unrevoked mobile OTP challenge, and account/channel state. Include expired sessions/challenges and scheduled roles; expiration alone is not the recorded revocation required here.
5. Review existing SIM/certification dates and custody conflicts against the new restrictive keys/checks. Existing issue/return facts must remain intact. Resolve factual inconsistencies through reviewed, evidenced operations; do not weaken constraints or invent predecessor/approval history to make an upgrade pass.

## Legacy lifecycle evidence

A23 introduces a nullable applied-status marker. Its backfill only accepts uniquely timed, due **inactive** status changes whose cached status matches and whose employee, approved onboarding submission/version/provenance and lifecycle approval actor/time evidence agree. It also requires no active face enrollment and, for every linked account, Suspended/Disabled status, both channels disabled and no residual role/device/session/open mobile OTP challenge.

The marker backfill writes no revocation, account status, historical status, source date, provider or private evidence. It cannot prove that an older offboarding implementation performed the newer cleanup. Any residual access or ambiguous evidence leaves the marker null for review. Active release history is never automatically marked.

Before rollout signoff, enumerate unresolved due approved lifecycle changes through the authorized history/reconciliation paths and the restricted target inventory. Record a disposition for each. A null marker or `employee_status_requires_manual_review`, `employee_status_history_conflict` or related reconciliation error is an unresolved gate. Do not enable suspension release for such a record, blanket-set markers, alter old approval/status rows or delete access/face evidence to bypass the gate. Use approved revocation workflows where available; an incompatible already-applied legacy timeline needs a separately reviewed and audited reconciliation plan. Genuine target data is required to make that plan concrete. Authentication continues to deny inactive employment and an unreconciled Active release independently of worker timing.

## Apply and verify on the approved target

1. Reconfirm reviewed file hashes and target history immediately before applying the pending range in the coordinated window. Execute using the approved SQL/EF deployment process. Record the resulting migration history and schema readiness.
2. Verify existing rows, IDs, source dates, attachments and history remain present. The only new date projections derive from retained SIM issue/return and certification issue dates; new certification rows default to initial revision/status 1.
3. Exercise real authorized HTTP writes and identical/changed-key retries for access, bank, documents, lifecycle, records and routing. Verify SQL transaction rollback and competing requests, current revocation after replay and private-file authorization after IO. No synthetic test result replaces this step.
4. Verify actual approved SMS/MFA/recovery/device flows, scanner/quarantine behavior, private storage/key restore and current employment denial. Confirm retirement/release, field rules, custody/SIM authority and routing values only from approved management decisions.
5. Run the lifecycle sweep and review every reported conflict. Confirm no unresolved legacy state is counted as successful reconciliation. Preserve encrypted face objects until the separately approved Part B05 retention/disposal process authorizes their disposition.
6. Record operational signoff only after the real target checks pass. Shared claim/benefit consumers, delivered notifications/reports and broader category/rehire decisions remain the specified later work.

## Recovery

The downgrade begins with an evidence-preservation guard before any drops. It refuses retained Part A histories, new lifecycle/document states, operational SIM/certification provenance and scoped delegation evidence. A generated Down script is for review; it is not a routine safe rollback once Part A is used. Prefer a reviewed forward fix or verified restoration of the coordinated database/private-store/key backup. A downgrade requiring removal of retained evidence needs its own approved preservation plan. No deletion or restoration was performed by this implementation.
