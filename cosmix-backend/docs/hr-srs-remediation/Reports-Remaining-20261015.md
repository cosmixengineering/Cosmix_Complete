# H1: Remaining HR Report Values

15 October 2026. Scoped continuation of the other twelve reports. The existing
daily/monthly repository calculations and retained-source logic are unchanged.
This document does not close the 19-task plan or claim production acceptance.

## Implemented Values

| Report | Result and source |
| --- | --- |
| punch-audit | Highest assessment revision for the same employee/punch. Missing assessment is Unverified; unknown geofence result is null. Receipt/device coordinates do not imply verification. Source identifies retained Office/Site capture context, not a guessed device-name channel; absent site is Unassigned. |
| operational-zones | Event's retained ZoneVersion links to actual zone and type display color; latest recorded response supplies review status. Unknown zone/color are empty, not invented Green/Amber labels. Current zone master name/color are reference labels, not immutable historical label snapshots. |
| site-reporting | Review sequence selects the latest review; original report notes remain original. Latest immutable employee response ID/time are separate columns; private response contents are not exposed. |
| late-and-kpi | Full assessment-period overlap, including a period spanning the whole selected year. Department/branch/site use the assignment effective at assessment start. Only cases linked to this exact assessment contribute disciplinary state. Money/KPI percentages remain proposals, not finalized KPI benefits or payroll deductions. |
| overtime | Repository no longer calculates requested minutes at latest/fallback salary. Application uses the existing internal OT retained-evidence validator, then reads the exact protected decision. Original source scopes must satisfy every report/source/export permission. Approved minutes/ticks, money, currency and decision ID come from retained evidence. Financial state remains calculated_pending_payroll, not paid. Missing reader dependencies leave explicit unavailable quantities; invalid production evidence fails closed. |
| meal-allowance | Highest submission version supplies claimed amount/currency/payable/payroll dates; latest settlement record must match that submission version. No settlement means NotScheduled. Claimed money is not relabelled as a paid amount. The legacy SettlementBatchReference field continues to carry the source external-payment reference; it is not proof of a payroll batch. |
| leave | Grouping uses employee, leave-type ID and exact fiscal interval, not display name/calendar year. Signed ledger minutes include adjustments, reservations and releases. Linked reversals contribute to their original category; missing/cyclic references conflict. Balance sums every delta once. Entitlement, net utilization, net reservation and encashment-consumption minutes are explicit. |
| employee-master | Existing effective assignment selection is retained. Missing branch/category/type are Unassigned. Company phone no longer falls back to the private login phone. Enum filters use translatable typed predicates. |
| document-expiry | Days remaining use the normalized business as-of date. All four existing sources merge before stable pagination. Unknown issuers stay empty; expiries beyond 90 days are Valid. Superseded or revoked effective certifications do not appear as current reminders. Source IDs break ties. |
| fuel-and-mileage | Actual eligible distance/fuel ratio, null when fuel is zero. No invented 28 km/l or next-service odometer. Trip date uses the existing business-date conversion. The report displays retained claimed money, not a fresh valuation or payment. |
| payroll-handoff | Exclusive batch end prevents September batches appearing in October. Exact signed line amounts, source ID/version and attendance-lock-event ID are retained. No recalculation, currency mixing, payment inference or fabricated company totals. |
| audit | Existing restricted All-scope projection/category predicates remain. Stable event ordering, true page offsets and totals apply. |

All twelve now use deterministic ordering and requested page offsets. Preview
TotalCount and exported Total Records represent the authorized matching row count
before pagination. Repository reads are bounded to 16,384 rows; exceeding that
bound conflicts rather than reporting a truncated total. Existing stricter
employee/source/roster authorization bounds remain. This is bounded materialization,
not a claim of large-company query-plan/load acceptance.

CSV and XLSX share the corrected tabular projection. Nullable values export as
empty cells. Remaining-report decimals use invariant culture without forced
one-decimal/two-decimal rounding; UTC timestamps retain their full offset/precision.
Formula-injection protection remains, including a protective apostrophe on
negative numeric text in CSV. Export still honors the requested filter and page.

## Contract Limits

