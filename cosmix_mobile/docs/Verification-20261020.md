# Mobile verification — 20 October 2026

The owner explicitly reauthorized builds and testing after the code-only phase.
This record separates compiled implementation, synthetic behavior, live service
boundaries and acceptance that needs a real employee/device.

## Verified

| Check                       | Evidence                                                                                                                                                                  |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Locked native dependencies  | `npm ci --ignore-scripts --no-audit --no-fund` completed                                                                                                                  |
| Mobile source               | `npm run verify`: lint with zero warnings, formatting and TypeScript passed                                                                                               |
| Backend                     | Release build had zero warnings/errors; final Release publish including all new history routes completed                                                                  |
| Android                     | x86_64 debug build succeeded: 245 tasks, 19m 5s; APK installed successfully                                                                                               |
| Emulator                    | Actual app loaded through Metro; login form disabled empty input, accepted a reserved synthetic number, and displayed the real backend's safe unavailable-login message   |
| Native API connection       | App called HTTPS `10.0.2.2:7241` successfully without a TLS bypass; absent SMS returned HTTP 503                                                                          |
| Core behavior               | 29/29 synthetic checks: sessions, logout races, face gating, exact retry, Check Out behavior and receipt semantics                                                        |
| Self-service behavior       | 22/22 synthetic checks: own APIs/permissions, full history paging, exact leave fractions, unavailable balances, request reconciliation and dates                          |
| Backend financial authority | 11/11 synthetic checks: self identity, explicit grant, other-employee denial, revocation, audit failure, pagination and existing overview behavior                        |
| Private evidence adapter    | 6/6 synthetic OS-port checks: scoped picker copies, cache path rejection, size limits, content header rejection and safe cleanup                                          |
| Live HTTPS/API boundaries   | 28/28: readiness, narrow host allowlist, OpenAPI routes, unauthenticated denial and disabled OTP contract                                                                 |
| Live SQL                    | 4/4 read-only checks using a fresh nonexistent employee ID: actual EF SQL translation/paging for payroll, requests, agreements and profile, with no unowned rows returned |
| Face provider heartbeat     | Ubuntu CompreFace `/core/status` returned HTTP 200                                                                                                                        |

The SQL metadata check found 24 applied migrations and the relevant tables on
the configured local target. No employee fixture, policy, grant, migration,
approval, payroll entry or attendance record was written by these checks.

## Fixes found during this pass

- Replaced recent-ten advance/loan history with self-only paged requests and
  agreements, including fully repaid agreements and stable sorting.
- Added paged own payroll handoff history to avoid the recent-six limit.
- Added full raw Check In/Check Out history using the existing own-record API.
- Added independent detailed leave-balance selection to the history screen;
  applying/roster permissions are not needed merely to inspect remaining days.
- Removed inline-style/source-check failures and corrected the cleanup callback.
- Fixed the dedicated development preview's hostname configuration. It binds
  loopback and permits only localhost, 127.0.0.1 and the emulator's 10.0.2.2.
  An unrelated HTTP hostname remains rejected; certificate validation remains on.

The emulator's System UI briefly reported an ANR during the heavy native build.
Choosing Wait recovered it; the subsequent app login and HTTPS smoke test
worked. This was not counted as a successful biometric/device test.

## Still not accepted

- Successful real SMS/OTP employee login: provider remains absent and the normal
  backend fails closed with `authentication_unavailable`.
- Authenticated employee screens against populated real records, including the
  employee's actual configured leave policy/balance and approval transitions.
- Native evidence selection/upload with scanned documents.
- Consented employee enrollment, real face/liveness success, print/replay
  resistance, physical GPS/geofence acceptance and a real Check In/Check Out
  pair. Simulated emulator location remains rejected by design.
- iOS, production signing/distribution and production attendance readiness.

No mock OTP, production login bypass, enrollment substitute or accepted punch
was introduced to get around these dependencies. Independent source review
found no remaining Important/Critical issue in the new history changes; that
is source evidence, not a guarantee about untested employee workflows.

## Artifacts and running preview

Evidence is outside Git at
`C:/Users/Huma.Khan/.codex/artifacts/cosmix-mobile-20261020/verification`.
It contains the source/build logs, temporary focused harnesses, HTTP/SQL logs
and actual emulator login screenshots.

