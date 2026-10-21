# H Export and Reminder Continuation

15 October 2026. Scoped continuation after the temporary source freeze was
explicitly released. A02 execution was declined; no SQL, private-storage,
fixture, host or provider operation is authorized or performed here. These source
edits invalidate historical A02 source hashes. The historical packet is unchanged
and was not regenerated.

## Implemented Behavior

- Daily and monthly attendance exports retain the repository's authorized total
  before pagination. Both CSV and XLSX now carry that total through the tabular
  conversion, including empty later pages. The requested page still determines
  exported rows; this does not silently export the full matching population.
- Certification reminders select active revisions effective on the sweep date
  and exclude revisions superseded by any effective successor for the same
  employee. An effective revoked or suspended successor suppresses its prior
  active revision. A future successor does not suppress today's current revision;
  a renewal outside the reminder window still supersedes the old expiry.
- XLSX cells normalize control characters other than CR/LF to spaces, matching
  the existing CSV control policy. Invalid UTF-16 becomes the Unicode replacement
  character; XML-forbidden U+FFFE/U+FFFF become spaces. Valid Unicode, supplementary
  characters, leading/trailing spaces, and CR/LF/CRLF survive XML parsing. Cells
  remain inert inline strings; formula-looking text never becomes a formula.
  Metadata and headers use the same cell serialization.

Existing reminder timing, account filters, transactional pages, outbox identities,
retry cadence and notification dispatch gates are unchanged. Previously queued
events remain history; the generic dispatcher does not re-evaluate later source
changes. No financial value, legal authority, access shortcut or policy default
is introduced.

## Verification Sources

All previous standalone assertions are retained. No test project was added.

- `VerifyHrReportXlsx.cs`: independent ZIP/XML parsing, package relationships,
  worksheet dimensions and cell addresses beyond column Z, literal formulas,
  Unicode/control handling, exact line endings, empty pages and full metadata.
- `VerifyHrReportBoundary.cs`: adds XLSX source/export permission checks, server
  selection and export audit checks, revocation during private audit IO, and
  CSV/XLSX totals on an empty later daily/monthly page.
- `VerifyHrReportResults.cs`: adds parsed daily/monthly XLSX pagination, totals,
  excluded employee checks, independently expected monthly values, nullable
  missing quantities and explicit retained zero values.
- `VerifyHrReportsRemaining.cs`: adds parsed XLSX values for the other twelve
  projections, independent expected values, complete column/row parity, foreign
  source exclusion, totals and empty later pages.
- `VerifyDocumentExpiryReliability.cs`: adds effective certification chains,
  future transitions and repeat-sweep deduplication. A new synthetic failure
  occurs after persistence but before commit acknowledgement; replay must retain
  committed pages, deduplicate them and finish the remaining page.

## Executed Evidence

Frozen dependency input:
`D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release`.
No product project or solution build ran. Standalone compiler artifacts are in
unique verification directories, outside shared `bin`/`obj` and parent outputs.

- Baseline XLSX: 74/77 checks, with expected failures for line-ending fidelity,
  XML-invalid controls and malformed UTF-16. Current exporter source compiled
  directly with the same verifier: **81/81 passed**. Extra successful assertions
  execute after the previously throwing exports become usable.
- Baseline boundary: the new daily CSV total assertion fails on the frozen
  Application DLL, which still contains the old conversion.
- Baseline reminder: 55/58 checks. All three new certification selection/replay
  expectations fail, while the ambiguous-commit retry checks already pass.
  The corrected worker source compiled directly with that same verifier passes
  **58/58 checks**, including the existing paging, rollback, cancellation,
  producer/dispatcher and SQL-translation cases. No worker host was started.
- Other twelve reports: **398/398 passed**, with current scoped repository sources
  compiled into the standalone executable and frozen application dependencies.
  This exercises the unchanged twelve service conversion paths and their XLSX
  exports; it does not verify the new daily/monthly conversion against a rebuilt
  Application DLL.
- Updated boundary, attendance-result and remaining-report verification sources
  compile successfully against the frozen dependencies. Integrated verification
  of the changed service requires the parent's fresh Application assembly.
  Executing the updated attendance-result suite against the old Application DLL
  gives **198/204**: exactly six expected XLSX-total failures, covering daily and
  monthly pages 1, 2 and 3. The expanded boundary suite again reaches the expected
  daily CSV-total failure after its XLSX authority/revocation checks.

