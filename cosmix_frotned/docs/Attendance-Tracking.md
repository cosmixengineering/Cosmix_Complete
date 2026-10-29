# HR attendance tracking

Route: `/hr/attendance/tracking`. Sidebar: **Attendance Tracking**, directly after Attendance in the HR and Admin navigation.

## Using the page

1. Select a month and search by employee name or employee number. The directory shows approved onboarding records, ten per page.
2. Read the monthly matrix. Select a date header for that day's log, or an employee/date cell for its detail. The employee column stays visible while scrolling across dates.
3. Switch to **Daily log only** for a compact operational view. **View day** opens the retained record explanation and, when permitted and available, current session start/end times.
4. Use **Refresh** or opt into **Refresh every minute**. Automatic refresh runs only while the tab is in the foreground. Summary counts explicitly cover the current directory page, not the company total.

## Data and authorization

- Existing authenticated employee directory, attendance calendar and session-audit APIs supply the data. No mock employee rows, random attendance, hardcoded rates, biometric success or GPS positions are used.
- Team tracking requires All-scope `hr.employees.read` and `attendance.summaries.read`. Session details additionally require `attendance.punches.read`. Server authorization remains authoritative; no grants are created.
- At most ten employee calendars are loaded, with two HTTP reads in flight. Requests cancel on navigation/selection changes. No automatic retry loop or background all-company scan.
- Each calendar must match the requested employee, dates, full requested coverage and unique day identities. Malformed/mismatched responses become unavailable rows, never fabricated attendance.
- Future dates are not queried or marked absent. Missing locked summaries show `N/A`; counts/hours are unavailable rather than zero. Open/recorded sessions without policy classification are `IN`/`REC`, not assumed present. `P`, `L`, `A`, leave, holidays and rest days come from the server's calculated classification.
- Session evidence is loaded only on demand. Latest visible revisions are shown; additional audit pages are disclosed rather than silently totalled. Calculated overtime is not labelled approved or paid overtime.
- There is no bulk presence, waive-late, import or payroll action. Site/department filters and GPS/biometric live counters from the reference screenshot are not invented when the existing directory/calendar contract does not supply them.

## Verification on 2026-10-28

- Actual-source in-memory harness: **25 checks passed**, including classifications, missing/future data, unavailable durations, two-request concurrency, duplicate employee/date rejection, wrong employee/date rejection and cancellation. Transport was synthetic; these checks are not SQL acceptance.
- Live authenticated HR browser: directory returned 12 approved employees; page one showed ten and page two two. Name search narrowed the log to one employee. Date/cell selection opened the matching day detail. The separate daily-log view worked.
- Existing October locked records returned explicit unavailable summaries and rendered without the previous exception. The exact previously reported employee/month also rendered 31 missing-history rows on the existing employee screen, with no inferred totals or recalculation of locked history.
- `npm run verify` passed lint, Prettier, TypeScript and Vite production build. The existing large main JavaScript bundle warning remains.
- This pass performed reads in the authenticated application, not attendance mutation, real punch capture, biometric, SMS, payroll or approval acceptance. No migration or historical-data repair was applied.

## Changed files for this feature

- `src/modules/attendance/api/tracking-api.ts`
- `src/modules/attendance/pages/AttendanceTrackingPage.tsx`
- `src/styles/attendance-tracking.css`
- `src/modules/attendance/index.ts`
- `src/app/router/AppRoutes.tsx`
- `src/layouts/AppLayout.tsx`
- `src/layouts/AdminLayout.tsx`