- Leave day-valued legacy fields are now nullable. They remain null because this
  ledger does not prove one universal minutes-per-day conversion, pending request
  days, unpaid days, encashable days or paid encashment settlement. Consumers must
  use the exact minute fields and fiscal interval, and display unavailable days.
  Encashment consumption is not settlement. No 480-minute day or six-day retention
  rule is invented.
- Fuel NextServiceDueKm remains null until an authoritative dated due-odometer
  source exists. The stored service odometer plus an assumed 1,000 km is not one.
- This does not upgrade generic KPI proposals into actual awarded benefits,
  perform payroll posting, activate legal deductions or certify financial totals.
- Timestamp receipt/request filters retain the existing inclusive UTC-day range
  contract. Current authorization, expiry as-of and trip business dates reuse the
  established business-date conversion. No new timezone or fiscal policy is added.
- Scope, source/export permissions, protected audit persistence and post-IO grant
  revalidation remain in the existing outer transaction. OT evidence loading does
  not nest the public financial endpoint or create another transaction.
- No common DI, Program, DbContext, migration, permission definition or global
  progress-map edit is required. The two additional report-service dependencies
  are existing registered services. No host, SQL, seed, account, credential or
  real provider operation was performed.

## Verification

The report-only standalone scripts use the existing
`docs/attendance-gap-c/VerifyManaged.ps1` approach. They do not add a test project.

Initial isolated Release build:
`D:\CosmixVerification\hr-reports-remaining-20261015-h1\baseline`.
The initial no-restore attempt failed because the new isolated output had no asset
files; locked restore/build then passed with zero warnings/errors.

RED: `VerifyHrReportsRemaining.cs` reproduced 51 failures across the twelve reports
against the baseline. Retained runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-ac24cad70bc747df8e9059747fb9c166`.
One original leave expected balance was subsequently corrected from 5,145 to 5,280
minutes: 5,400 - 135 + 135 - 30 - 90. That was a fixture arithmetic error, not a
product defect.

First corrected Release solution build passed with zero warnings/errors:
`D:\CosmixVerification\hr-reports-remaining-20261015-h1\green\bin\Cosmix.Api\release`.
Standalone evidence on that frozen build:

- Remaining report values: 186/187 before the fixture arithmetic correction.
- Report OT integration: 12/12, including a 120-minute request/90-minute retained
  award/PKR 538, unchanged award after future salary, actual CSV values, original
  scope, separate export/financial grants, tampered decision and revoked session.
- Existing repository scope suite: 242/242. Its count decreased because removal
  of unused latest-salary/service queries reduces captured-expression checks;
  the original fourteen-variant scope scenarios were retained.
- Existing daily/monthly result suite: 153/153.
- Existing boundary suite exposed a selection-object identity regression in the
  first OT integration. Source now preserves the exact server-created selection
  object while attaching an internal, JSON-ignored authority context.

Retained first integration runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-d3546001ac31421d97e40764b1ddaf2e`.

Final source follow-ups include enum-filter SQL translation, review sequence,
current certification revision, full CSV totals, business trip dates and the
selection-identity repair. All five suites below subsequently passed on the
parent's frozen Release at
`D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release`:

1. `docs/hr-srs-remediation/VerifyHrReportOvertime.cs`
2. `docs/hr-srs-remediation/VerifyHrReportBoundary.cs`
3. `docs/hr-srs-remediation/VerifyHrReportQueries.cs`
4. `docs/hr-srs-remediation/VerifyHrReportResults.cs`
5. `docs/hr-srs-remediation/VerifyHrReportsRemaining.cs`

Exact results on that build: **12 + 266 + 242 + 153 + 267 = 940/940**.
Runner exit code 0; no SQL connections attempted. Retained sources, compiler and
execution logs, and all dependency hashes are in
`D:\CosmixVerification\part-c-parent-20261013\managed-7f463638bb65426ca057a665004edda3`.

One final product predicate is newer than that Infrastructure DLL (12:12:12 UTC
source versus 12:11:40 UTC DLL): punch assessment must also match the punch's
EmployeeId. This guards an inconsistent cross-employee assessment link. The
new independent regression was added after the successful run. **Do not treat
940/940 as verification of that final predicate.** The parent must rebuild and
rerun the remaining-report suite (and its chosen integration gates). No agent
full build was started after the parent reserved the build slot.

