# Initial CEO SuperAdmin setup

This is an explicit local command for an empty business database. It does not
start the API, create an HTTP setup endpoint, send SMS, or seed credentials.
The actual CEO/owner must authorize self-initialization and the enumerated fixed
role grants. The operator checks the retained approval evidence; the command
cannot establish the authenticity of a paper or externally signed approval.

Do not run until the real identity and authorization are available. The command
refuses an unmigrated model, a different confirmed target, any existing business
row, redirected console input/output, or additional arguments. There is no
force, replacement, or password-reset mode.

## Private inputs

Provide a private JSON file with these exact case-sensitive properties:

- `Employee`: the actual `EmployeeDraftDetails`; name, identity document type
  (CNIC = 1, passport = 2), identity number, contact, official email and actual
  joining date are required. Unknown/optional information should remain absent.
- `LoginPhone`: explicitly owner-approved canonical international phone.
- `OwnerLegalName`, `OwnerIdentityDocumentNumber`: must match this employee.
- `OwnerApprovalReference`, `OwnerApprovedAt`: real retained approval reference
  and its actual timestamp, not a fabricated HR approval.
- `AuthorizesSelfInitialization`, `AuthorizesFixedRoleGrants`: both must be true
  and supported by the approval evidence.
- `OwnerApprovalEvidenceFile`: absolute path of the retained approval document.
  Its exact bytes (at most 1 MiB) are included in the encrypted snapshot.

Do not include a password in JSON. Unknown JSON properties are rejected.
No example identity or default account is supplied.

Pre-provision a private snapshot directory and a separate private key directory.
Supply an existing cryptographically random 32-byte binary AES key, a stable
non-secret key reference, and a durable private backup of that key. The command
does not generate, rotate, or log encryption keys. Retain the directory location
and key reference in the deployment's private operating record.

All paths must be absolute local paths outside Git, the application binaries
and `wwwroot`; symbolic links/junctions are rejected. On Windows, the private
file and containing-directory ACLs may allow only the current operator, SYSTEM,
and Administrators. Unix paths must have no group/other access. The adapter
checks these conditions and does not silently change supplied-file permissions.

## Invocation

Configure `ConnectionStrings:CosmixDatabase`,
`WebAuthentication:MinimumPasswordLength` (12-128), and
`MobileAuthentication:BusinessTimeZoneId` through existing private configuration.
Use the intended environment; standard user-secrets loading requires Development.

From the backend directory:

```powershell
dotnet run --project src/Cosmix.Api -c Release --no-launch-profile -- bootstrap-superadmin --help
dotnet run --project src/Cosmix.Api -c Release --no-launch-profile -- bootstrap-superadmin
```

Run the second command only when authorized to create the actual account. It
prompts for private paths, backup/self-initialization attestations, exact live
SQL server/database confirmation, and the owner's concealed password twice.
The fixed permission list is displayed before authorization. No grants are
automatically added for future catalog entries.

Six saves run inside one serializable transaction under `cosmix:authentication`:
employee draft; owner submission; owner decision; pending account; fixed roles
and initial assignment/scope; web credential, unverified phone binding and audit.
The role creator/grantor/assigner is the actual new owner account. The executing
OS subject is recorded separately in submission, snapshot and audit metadata.
Normal HR actors are never invented. Admin is a fixed role definition only;
the single created account receives SuperAdmin. Mobile remains disabled and
web login still needs real registered-number SMS proof.

## Snapshot and recovery

Snapshot names are random `<version>.bootstrap.enc`, created once and marked
read-only. Binary format: `CXB1` (4 bytes), AES-GCM nonce (12), tag (16), then
ciphertext. UTF-8 additional authenticated data is
`cosmix-owner-bootstrap/v1/<version>/<key-reference>`. The decrypted JSON is
`BootstrapSnapshotPayload`, including identity, approval-document bytes, target,
executor, timestamp and granted permissions; it never contains a password.
The database retains the ciphertext SHA-256, version, schema version and key
reference. Creation includes a durable flush, read-back and authenticated
decryption check. Back up the encrypted snapshot with its independent key.

A confirmed rollback attempts to delete only the newly created matching snapshot.
Deletion failure or an uncertain commit retains the encrypted object and reports
its opaque reference. Reconcile it with the database before retrying or deleting
anything. Process termination can also leave an unreferenced encrypted file;
there is deliberately no automatic directory-wide cleanup. Supplied identity,
approval and key files are never deleted. The private filesystem administrator
can override read-only flags; retain protected backups for durable evidence.
