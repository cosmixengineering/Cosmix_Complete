# Configured daily Attendance calculation

Local implementation, 12 October 2026. The registered calculator resolves the actual published
Attendance policy and verified leave effects, builds an immutable in-memory source snapshot,
and produces ordinary payable minutes, late minutes, separately approved OT minutes and status.
It always returns `PayrollReady=false`. No compensation record is created or changed, no salary
is withheld, and no monetary deduction or payroll settlement is produced. The required late
deduction configuration is retained by policy; the daily engine only identifies late occurrences
and the configured reset period. Month locking retains its financial readiness blockers.

`AttendanceCalculationSnapshotProducer` runs within the summary caller's authorization transaction.
It compares the supplied factual fingerprint with the actual canonical session revisions and
reuses the OT repository's accepted-assessment, consumed face-proof and correction-decision checks.
Additional raw bindings pin employee, business date, employment assignment, site, shift, site roster,
temporary change and holiday revision. Historical lifecycle/assignment coverage and cross-date duty
overlap are checked across each occupied interval. Manual approved duty is validated against its
protected proposal and exact applied session; its failed raw Check In remains unchanged. Initial
correction calculation does not require a prior summary link, avoiding a circular dependency.

The leave producer must report `Complete` even for an empty result. Pending/conflicting effects
block calculation. Approved allocations retain request, allocation, decision, policy and shift
version identities; the calculator checks the exact pinned shift and UTC interval. Duty/leave
overlap, unknown pay treatment, nonintegral allocation durations and out-of-window leave fail closed.
Missing policy, roster or holiday-calendar interpretation returns no payable value or absence status.
Future dates and a closed-duty absence before the scheduled end remain unavailable. A raw session
with a latest `AcceptedWithException` assessment remains unavailable for ordinary payable time;
a time correction does not implicitly approve its location exception. Explicit approved manual duty
is separate and may reference the retained failed Check In without turning it into a successful raw event.

The pure engine preserves exact UTC ticks and projects minutes once after summing the total:

- Both explicit time bases exclude pre-shift duty without an approved early-reporting exemption.
  `ActualElapsedDuty` includes verified time from shift start onward; `ScheduledWindowOverlap`
  uses only overlap with the scheduled clock window.
- The explicit unpaid-break option deducts the configured daily duration once, after combining
  ordinary duty and paid leave. It does not infer a lunch clock interval or deduct a break per session.
- Thresholds mean strictly after the configured number of seconds. Paid/unpaid leave covering
  the beginning or end of the shift adjusts the required reporting/departure boundary.
- Fixed half-day credit is the minimum of available scheduled paid capacity and the greater of
  configured half-day credit or approved paid leave. Available capacity uses configured scheduled
  minutes, subtracts the selected unpaid break once, then excludes unpaid leave. Schema 2 requires
  the explicit `half_day_total_includes_paid_leave` choice; legacy schema 1 cannot guess that combination.
- Confirmed hourly arrival takes precedence over half-day credit. Rest-day work without an
  authorized schedule remains unavailable when actual-time credit is selected.

Approved OT is read through the existing service's protected proposal/decision/formula validator.
The calculator retains only IDs, approved ticks, exact eligible intervals and a digest in its own
snapshot. It never returns the retained base salary or amount through ordinary daily results.
Pending OT blocks calculation; changed session/assessment/roster/employment evidence requires
reconciliation. Duplicate approved intervals conflict. Verified awarded OT is excluded from ordinary
elapsed payable time, preventing duplicate interpretation; it does not get deducted twice from
the scheduled-window basis. Award validation uses the original retained compensation evidence,
not today's salary. Carry-forward financial interpretation and monthly deduction aggregation remain
separate required producers.

The public `InputProvenance` is a SHA-256 digest of ordered, versioned inputs, including exact source
revisions and policy/leave/OT evidence digests. It contains no private file reference, medical payload,
salary amount or calculation money. Existing saved legacy summaries remain readable through the
unchanged legacy fingerprint path; new provenance fields are omitted when null.

Standalone checks are `VerifyDailyCalculation.cs` (pure rules plus real snapshot producer/calculator
with synthetic module repositories and protected evidence) and `VerifyDailyCalculationQueries.cs`
(actual EF SQL translation with a connection-denying interceptor). They do not create test projects,
connect to SQL, write employee data or provide authenticated database/concurrency acceptance.
Executed results are recorded in the coordinated implementation verification record after running.

Executed locally: `VerifyDailyCalculation.cs` passed 67 assertions covering exact threshold ticks,
overnight time, daily breaks, fixed half-day/partial leave composition, genuine producer absence
readiness, future-day guards, raw/leave provenance, manual correction evidence, pending/stale/corrupt
or duplicate OT, stable input digests, and the nonfinancial public boundary. These are synthetic
in-memory checks, not real-provider, private-storage durability or authenticated SQL acceptance.
The production query translation harness passed ten checks with zero database connection attempts.
It caught and drove correction of an unsupported EF collection projection before handover.
An initial Release solution build passed with zero warnings/errors. The coordinated final Release
build covers the subsequent assessment projection and tightened source guards.
