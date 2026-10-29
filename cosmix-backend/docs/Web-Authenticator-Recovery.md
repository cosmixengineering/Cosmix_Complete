# Web authenticator and independent recovery

This flow applies to employee web accounts. The employee mobile app retains its existing phone, device and consent flow; it has no Admin/SuperAdmin approval panel.

## Enrollment and sign-in

An eligible employee signs in with the existing password/SMS flow, opens **Account & Security**, and confirms their current password. The setup key is shown once and stored only as an encrypted private-file reference. A six-digit TOTP confirms a pending enrollment within ten minutes and only from the initiating browser session. Confirmation increments the account security version, revokes existing sessions/challenges, and returns ten recovery codes once. Store these outside the browser in the employee's approved private password manager. Only keyed recovery digests are persisted.

Subsequent sign-in uses **Authenticator / recovery code**, with the email/password factor followed by the enrolled authenticator or one unused recovery code. SMS login cannot downgrade an enrolled account. Codes, challenges, browser context, current approved phone, employment eligibility and security version remain checked. TOTP counters and recovery consumption are recorded within the existing serialized authentication transaction. Refresh and administrative authority checks support both valid factor types. Changing a password preserves the enrolled factor while revoking old sessions.

Recovery-code regeneration requires the current password and a fresh TOTP. It revokes all previous recovery codes, including authority granted to sessions created using those codes. It does not return the existing codes. A lost response is not grounds to replay or expose stored plaintext codes.

## Independent reset

The employee's web access setup shows recovery only to an independently authorized administrator. Review requires a current administrative session, all-scope `access.accounts.web.manage` and `access.roles.permissions.manage`, and authority over every potential target-account permission. A system-role target requires SuperAdmin authority. Both same-account and same-employee reset are denied. The reviewer supplies the current row version, review reason and identity-review reference. The protected review, reviewer identity and version transition are retained in append-only `auth.MfaResetApprovals`.

Reset revokes all existing sessions, active factor credentials, recovery codes and outstanding login challenges. It does not issue a login session. The employee then signs in using SMS and enrolls again. **Production SMS remains an external prerequisite for initial enrollment and reset fallback.** No provider is fabricated or bypassed.

This is independent approval by a different authorized employee, consistent with the existing identity-recovery boundary. It is not an invented two-administrator request/approval chain.

## Rollout and verification

Deploy the matching application and `AddWebAuthenticatorRecovery` migration together. Existing accounts keep `AuthenticatorRequired=false`; legacy domain records do not silently enable an operational factor. If legacy active confirmed TOTP records violate the new unique index, stop for an explicit data review rather than deleting evidence.

The private file store and OTP keyed-digest configuration must be available. Include encrypted authenticator secrets and their separately controlled encryption keys in the documented recovery plan. Do not log setup keys, URI secrets, verification codes or recovery-code responses. HTTP responses are `no-store`; mutations use the existing cookie/CSRF boundary.

Verification on the disposable SQL clone: 610 retained regression tests passed, zero failed/skipped, including TOTP vectors, opaque password rejection, browser/expiry/version/employment checks, code reuse, session refresh, enrollment and independent reset. A real SQL test executes the administrative SMS/TOTP union, saves factor/reset evidence and checks append-only protection. Web lint, formatting, TypeScript and production build passed. Evidence: `D:/CosmixPayrollVerification20261028/ceo-gap-fixes/web-mfa-regression.log` and `web-mfa-verify.log`. These results are synthetic/controlled verification, not employee enrollment or production SMS acceptance.

Implementation references: [Otp.NET](https://github.com/kspearrin/Otp.NET), [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238).
