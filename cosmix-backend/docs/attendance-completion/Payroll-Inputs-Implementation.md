# Attendance payroll calculation inputs

Confirmed by the user on 12 October 2026:

- Recommended initial normal salary settings use monthly basic salary divided by the actual
  payroll-month days. Full unpaid absence costs one day; unpaid half-day costs half a day;
  hourly shortfall uses the daily rate divided by scheduled shift hours.
- Approved paid leave, weekly offs and applicable paid holidays do not create deductions.
  Overtime keeps its separately approved calculation and evidence.
- HR settings retain divisor, deduction and late-count rules as explicit immutable versions.
  No recommended value is automatically published to a company or account.
- A late threshold uses the salary effective on its completion date. Salary changes do not reset
  the count. A change in the late rule starts a new count; unchanged rules retain their count.
  Previous events keep their applicable historical rules.
- No duplicate deductions, changes to finalized history, compensation mutation or salary payment
  release. This module prepares auditable payroll inputs only.

## Implementation boundaries

1. Extend policy rules with a new schema while preserving schema 1/2 canonical bytes.
2. Build bounded actual-source monthly deduction candidates with exact time and source provenance.
   Retain each policy/compensation identity and the events completing a threshold.
3. Add separately permissioned financial preview, immutable saved revisions and history. Persist
   protected calculation detail; repeated retries and stale competing saves cannot duplicate charges.
   Revisions replace the previous complete monthly input set, rather than adding another deduction.
4. Reuse current authentication, historical scope, protected storage, audit and transaction boundaries.
   Locked months remain immutable. Carry-forward financial valuation and payroll handoff are separate
   dependencies; preparing input does not falsely mark all Attendance payroll ready.
5. Keep all work local. New schema files can be prepared, but no migration, fixture or database write
   is executed. Use standalone behavioral, query/model and registered anonymous HTTP checks.

The user subsequently confirmed:

- New ordinary and late attendance deductions require two-decimal rounding; the suggested form
  chooses half-up explicitly. Historical schema 1/2 calculations are preserved, and separate OT
  retains its approved whole-rupee rule.
- For overlapping ordinary shortfall and late-threshold penalties, only the larger applicable
  deduction is counted. Retain the suppressed candidate for explanation; never sum both charges.
- Admin's assigned permissions manage enablement, grace/threshold, full/half-day fractions, fixed
  PKR amounts or configured hours, and weekly/monthly/other supported counting periods. Weekly
  start day is explicit. Disabling a rule does not erase historical configuration.
- Cosmix Engineering operates in Punjab, Sindh, KP, Balochistan and Islamabad in HVAC/MEP engineering,
  installation, maintenance and field services. Company name, business type and Head Office address,
  city and jurisdiction are configurable company settings, not seeded facts.
- Company-level configuration belongs to current SuperAdmin with an explicit permission. Site
  jurisdiction is historical and explicit; neither Head Office nor a mutable city label decides a
  remote employee's applicable location. Unverified legal rules cannot become active payroll rules.

## Legal applicability

The federal [Payment of Wages Act, 1936](https://pakistancode.gov.pk/pdffiles/administrator8820e88efaf7eedabf5c1d8c73b3dee5.pdf)
distinguishes fines from absence deductions in sections 8 and 9. The official
[Sindh Payment of Wages Act, 2015](https://sindhlaws.gov.pk/setup/publications_SindhCode/PUB-NEW-18-000186.pdf)
also addresses these separately. Absence proportionality and fine procedures cannot be replaced by
a configurable company formula. The operating jurisdictions are now confirmed; employee legal
coverage, statutory applicability and the approved deduction policy still need review
before production use. A calculated candidate is not certification of legal deductibility or authority
to withhold wages; no real payroll posting or payment execution is part of this module.

## Implemented API and persistence

Four operations under `/api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}`
provide GET `/preview`, POST/GET `/revisions` and GET `/revisions/{id}`. Read and calculate use
separate explicit `attendance.payroll-inputs.read` / `attendance.payroll-inputs.calculate`
permissions, current web MFA, and all current/original historical scopes. Unsafe requests also
require browser CSRF. No permission is silently granted at startup.

Save binds the current preview fingerprint, expected revision, reason and retry key. Source changes,
competing revisions and locked months reject a new save. Identical source input under another key
reuses the current revision. A revision is the complete replacement input set, not another additive
charge. Protected evidence contains calculation inputs, source hashes, exact financial lines,
jurisdiction versions and original scopes; SQL contains a bounded header and private reference.
Saved detail does not recalculate under a changed policy, salary or site mapping. Responses expose
reviewable source identities and exact amounts, never storage paths or unrelated bank/medical data.

The normal-salary form recommendations use actual month days, basic salary, 1/0.5 day absence
fractions and two-decimal half-up rounding. They are suggestions, not automatic publication.
Schema 3 supports late enablement, dynamic thresholds, weekly/month/year/fiscal counts and
day/fixed-amount/hour candidates. Legal activation has no client-settable switch. Every prepared
or retained response remains `payrollReady:false` and `paymentInstruction:false`.

Local migration 20 adds financial input headers and explicit company/site configuration. Its
incremental SQL is prepared, not executed. Source review and 22 offline model/script checks cover
constraints, snapshot agreement and refusal to downgrade retained history. Prior migration files
remain unchanged. See [SQL record](../sql/README.md), [company settings](Company-Settings.md),
[engine details](../attendance-policies/Payroll-Inputs.md) and [client examples](Policy-Leave-Client-Examples.md).

## Executed checks and limits

- 49 financial engine/real-producer checks: calendar divisor, exact rounding, salary provenance,
  half-leave settings, resets, disabled rules, overlap candidates and incomplete evidence.
- 57 service checks: permission/MFA denial, saved source scope revocation, exact retry, stale
  revision/source, locked periods, protected evidence binding and jurisdiction contract handling.
- 6 production SQL translation checks with zero connections; no SQL execution is implied.
- 30 company service plus 23 company EF/query/write-guard checks; zero connections.
- Policy schema 3: 121 checks; legacy rules 90 + 7; direct publication 64. Leave self reads 43 and
  workflow 65; daily calculation 67. These are standalone checks, not real-account acceptance.

Final build/registered HTTP results are recorded in [Verification](Verification.md). Actual
authenticated SQL, private-storage restart/replay, competing writers and mobile UAT remain
unexecuted for these new changes. Cross-month late overlap requires reconciliation with earlier
financial revisions and remains explicit `prior_deduction_reconciliation_required`; no earlier
charge is guessed. Financial carry-forward, verified statutory payroll activation and payroll
handoff are separate dependencies. A missing dependency never releases payment or stops salary.
