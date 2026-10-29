# Cosmix Mobile

React Native CLI + TypeScript employee app, using the existing Cosmix .NET backend.
The approved first slice is phone/SMS login, dashboard, Check In with server face
and liveness verification, and location-based Check Out. Product direction and
future supervisor/site staff/rider panels are recorded in
[docs/Product-Direction.md](docs/Product-Direction.md). Customer features are deferred.

The source now also includes a shared Employee Services hub: leave and policy
balances, advances/loans and repayment details, resignation intake, allowances,
vehicle activity/claims, attendance corrections, overtime, encashment, monthly
attendance, full punch/payroll history, profile/assigned items, finances and inbox. See
[docs/Employee-Self-Service.md](docs/Employee-Self-Service.md) for contracts and
access requirements. The owner subsequently authorized builds and testing.
The 26 October verification passed source checks, 117 controlled regression checks,
Android JavaScript bundling, and normal native login against the isolated local
backend with its synthetic OTP delivery sink. The existing Android debug APK ran
the current source through Metro. Native versioned consent, dashboard, own profile
and empty attendance history were verified; real SMS delivery and biometric
attendance acceptance remain unverified. See the verification section below and
[the earlier build record](docs/Verification-20261020.md) for evidence and limits.

The authenticated workspace now has five tabs: Home, Services, Attendance,
History and Profile. The shared circular bottom bar follows the owner's visual
reference. History provides permission-aware category buttons for Check In/Out,
leave, advances/loans, allowances, overtime, corrections, monthly attendance,
payroll, payments, encashment, vehicle activity and resignation. Dates and pagination reach
older records. Check In/Out receipts show the full server-recorded date and time,
including year and seconds, explicitly in Asia/Karachi. The result remains
separate from the recorded time; pending/rejected receipts are not success.
Request forms remain above the tabs, and attendance locks tab changes while
verification/submission runs. Profile uses the actual own-profile endpoint.