Earlier attendance/self-service snapshot, `CosmixMobile-debug-x86_64.apk` SHA256:
`55A112E8F48B72001308931DDD1271AC531A068AEFDDC7D90C51D3FFFCFF1FE4`.
This is an emulator debug APK using the current Metro source and dedicated
loopback API preview. It requires Metro/adb reverse and is not a standalone
physical-phone or production release. Private TLS keys and passwords remain
outside the repository. No commit, push or release deployment was performed.

## Later central History and bottom navigation change

- Added one History tab with permission-aware category buttons for eleven own
  record types. Categories change in place and reset their date/page/detail
  context. Existing service workflows reuse the same record component.
- Check In/Out receipts now display full server-recorded date, year, time and
  seconds in Asia/Karachi. The UI identifies receipt time separately from the
  server's acceptance/review result. It does not invent paired sessions or hours.
- Five actual workspace tabs use the owner's rounded gray tray, white circular
  buttons, lime selected state and custom outline SVG icons. Attendance is in
  the middle. The bar respects safe areas and hides while the keyboard is open.
  Attendance verification locks tab changes; request forms remain above the tabs.
- Source checks pass: lint with zero warnings, Prettier and TypeScript.
- Fresh temporary checks: 7/7 history/permission/time-format checks, 7/7 synthetic
  React UI checks, 29/29 core checks and 22/22 service checks. The React harness
  substitutes native ports and server data; it is not employee acceptance.
- Review found and fixed two issues: missing access-retry controls after a
  transient permission refresh failure, and late details reopening after a page
  or filter change. The late-detail regression failed before the fix and passed
  afterward. Focused follow-up source review found no remaining actionable issue.
- The native preview additionally exposed a tab-render callback incorrectly
  invoking a hook-bearing component as a function. Reproduced it in a focused
  regression (one check failed before correction, 7/7 passed afterward); the navigator
  now returns a React element through the renderer callback. Circle width and
  height are explicit equal dimensions after inspecting actual native layout.
- Native visual acceptance used a throwaway entry/provider entirely outside Git,
  with a visible `SYNTHETIC UI PREVIEW · NO SERVER WRITES` banner. It rendered
  the actual tab navigator, icons, History screen and shared record controls.
  On emulator-5554, inspected the five circular buttons and lime selected state,
  switched Attendance/History, changed Leave within History, and scrolled the
  synthetic Check Out/Check In receipts to verify `20 Oct 2026, 18:04:05` and
  `20 Oct 2026, 09:01:12`. XML bounds confirmed equal circle width/height
  (within one physical pixel of rounding). Screenshots/XML stay in the external
  `native-preview` folder. This fixture has no real tokens or network writes
  and does not change the production authentication flow.
- After the visual check, stopped the external fixture server, restored the
  normal project Metro server and launched the actual anonymous login screen.
  Checked the normal 6,023,572-byte application bundle excludes both the fixture
  banner and provider. No ReactNativeJS/AndroidRuntime error entries appeared
  for the restored process. The normal debug preview remains running.
- Android x86_64 debug rebuild including the SVG native module succeeded in
  8m 29s (265 tasks: 39 executed, 226 up to date), and APK installation succeeded.
  Existing upstream Gradle/path deprecation warnings remain.

Current native debug snapshot: `CosmixMobile-navigation-debug-x86_64.apk`, SHA256
`429BFEB4F4BD518BFF4575F6A35626F43F71E6477EDCEB0A8464D6AB54C3E4FA`.
Like the earlier debug APK it requires Metro and adb reverse; it is not a
production or standalone phone build. New evidence is in the same external
verification directory. The employee/SMS/physical-biometric acceptance limits
above still apply.

## Later login and OTP reference design

- Replaced the two authentication layouts with shared lavender/white rounded
  cards, indigo buttons, Pakistan SVG flags and a fixed +92 phone prefix. The
  login is phone-only. Need help opens real account/SMS/device guidance for HR;
  no sign-up, email selector, social-provider buttons or fake auth was added.
- Pakistan local/national/international mobile formats normalize to the existing
  E.164 API contract. Urdu/Arabic digits are supported. The format checkmark
  confirms input format only; server eligibility remains authoritative.
- Six OTP cells share one secure native input with paste/autofill metadata,
  default masking, explicit Show/Hide, accessible labels and a phone-edit action.
  Successful resend clears the digits and remounts the field for its new
  challenge. Cooldown/expiry are checked inside handlers as well as in disabled
  controls. Existing real challenge, request and verification routes remain.
