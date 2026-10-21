# HR encrypted export verification - 19 October 2026

## Executed result

`VerifyHrDurableExports.cs`: **401 checks PASS**, exit 0, against frozen
`D:/CosmixVerification/hr-backend-20261019/hr-production-build02/bin/Cosmix.Api/release`.
This replaces the earlier 365-check result for this suite; it is not 401 additional checks.

Command:

```powershell
& './docs/attendance-gap-c/VerifyManaged.ps1' -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261019/hr-production-build02/bin/Cosmix.Api/release' -Sources @('docs/hr-srs-remediation/VerifyHrDurableExports.cs')
```

Output: `D:/CosmixVerification/part-c-parent-20261013/managed-a5d1838f6e9c4f998d327aab2fa2e470`.
Standalone compilation retained two CS1701 PdfPig reference-unification warnings.
No product rebuild was needed: this continuation changed verification code, not product binaries.

## New coverage

For each CSV, XLSX and PDF export, the actual export service/dispatcher/renderers
use the production `EncryptedPrivateFileStore` with fresh synthetic owner-only
storage and a fresh private key. Checks cover:

- Completed job download and encrypted, read-only output on disk.
- Encrypted scope/receipt/audit/output/manifest objects.
- Reconstructed service/store download and idempotent replay without a duplicate
  stored-file or notification record in the synthetic repository.
- A separate dotnet reader process verifies the exact saved reference, purpose,
  byte length and SHA256 without sharing the writer's in-memory file store.
- Corrupted ciphertext, missing output and missing key fail closed with the
  actual `private_file_storage_unavailable` contract.
- Restoring only the freshly generated test objects restores readability.
- A revoked synthetic session is rejected with `invalid_authentication`.

The three successful private evidence directories are:

- `D:/CosmixVerification/hr-backend-20261019/export-storage-005607a00cfe4dd99af4d5cfd6760b79`
- `D:/CosmixVerification/hr-backend-20261019/export-storage-2b47110ad98e43b79463e14e163d2ffc`
- `D:/CosmixVerification/hr-backend-20261019/export-storage-1e07a7d774a94207925da576201c6bbc`

Fresh fixture objects only were deliberately damaged/renamed and restored.
No existing application storage, C01 artifacts, database, credentials or keys
were changed. No cleanup ran. Earlier failed fixture runs remain retained.

## Fixture failures corrected

Initial new tests incorrectly tried to overwrite a read-only encrypted object,
then expected synthetic error codes for physical-store failures and revoked
sessions. The fixture now deliberately changes attributes only on its own newly
created object, restores them, and asserts the actual documented error contracts.
These were verification-harness corrections, not demonstrated production defects.

## Acceptance limits

Repository, transaction and authentication-state adapters remain synthetic.
This verifies real encrypted export IO and independent-process decryption, NOT a
SQL-persisted worker restart, authenticated HTTP flow or actual SQL concurrency.
No real SMS/face provider was called. Full HR production acceptance is not achieved.

## Latest bounded authentication approval: stopped before execution

The latest user approval permits only two synthetic HR/Admin logins, local MFA,
CSRF/permission checks and logout/revocation, with no business writes, on owned
`localhost:7481`. It requires every precondition to pass and a stop on mismatch.

Read-only source review found that the existing C01 runner is not compatible:

- It still pins pipe `LOCALDB#2E40033B` / process 24152 rather than the later
  observed existing instance pipe `LOCALDB#F9CAED57` / process 29836.
- It creates, updates and deactivates a Department, beyond the auth-only approval.
- Existing C01 private settings/results must be preserved, not overwritten to
  make the old runner pass.

Accordingly no C01 execution/resume, host start, authentication request or SQL
write was performed in this continuation. Fresh SQL identity/ONLINE, the exact
two account bindings and the remaining host guards are NOT reported as newly
passed. The previously observed database identity is historical evidence only.
Separate auth-only preparation approval was requested; executing the old broader
runner or weakening its guards is not authorized.
