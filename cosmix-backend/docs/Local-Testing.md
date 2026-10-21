# Local authentication testing

Approved on 2026-10-08: a separate `Cosmix_Testing` database with synthetic
SuperAdmin, Admin and HR identities. This is not the real owner setup or a production deployment.

## Start and stop

From the backend folder in PowerShell:

```powershell
# First setup only: build, apply the existing migrations to a NEW database, seed fixtures.
.\scripts\Start-LocalTesting.ps1 -Initialize -SetupOnly

# Start the testing API; stop with Ctrl+C.
.\scripts\Start-LocalTesting.ps1
```

Use `https://localhost:5173/sign-in` with the React development server running.
The API uses `https://localhost:7240`; stop a previously running API on this port first.
Choose one configuration at a time. Ordinary Visual Studio startup still uses the
original user-secrets settings; the testing script does not change them.

Private files are under `%LOCALAPPDATA%\Cosmix\LocalTesting`:

- `Test-Accounts.json`: three distinct generated passwords and synthetic email/phone details.
- `settings.json`: separate test database connection, JWT/OTP keys and test-only limits.
- `keys`, `files`, `snapshots`: separate encrypted fixture storage; no production keys reused.

The directory is restricted to the local Windows user. Do not upload these files,
paste them into chat/Git, copy them to deployment, or import real employees into this database.
The SQL server is the same active local instance referenced by the existing development
connection, with the catalog changed to `Cosmix_Testing`. A named LocalDB pipe can change
after an instance restart; confirm the actual local instance before updating this private connection.

## Login

1. Open `Test-Accounts.json` locally and use the selected email and password.
2. Continue to verification, then press **Show test OTP**. Delivery may take a second.
3. Enter that code and sign in. Resend still requires the password and a 30-second cooldown.
4. Sign out before testing another account.

The accounts are `superadmin@example.invalid`, `admin@example.invalid` and
`hr@example.invalid`. Names and passport numbers are explicitly synthetic. Reserved
fictional phone numbers are used only by the in-memory adapter; nothing is sent to a carrier.
Admin/SuperAdmin are the existing fixed roles, web-only. HR is a dynamic limited role,
with mobile eligibility and no role/account-administration or fixed-Admin approval grants.
These are minimal authentication fixtures, not completed hiring forms or seeded payroll data.
HR/Admin submission evidence is marked as synthetic; the test owner is explicitly not a real CEO.

## Boundaries

- No fixed OTP, authentication bypass, public signup, production seeding or new database tables.
- Password hashing, random OTP creation, cryptographic verification, expiry, attempt limits,
  browser binding, CSRF, refresh rotation, logout and current permission checks are unchanged.
- The replacement sender is registered only with explicit private settings in `Development`,
  with the exact `Cosmix_Testing` catalog on local Windows-authenticated LocalDB. External
  SMS provider configuration is rejected. Production refuses testing settings at startup.
- The testing host refuses non-HTTPS, non-localhost and non-loopback requests. It requires
  exactly the three synthetic fixture identities at startup. This is a local developer aid,
  not a secure way to display OTPs for real users or host a shared staging environment.
- The OTP viewer is a separate, uncached, CSRF-protected route. It checks the issuing browser,
  approved fixture account/phone, security version, current challenge and successful dispatch.
  Consumed, revoked, expired and exhausted challenges cannot be revealed. In-memory codes are
  bounded and expire; a process restart loses them, so request a fresh challenge.
- No OTP/password is written to ordinary logs or normal authentication responses. The React
  helper is development-only and is removed from the production bundle.
- Seeding is an explicit command with an empty-database guard and one fixture transaction.
  Repeating setup on the complete fixture returns without reseeding or changing passwords.
  A partial or unexpected database fails closed. Never delete/reset it to hide a setup failure.
- `Cosmix_Development`, its user-secrets connection and the real first-owner workflow are unchanged.

## Verification on 2026-10-08

- Release build: zero warnings/errors; EF model matches the existing migration snapshot.
- New database: 12 migrations, 126 tables including EF history, 290 enabled/trusted foreign keys;
  `DBCC CHECKCONSTRAINTS` reported no violations.
- All three real HTTP login flows passed: password challenge, correct OTP, authenticated account,
  scoped permissions, refresh and logout. Wrong/reused OTP, another browser, missing CSRF and
  unknown-account OTP requests were rejected. HR received 403 on role administration.
- Browser: React HR login reached the actual authenticated account page using the .NET API.
- Production startup with testing settings was rejected; repeated setup did not reseed.
- Setup refused a configuration pointing at `Cosmix_Development` before touching the database.
  A non-local Host header was rejected. The original user-secrets file hash and original
  database counts (12 migrations, 126 tables, zero business rows) were unchanged.
- Resend after cooldown created a new challenge and invalidated the previous code/viewer.
  Five incorrect attempts exhausted the challenge; even the correct code was then rejected.
- Frontend lint, formatting, TypeScript and production build passed. Test OTP strings/routes
  were absent from production JavaScript.

Real SMS delivery, Jazz integration, mobile application UI and production owner provisioning
are not verified by this setup. The fixed testing limits are not approved production policy.
Temporary API verification scripts were kept outside the repositories; no test project was added.
Focused independent read-only code review reported no remaining important findings.
