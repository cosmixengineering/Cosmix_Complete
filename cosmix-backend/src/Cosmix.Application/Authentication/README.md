# Authentication

Owner: Developer 1. Source: SRS 4, AUTH-001 to AUTH-008.

The current services implement shared mobile phone/SMS OTP, web email/password plus registered-number
SMS MFA, rotated refresh tokens, password/phone/device recovery, current-state session validation,
logout and controlled initial credential/owner provisioning. There are no seeded credentials.
Read [Shared Authentication](../../../docs/Shared-Authentication.md) for current DTOs and config gates;
the historical foundation-only description no longer represents the implemented services.

Access/ retains paired permission and server-resolved resource scope evaluation. Fixed Admin/SuperAdmin
web MFA is required for bounded access management. The creation/grant/assignment writes require an
actor/action/target/payload-bound Idempotency-Key, a 24-hour protected receipt and fresh authorization
before replay. Required reasons are retained; see [Authentication and Access](../../../docs/Authentication-Access.md).

Mobile new-device sign-in creates a transactionally persisted own in-app alert. Notification template
text instructs the recipient to review their sessions; it contains no employee/device/session secrets.
Own inbox queries derive the recipient from the authenticated account, never from a requested recipient.
Live SMS/provider/device acceptance and actual first-owner provisioning remain deployment work and
require the approved target/settings. The PM's owner-creation deferral remains in force.

Application owns DTOs/use cases and provider contracts. API adapters own HTTP/cookies/CSRF/current
session transport. Infrastructure owns repositories and provider adapters. Never trust client roles,
employee membership, claim-only MFA or permission names to bypass current stored state.