Retained artifact locations:

```text
D:\CosmixVerification\part-c-parent-20261013\managed-38c66c3df046492da147d248b7e6313f
  VerifyHrReportXlsx.dll                         baseline
  VerifyHrReportXlsx-current-source.dll          current exporter source
  VerifyHrReportBoundary-scoped-checks.dll       frozen Application dependency
  VerifyHrReportResults-scoped-checks.dll        current repository / frozen Application
  VerifyHrReportsRemaining-scoped-checks.dll     current repository / frozen Application
D:\CosmixVerification\part-c-parent-20261013\managed-4c005b32023d4578a33b795f54b9018b
  VerifyHrReportBoundary.dll                    first baseline total regression
D:\CosmixVerification\part-c-parent-20261013\managed-b08b848cde00496592632b09708b8591
  VerifyDocumentExpiryReliability.dll            baseline
  VerifyDocumentExpiryReliability-current-source.dll
```

Current-source standalone compilation uses `/nowarn:0436` solely for intentional
shadowing of the frozen exporter/worker/repository type by its source. The worker
compiler retains the two existing SqlClient CS1701 framework-reference warnings.
No source edits or assembly rewriting are used to bypass access or compilation.

## Parent Integration

No registration, permission, DbContext or migration change is required.
After the parent's combined Release build, set `$ParentRelease` to its new frozen
`bin\Cosmix.Api\release` directory on D and run this exact standalone command from
the existing backend checkout:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $ParentRelease -Sources @(
  'docs/hr-srs-remediation/VerifyHrReportXlsx.cs',
  'docs/hr-srs-remediation/VerifyHrReportBoundary.cs',
  'docs/hr-srs-remediation/VerifyHrReportResults.cs',
  'docs/hr-srs-remediation/VerifyHrReportsRemaining.cs'
)
```

The parent requested report-only execution against the final frozen Release when
it is supplied. The worker's direct-source check is complete; its optional later
combined-assembly command is:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $ParentRelease -Sources @(
  'docs/hr-srs-remediation/VerifyDocumentExpiryReliability.cs'
)
```

The existing report scope/OT and notification suites remain available as parent
integration regressions: `VerifyHrReportQueries.cs`, `VerifyHrReportOvertime.cs`
and `docs/attendance-completion/notifications/VerifyNotifications.cs`.

ZIP/XML parsing is not Excel application acceptance or a full OOXML schema
validator. Synthetic repository/transaction/private-byte collaborators do not
establish actual SQL locking, execution plans, encrypted storage, process restart,
authenticated HTTP, load or deployment-worker acceptance. Parent must retain
those limitations in shared status documents. The source freeze release does not
authorize any of those external operations.

## Exact Changed Paths

```text
src/Cosmix.Application/HR/Reports/HrReportService.cs
src/Cosmix.Application/HR/Reports/HrReportXlsxExporter.cs
src/Cosmix.Api/BackgroundJobs/DocumentExpiryReminderWorker.cs
docs/hr-srs-remediation/VerifyHrReportBoundary.cs
docs/hr-srs-remediation/VerifyHrReportResults.cs
docs/hr-srs-remediation/VerifyHrReportsRemaining.cs
docs/hr-srs-remediation/VerifyDocumentExpiryReliability.cs
docs/hr-srs-remediation/VerifyHrReportXlsx.cs
docs/hr-srs-remediation/H-Export-Reminder-Continuation-20261015.md
```

Existing dirty changes were preserved. Shared registration/status/SRS/schema
files, other agents' implementation scopes and the A02 packet were not edited.
No Git write, branch, worktree, fetch, pull, push or commit was performed.

Product SHA-256 at handoff:

```text
HrReportService.cs             7A78273C645527FB55C913BAD61F65944FAB87D3966DFBB8B25954D7C0947F11
HrReportXlsxExporter.cs        B72450FB238D603C116215D679450C9824FE8100DE533B4C99B0D4E46A2F44B8
DocumentExpiryReminderWorker.cs 9D582D113ACD9C6C447E4C5F222FECD744C47404882C28F2B22A5C5A774BB6DE
```