SHA-256 of the tested parent assemblies:

```text
Cosmix.Api.dll            4C43EB1E297C734E10C014CE5E367730AF19492B0DB1D95BDBFE8C243403DF48
Cosmix.Application.dll    312ADC7BE2A89F075DED22B39503571C254E89E763DCFE1344046946596C5A41
Cosmix.Infrastructure.dll 6978D4CA5241568E7DA4C25D5E76E31F3476D9365F19C31D74FD26C1590CA619
Cosmix.Domain.dll         1795D820E2AB4520796D14EBAD420B7D48D05337E3702A23BCA1EA63FB300E23
```

SHA-256 of report product sources at handoff (file names below map to Exact H1 Files):

```text
HrReportAuthorization.cs     4C970266B193DCA868B202A5AA42D3907A4958B1F10A17EBFEF9F73FD62E9270
HrReportContracts.cs         F580006ADA0C7CE216756CD8ABA632308B803CAA42C4B814ABA072B198155FF3
HrReportService.cs           2C4D911D841170F93032E616E211703D777B9C7C60C4A6916F0D1BBC6075E183
HrReportOvertimeEvidence.cs  FDFD9122D172007E7534AD834409128BAE99FCD0A0038118AA0C232FA1C2FDFA
HrReportRepository.cs        878D2D7916EAAE5CA574B295A707AAD16FDD6AD43EC6E18CF0226D5DE5A5CF14
HrReportScopeRepository.cs   B25489AA0C56FB2F74FF9F597D87CC5DAAF031F5985FCCBBD3986F725BE080AE
```

SHA-256 of standalone sources:

```text
VerifyHrReportOvertime.cs   6934C882A44450B0917DBF680C2C2C743532CAAF34E49C780CBF16FEFF00522B
VerifyHrReportBoundary.cs   C61A27EA1BE2AE0E1DC96FC900DA16F375CB74B8326ED23BF8245F621EDDFFC6
VerifyHrReportQueries.cs    428B3F1CD1B983A3D51FBB30BEC87E64FC7702D4D55EF7E2588AD676FF763001
VerifyHrReportResults.cs    EFB6930E797BDDF3EA969B76036BB6D1389EE0C6EB71E18BED628BCB82DBFA1C
VerifyHrReportsRemaining.cs C576EBD424A12EEAB31B2FCBE63DD0E18FEF37D717ABDF9B5D1ED550D1D77A2F (267-check tested source)
VerifyHrReportsRemaining.cs 264D4AF402E1936D41DAA1E69AF040AF236E65ED2EF7F83D11123CBF22E50937 (handoff source with extra guard regression)
```

The last script executes real repository queries on synthetic in-memory model
rows through an EF query-provider collaborator, checks independent expected
values, parses generated CSV, checks decimal/quote/newline parity under fr-FR,
and asks the real SQL Server provider to translate the captured expressions.
Connection interceptors throw on any attempted connection. The OT script uses
actual application services and an isolated copy of the existing synthetic OT
fixture. These are not actual SQL tests, real encrypted-storage tests or
authenticated HTTP acceptance. XLSX parsing, SQL execution plans, concurrency,
storage failure/restart and end-to-end exports remain separate acceptance work.

## Exact H1 Files

- `src/Cosmix.Application/HR/Reports/HrReportAuthorization.cs`
- `src/Cosmix.Application/HR/Reports/HrReportContracts.cs`
- `src/Cosmix.Application/HR/Reports/HrReportService.cs`
- `src/Cosmix.Application/HR/Reports/HrReportOvertimeEvidence.cs` (new)
- `src/Cosmix.Infrastructure/Repositories/HR/HrReportRepository.cs`
- `src/Cosmix.Infrastructure/Repositories/HR/HrReportScopeRepository.cs`
- `docs/hr-srs-remediation/VerifyHrReportsRemaining.cs` (new)
- `docs/hr-srs-remediation/VerifyHrReportOvertime.cs` (new)
- `docs/hr-srs-remediation/Reports-Remaining-20261015.md` (new)

These files already contained earlier dirty repairs where applicable; those
repairs were preserved. No Git synchronization, commit, branch or worktree was used.