Payment history groups each salary entitlement by payroll period, with separate
confirmed, posted, pending, failed and reversed amounts. Each period has its own
payment-entry pager; the totals cover all its entries. Posted payments remain
awaiting confirmation. The app uses the employee-only financial endpoints and
does not display banking details or payment evidence. See the
[payment history contract and acceptance limits](docs/Employee-Self-Service.md#unified-history-and-payments-28-october-2026).

Login and verification use shared reference-inspired authentication controls:
Pakistan/+92 phone entry, rounded white cards, indigo actions, a Need help sheet
and six OTP cells backed by one native secure/autofill-capable input. Local 03…,
national 3…, +92/0092 paste formats and Urdu/Arabic numerals normalize before
the existing OTP API call. Code visibility is an explicit user choice. Resend
cooldown/expiry and single-flight request guards remain enforced. Motion
respects Reduce Motion and pauses on background/blur; no new dependency or
alternate authentication provider was added.

The login illustration now uses the supplied Cosmix Engineering logo as a
bundled transparent HD PNG. Shared controls add logo entrance/float motion,
soft ambient movement, animated field focus/format feedback and gradient actions
with press feedback. Motion remains gated by the existing accessibility and
screen/application lifecycle controls.

## Structure

Shared source folders serve all approved employee roles; .NET permissions and
record scopes remain authoritative.

| Folder                                       | Responsibility                               |
| -------------------------------------------- | -------------------------------------------- |
| `src/app`                                    | App composition, services and privacy cover  |
| `src/auth`                                   | OTP and protected session lifecycle          |
| `src/screens`                                | Login, dashboard, attendance and services    |
| `src/navigation`                             | Typed authenticated/anonymous navigation     |
| `src/components`, `src/theme`                | Shared accessible controls and visual tokens |
| `src/hooks`                                  | Scoped server reads and mutation state       |
| `src/services/api`                           | Fixed HTTPS transport and safe errors        |
| `src/services/attendance`                    | Face/capture/proof/punch coordination        |
| `src/services/selfService`, `documents`      | Self APIs, exact request retry and evidence  |
| `src/services/camera`, `location`, `storage` | Native adapters                              |
| `src/types`, `src/utils`                     | Contracts and pure validation                |
| `android`, `ios`                             | Native projects                              |

React Native is pinned to 0.87.1. Dependencies are pinned in package-lock.json.
The development identifier is `com.cosmix.mobile`; production signing and
identifiers must be finalized before distribution. The existing branch is
`codex/mobile-foundation` in [Cosmix-Mobile-App](https://github.com/cosmixengineering/Cosmix-Mobile-App).

## Running the Android development app

The dependency lock was installed with lifecycle scripts disabled. The document
picker is included in the verified Android native build; actual document
selection/upload still requires authenticated device acceptance.

Use Node24, JDK17, the Android SDK and an Android emulator.

```powershell
npm ci
npm run verify
npm start
```

In another terminal, with platform-tools on PATH:

```powershell
adb reverse tcp:8081 tcp:8081
npm run android -- --active-arch-only
```

Metro listens only on host loopback. Android debug code uses `localhost:8081`
through adb reverse. React Native debug tooling may show a nearby-network
permission prompt on Android 17; the release dependency does not declare that
debug permission. See the official [React Native setup](https://reactnative.dev/docs/set-up-your-environment).

`src/config/environment.ts` contains the public emulator API target:
`https://10.0.2.2:7241`. This is the host laptop's .NET API, which calls the
Ubuntu face provider. It is not a direct CompreFace/model endpoint. Start that
API with the existing normal development configuration and an HTTPS certificate
whose SAN includes 10.0.2.2. Certificate keys/passwords remain outside this repo.
For this dedicated loopback preview, set process-local `AllowedHosts` to
`localhost;127.0.0.1;10.0.2.2`. The default backend accepts only localhost and
otherwise rejects the emulator hostname with HTTP 400. Keep the listener bound
to loopback and retain the hostname allowlist; do not use a wildcard.
The checked-in **public** local CA is trusted only by the Android debug variant,
only for that emulator host. Release configuration trusts system CAs and denies
cleartext. No `trusty`, permissive trust manager or hostname bypass is enabled.

On this Windows host Java's AF_UNIX wakeup pipe failed with `Invalid argument:
connect`. A process-local `jdk.net.unixdomain.tmpdir` pointing to an existing
regular file makes OpenJDK's documented-source fallback use a TCP pipe. This was
used only for local Gradle invocation; no machine-wide Java setting was changed.
If needed, set JAVA_TOOL_OPTIONS for that build invocation to
`-Djdk.net.unixdomain.tmpdir=<absolute path to android/gradle.properties>`.
[OpenJDK PipeImpl source](https://github.com/openjdk/jdk17u/blob/master/src/java.base/windows/classes/sun/nio/ch/PipeImpl.java)
contains the fallback logic.

## Security and attendance behavior

- Login uses registered phone + real server OTP. No SMS provider is configured
  yet, so the normal backend rejects login with 503 `authentication_unavailable`.
  No mock OTP, shared password, hidden account or face-login substitute is added.
- Access tokens stay in memory. Refresh and installation secrets use the OS
  Keychain/Keystore. Refresh is serialized; uncertain rotation requires a fresh
  sign-in rather than replaying a potentially consumed secret.
- The server owns permissions, device approval, enrollment, schedules, location
  policy, face matching, liveness and punch acceptance. The mobile app never
  contains CompreFace administrator credentials or an API key.
- Check In first reads fresh location and calls the server location preflight
  against the assigned dated site/office boundary. Only an allowed result opens
  the camera: server challenge, normalized JPEG, protected upload, server
  verification, then a second fresh location for final server validation. A failed/expired
  face result cannot submit a punch. Check Out does not capture a face.
- Temporary camera files stay in app cache and are cleaned after capture. No
  image is saved to the gallery or persisted in app state. Cache cleanup after
  an OS crash is not guaranteed. JavaScript strings are not securely erasable.
- Uncertain punch submissions keep their exact key and body in secure storage.
  An explicit retry reconciles that submission; it never silently creates a new
  punch. Only an explicit `Accepted` receipt is presented as ordinary success.
  Review, rejection and exception results remain distinct.
- On authenticated entry, the current published location notice must be checked
  and accepted explicitly for its displayed version. Foreground and attendance
  actions recheck policy; unavailable checks offer retry or sign-out. No active
  notice blocks the workspace and attendance callbacks until HR publishes one.
- Camera and precise foreground location are requested when needed. No
  background location or microphone permission is requested. Known simulated
  location is rejected, including in an emulator. Session checks after native
  permission and camera processing prevent further GPS or image work after
  sign-out; temporary image cleanup still runs.
- Background views have a privacy cover. Android release windows additionally
  block screenshots. Android backups are disabled. No tokens, OTPs or face
  payloads are logged. A production API URL and release signing are still pending.

## Verification and remaining acceptance

On 26 October 2026, the current original checkout passed `npm run verify`, all
117 external controlled checks, and the Android production JavaScript bundle.
The isolated Android 37 emulator used the existing native debug APK with current
Metro source and certificate-validated HTTPS to the local API clone. Normal
synthetic OTP login, explicit version 1 acceptance, exact version 2 reacceptance,
the no-active-policy gate, dashboard, own profile and empty filtered attendance
history passed. The clone recorded one consent per displayed version. Camera
and precise/coarse location permissions remained ungranted during these checks.
Evidence and source hashes are outside Git in
`D:/CosmixFirst11Verification/final-acceptance/mobile/report.md` and
`continuation-source-hashes.json` beside it.

The later continuation reproduced and fixed session changes during location and
camera permission dialogs and camera processing. Its 34 additional checks compose
the actual workflow, HTTP client, camera/location adapters and history client with
controlled native modules and transport. They cover temporary file cleanup,
preflight rejection, failed face/final GPS, exact uncertain-submit retry and
overnight history dates. These last guards passed source checks and bundling;
the earlier native smoke was not rerun after them.

The native fixture had no verified face enrollment, so attendance correctly
remained unavailable. No enrollment was fabricated and no accepted biometric
punch, real camera liveness, print/replay rejection or physical GPS result is
claimed. GPS-before-camera ordering and rejection paths were exercised using
the actual coordinator and location adapter with controlled API/native ports.
OTP delivery used only the external local verification host's synthetic sink;
the production SMS provider is still absent. A fresh native rebuild was
interrupted to free host resources and is not claimed as a new build pass.

See [docs/Employee-Attendance-Implementation.md](docs/Employee-Attendance-Implementation.md)
and [docs/Verification-20261020.md](docs/Verification-20261020.md)
for implementation detail and earlier evidence. A successful source check or debug build does not prove
real biometric attendance acceptance. Real login requires an SMS provider and
an eligible employee/device. Full acceptance also needs HR enrollment, approved
attendance policy/roster/site settings, and consented physical-device camera,
print/replay and location testing. iOS builds require macOS/Xcode/CocoaPods and
have not been verified on this Windows machine.

There is no permanent test suite or generated screenshot/artifact folder in the
repo, following the project instruction. Temporary regression checks and build
outputs stay outside Git. No commit, push or release deployment is included.

## Dependency advisory

Prior attendance-slice audit, checked 20 October 2026: 21 high dependency entries trace to the same
[braces stack-exhaustion advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm),
with no patched version reported for the installed 3.0.3. The chain runs through
CLI/Metro file-pattern tooling and React Native peer dependencies. Runtime
exploitability has not been assessed. The suggested forced fix downgrades
React Native/CLI incompatibly, so it was not applied. Do not accept untrusted
glob patterns or expose Metro publicly; review this risk before production.
This audit was not rerun after adding the document-picker dependency metadata.
