# Demo policy and attendance verification — 28 October 2026

Employee #33, **TEST ONLY DEMO ATTENDANCE 20261028**, now has an explicitly synthetic, employee-only policy for 28 October. Actual authenticated correction and independent approval produced a persisted **Present** result: **480 worked minutes, 480 payable minutes, 0 late minutes and 0 overtime minutes**. Payable minutes are attendance inputs, not a payment; `payrollReady=false` remains intact.

## Configuration and scope

- Employee: `405f402c-7cd8-47ac-9f85-71c993b9d49d`.
- Policy version: `f41c960f-4b14-4270-b5da-67574bc903c4`.
- Applicability: 28 October inclusive to 29 October exclusive, this demo employee only.
- Existing demo shift: 09:00–17:00 Asia/Karachi. Explicit synthetic rules include a 15-minute late threshold, no active late penalty and zero ordinary deduction fractions. These are test inputs, not approved company defaults.
- Six appended configuration rows: policy, version, employee assignment, empty synthetic holiday calendar, original roster ending and replacement roster. One labelled fixture audit record. The original 27 October roster and factual summary were preserved.
- Historical configuration was prepared by the guarded synthetic fixture adapter, not by pretending to publish through the normal management API. Normal publication remains future-only. No production guard or authorization was weakened.

## Executed checks

| Verification | Observed result |
|---|---|
| Rules validation | Explicit configuration accepted; invalid arrival thresholds rejected |
| Nonfuture publication preview | HTTP 200, `canPublish=false`, exact `policy_future_date_required` error |
| Source preview | Correct dated site/shift/calendar, unlocked period |
| Admin submission | Pending; attendance not applied before review |
| Requester self-review | HTTP 403, `correction_requester_cannot_decide` |
| Separate HR approval | Saved correction, session and calculated summary |
| Exact approval retry | Same decision/session/summary; no duplicate business records |
| Daily and saved summary | Present; worked/payable 480, late/OT 0; exact policy binding |
| Stale recalculation | HTTP 409, `attendance_summary_revision_conflict` |
| Full October calendar | HTTP 200, all 31 dates; correct 28 October result |
| Prior 27 October | Original factual-only summary identity and values preserved |
| Summary history and independent SQL | Exactly one 28 October summary and one session |
| Logout | Both owned sessions logged out normally |

The final continuation passed **24 HTTP assertions** with 11 explicit business requests in that continuation. This is not the total request count across both attempts. The first attempt saved the business result but stopped at a test assertion expecting `calculated` instead of the saved-summary contract's `Present`. Only the assertion was fixed; the continuation reused the existing records. Four synthetic MFA sessions were used across both attempts. Review subsequently tightened the future-date assertion, and the exact assertion passed against the retained live response without another business run.

The existing standalone daily-calculation suite passed **193 checks**. These are separate offline rule/service checks, not additional live SQL tests. Fixture inspect, prepare and independent SQL verification also passed. PowerShell parsing and diff whitespace checks passed.

Saved summary: `533b0126-8141-4840-a949-d6e1f43e4255`.
Saved session: `a2399596-cd69-4a95-8599-bb08f886dfd4`.

## SQL preservation

Verified target: `Software-PC1\LOCALDB#36E7B148 / Cosmix_Testing`, ONLINE; owner `COSMIXRND\Huma.Khan`; database family `D9C34627-18A2-41CF-A804-821E39516303`; database GUID `631714B1-1CB2-43CA-9A04-4B2C04E3D1A7`. Exactly 31 existing migrations; **zero migrations applied**.

Before/after hashes covered 173 tables: **152 unchanged, 21 expected changes, zero unexpected changes**. All 33 employee rows and 42 protected table hashes remained unchanged. No payroll, payment, role/grant, raw-punch, face, legal, migration or period-lock changes.

Business additions: six configuration rows, one correction, one session, one summary and summary-session link, one approval request/decision, two idempotency receipts, one notification and one outbox record. Audit grew by 55 rows; four local OTP challenges/deliveries and four session/refresh-token rows were added. Auth rate-bucket contents changed with unchanged row count. Existing records were not deleted or reset.

## Limits and artifacts

This verifies configured attendance calculation through authenticated manual-duty correction. It does not claim real biometric/mobile punches, full hiring acceptance, legal payroll activation, salary payment or full HR production acceptance. The demo policy does not configure all employees or future dates. Rendered browser interaction was unavailable; API and SQL results were checked directly.

Source files: `DemoPolicyFixture.cs`, `InvokeDemoPolicyAttendance.ps1`, `VerifyDemoPolicyPreservation.ps1`, and the bounded evidence-folder option in `SnapshotDemoDatabase.ps1`. No production source changes were required for this configuration test.

Private evidence: `C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\DemoAttendancePolicy20261028`. Keep private fixture/settings/credential material local. The result, daily/calendar/history responses, approval/retry/denial responses and hash comparison are retained there. API on localhost:7240 and frontend on localhost:5173 were left running.