- Native-driver card/press/orbit animations respect Reduce Motion and pause
  on background/blur. Decorative loops and accessibility subscriptions are
  cleaned up. Authentication transitions and the help modal also respect the
  motion preference. No new dependency was needed.
- Temporary checks: 4/4 phone/OTP input cases, 7/7 authentication UI/controller
  cases, 29/29 existing core regressions and 7/7 workspace UI regressions passed.
  These use synthetic native ports and OTP responses, not real SMS delivery.
- Final `npm run verify` passed: zero-warning lint, Prettier and TypeScript.
  Android x86_64 `assembleDebug` passed in 6m 16s (265 tasks: 4 executed, 261
  up to date). This change adds JavaScript/UI only; no native dependency or
  signing change. The debug APK continues to use the normal Metro bundle.
- Inspected the actual normal login in emulator-5554, opened/dismissed Need
  help, and checked phone keyboard/focus with an incomplete number (Send code
  remained disabled). No SMS was requested in that keyboard check. Screenshots
  are external `auth-login.png`, `auth-help.png` and `auth-keyboard.png`.
- The normal Metro bundle compiled the new UI and was checked to exclude the
  external preview/provider. During editing, Fast Refresh logged old mounted
  hook-signature errors as those modules changed; the UI recovered and the
  native help/keyboard interactions worked afterward. A batched process
  force-stop/start command was blocked by automatic review without a detailed
  reason; verification continued through the existing app's hot reload.
- Full native OTP/autofill and successful employee authentication remain
  unaccepted without a configured SMS provider. No OTP route bypass was added
  to obtain a native verification-screen screenshot. Source review found no
  remaining actionable issue; that does not replace device acceptance.

Authentication-specific evidence is outside Git in the existing verification
directory: `auth-input.log`, `auth-ui.log`, `core-auth.log`,
`workspace-auth.log`, `mobile-verify-auth-final.log`, the normal Metro bundle
and `android-build-auth.log`.

## Later company logo and login interaction polish

- Replaced only the login phone-lock illustration with the supplied company
  logo. The built-in image editor removed its background using the supplied
  logo and the matching, clearer existing web asset as references. The new
  bundled PNG is 1947 x 808, 562,013 bytes, with transparent negative space.
  The web source asset was read only. Logo SHA256:
  `925A930CEF6CF4663CD5D4265F94E2D29634A1177FC8B0B8371B9F34296E2D42`.
- Shared native-driver controls add a logo reveal/float, restrained ambient
  movement, phone-focus glow/border/label transitions, animated number-format
  feedback and button gradient/press feedback. Reduce Motion keeps clear
  static focus feedback and suppresses decorative motion and native ripples.
  Animation cleanup and background/blur gating use the existing motion context.
- Native inspection found and corrected two Android rendering issues: the
  image needed an explicit transparent background, and SVG percentage dimensions
  inside the padded action produced incomplete fill. The gradient now measures
  an absolute wrapper and uses numeric SVG dimensions without layout cycles.
  Inspected the corrected normal application in emulator-5554 with clean logo
  transparency, full button fill, focused/blurred field states and no layout
  shift. Also inspected the phone keyboard with the focused glow visible. No
  SMS request or employee data was used for this visual check.
- Temporary authentication UI/controller checks passed 10/10, including the
  measured gradient, transparent image style, focus feedback, Reduce Motion,
  cleanup and existing OTP request/cooldown/expiry guards. Native ports and OTP
  responses are synthetic in that harness. Independent focused source review
  found no actionable regression, including the final rendering corrections.
- Final `npm run verify` passed: zero-warning lint, Prettier and TypeScript.
  The updated documentation also passed its focused formatting check.
- Source verification remains separate from authenticated employee acceptance.
  This change adds no native dependency; the installed debug app consumes the
  updated normal Metro source. Real SMS, enrollment and physical-device
  biometric acceptance retain the limitations recorded above.

External evidence: `brand-motion-final.log`, `mobile-verify-brand-final.log`,
`brand-idle.png` (normal native login), the focus captures and
`index-brand.android.bundle` in the existing verification directory. Earlier
intermediate screenshots contain rendering defects or the development refresh
banner and are retained as diagnostic evidence rather than final previews.
