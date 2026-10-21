# R1 authenticated HR report acceptance - 20 October 2026

## Verdict

**PASS: 21/21 checks across 20 actual HTTPS requests.**

The approved synthetic HR account previewed its own employee-master record,
downloaded CSV/XLSX, requested one durable PDF, replayed the same request key,
downloaded the completed PDF, and logged out. Reusing the retained logged-out
cookie was rejected. This is bounded authenticated SQL/storage acceptance, not
full HR or production acceptance.

## Approved target and execution

- Existing `Software-PC1\LOCALDB#9F3001F7 / Cosmix_Testing`, SQL PID 25768.
- ONLINE; owner/operator `COSMIXRND\Huma.Khan`.
- Family `D9C34627-18A2-41CF-A804-821E39516303`.
- Existing MDF/LDF: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- 25 applied migrations before and after; **no migration applied**.
- Approved local named-pipe transport exception only; not production TLS evidence.
- Owned `https://localhost:7481` host, PID 23632, exited after the test.
- Final port 7481 listener count: zero. Existing SQL process remains running.
- New private root:
  `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020R1`.
- Release: `D:/CosmixVerification/hr-backend-20261020/hr-six-gap-build11/bin/Cosmix.Api/release`.
- Reviewed/executed packet SHA256:
  `3F1344407306C9468837E07A1079D6173EF845223532F2742D0BBA0F0B839355`.

The preflight checked the exact target, owner/files/process, full migration list,
current report permissions, absence of the R1 root/receipt/prior HR export,
terminal OTP deliveries, absence of an unconsumed/unrevoked historical HR login
challenge, port availability, release hashes and retained evidence.

Read-only preparation initially failed on empty SQL JSON ID lists. The runner
now normalizes SQL NULL to an empty JSON array, preserves empty/single-row array
shape and compares enumerated IDs to the actual table count. No host/private/SQL
writes occurred during those failed inspections. No guard was bypassed.

## Actual checks

1. Anonymous and pre-MFA permission access denied.
2. Real password validation, bounded local OTP/MFA and current HR session.
3. Exact employee-only preview; second page returns zero rows.
4. CSV contains exactly the preview employee; XLSX report-data sheet contains
   exactly one employee row.
5. One PDF request and same-key retry return the same durable job.
6. Normal pending status, captured-job processing, then normal completed status.
7. Authenticated PDF download with real current session/report authorization.
8. SQL verifies one completed export, one stored file and one completed receipt.
9. Exact export outbox completed once; exactly one report-ready notification
   intent remains pending and undispatched.
10. Actual encrypted scope, receipt and output manifest/file decrypt with their
    recorded purpose, key reference, identity and SHA256 bindings. The downloaded
    PDF equals the protected stored output.
11. Missing-CSRF logout denied; valid logout succeeds; retained cookie denied.
12. All 155 protected SQL snapshots and 48 retained C01-C04/V1 files remain
    unchanged, including a second comparison after host exit.

### Retained counts

| Record | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Accounts | 3 | 3 | 0 |
| Migrations | 25 | 25 | 0 |
| Sessions | 128 | 129 | 1 |
| Refresh tokens | 189 | 190 | 1 |
| OTP challenges | 134 | 135 | 1 |
| OTP deliveries | 134 | 135 | 1 |
| Rate buckets | 81 | 89 | 8 |
| Audit events | 2582 | 2601 | 19 |

New report records: one export, one stored-file metadata row, one receipt,
two outbox rows (export and notification intent). Fourteen encrypted private
objects were retained. No employee, role, permission, policy, vehicle or financial
business record was modified. No existing files were deleted or reset.

## Independent output verification

`VerifyHrReportsR1Pdf.cs` read the actual downloaded PDF with PdfPig and compared
every one of its 14 fields against the actual CSV download. One page, one record,
all fields equal, no glyph outside page boundaries.

Evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-ba098994959442139c74310b07f62657/`.
Poppler rendered the one-page PDF; visual inspection found readable, complete,
non-overlapping text. Poppler reported unavailable Symbol/ArialUnicode display
fonts, but this ASCII test record rendered. This does not certify native Urdu
glyph rendering; the existing PDF uses escaped Unicode string notation.

## Important limits

The test uses the unchanged report handler and real SQL/private store, but a
reviewed exact-job acceptance dispatcher. Global workers were removed. It proves
that captured job's generation/commit/download, **not** production queue polling,
multi-worker contention, restart recovery or notification delivery. Local MFA
does not test a real OTP gateway. No real face or legal activation was attempted.

Other report types received read-only repository SQL checks, not authenticated
exports with rich fixtures. Employee-master success does not accept every report
or HR business workflow. Benefit/recovery and policy-backed vehicle/outstation
business SQL fixtures and frontend UAT remain separate.

Private results are retained under `results/`: `start.json`, `acceptance.json`,
`host-final.json`, `post-host-preservation.json`, CSV/XLSX/PDF and the render.
Credentials, keys, cookies and private host logs are not reproduced here.
