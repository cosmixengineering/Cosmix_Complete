# Company configuration and dated site jurisdictions

Company configuration is explicit, editable reference data. It creates no payroll posting, financial rate, legal certification or employee assignment. The company name and business type are required inputs; neither is hardcoded. The singleton profile links an existing active head-office Branch and its existing active City, with a separately supplied address and jurisdiction. Optional coordinates must be a valid latitude/longitude pair with at most six decimal places. No duplicate Branch, City or Site CRUD is introduced.

| Route under `/api/v1/hr/company-settings` | Purpose |
|---|---|
| `GET /` | Current profile or explicit unconfigured result; supported internal jurisdiction labels |
| `PUT /` | Current SuperAdmin creates or updates company/head-office details |
| `GET /history` | Paged profile and site-configuration audit history, up to 100 rows per page |
| `GET /sites/{siteId}/jurisdictions` | Retained dated site versions including corrected originals; maximum 5000 rows |
| `POST /site-jurisdictions` | Current SuperAdmin publishes a new effective date or explicit same-date correction |

Reads require current web MFA and explicit All-scope `org.company.read`. Every mutation additionally requires current SuperAdmin and explicit All-scope `org.company.manage`. Ordinary Admin possession of the manage permission alone does not authorize company/location changes. Authorization is rechecked before retry replay. Financial settings retain their separate permissions and services.

Initial profile creation omits `expectedVersion`; subsequent amendments require the current base64 rowversion. Site publication uses `expectedCompanyVersion` and returns the next company version, so competing configuration changes fail rather than overwrite. All writes require an `Idempotency-Key` and reason. The existing organization transaction saves business changes, before/after history, audit and exact retry receipts together. The singleton identity/unique key prevents duplicate companies; no initial profile or permission grant is seeded.

Site jurisdiction rows are immutable. An explicit new `effectiveFrom` begins a mapping on that date; the next later active date ends its interval without editing the original. A same-date correction must identify the active `supersedesVersionId`, preserving the original row and permitting only one successor. New retrospective publications are rejected using the current Pakistan business date. The affected interval must not overlap a locked attendance period. These checks run after receipt replay, so a previously successful exact retry can still replay during its ordinary receipt window after midnight. No Delete API or historical rewrite exists.

The internal payroll source returns one context per requested date/site, with `configured`, `unavailable` or `conflict`, the exact configuration version ID and internal jurisdiction code. It never falls back to the head-office province, mutable city text or a guessed province. Missing mappings remain unavailable; ambiguous latest dates or malformed correction history remain conflict. The read is bounded to 366 input days and 5000 source versions. It loads no employee, salary, attendance evidence or bank data.

`PK-PB`, `PK-SD`, `PK-KP`, `PK-BA` and `PK-ICT` are internal identifiers for Punjab, Sindh, Khyber Pakhtunkhwa, Balochistan and Islamabad Capital Territory. Selecting one does **not** certify applicable employment law, statutory contributions, deduction legality or exemptions. `legalStatus` remains `review_required`; no input can activate or bypass legal verification. Payroll preparedness consumes the explicit dated site context and retains its separate readiness checks.

## Client request examples

Use the existing authenticated web session with MFA and the application's CSRF protection. Every value in angle brackets below is a placeholder to replace with confirmed company data or an actual API response; none is a supplied company location or approved province. Existing Branch, City and Site IDs come from their existing master APIs. The head-office City must match the chosen active Branch.

Start by reading the current configuration and supported codes:

```http
GET /api/v1/hr/company-settings
```

An unconfigured response has `state: "unavailable"` and `profile: null`. Select the jurisdiction code explicitly from the returned list after confirming the location. Initial creation uses no expected version:

```http
PUT /api/v1/hr/company-settings
Content-Type: application/json
Idempotency-Key: company-profile-create-001

{
  "companyName": "<confirmed company name>",
  "businessType": "<confirmed business activities>",
  "headOfficeBranchId": "<existing active branch UUID>",
  "headOfficeCityId": "<that branch's active city UUID>",
  "address": "<confirmed head-office address>",
  "jurisdictionCode": "<confirmed code from GET jurisdictions>",
  "latitude": null,
  "longitude": null,
  "expectedVersion": null,
  "reason": "Initial confirmed company configuration"
}
```

For an amendment, first read the current `profile.version`. Send the complete desired profile with that exact value and a new key:

```http
PUT /api/v1/hr/company-settings
Content-Type: application/json
Idempotency-Key: company-profile-update-001

{
  "companyName": "<confirmed company name>",
  "businessType": "<confirmed business activities>",
  "headOfficeBranchId": "<existing active branch UUID>",
  "headOfficeCityId": "<that branch's active city UUID>",
  "address": "<confirmed updated head-office address>",
  "jurisdictionCode": "<confirmed code from GET jurisdictions>",
  "latitude": null,
  "longitude": null,
  "expectedVersion": "<profile.version from latest GET>",
  "reason": "Confirmed head-office details updated"
}
```

Publish a separately confirmed site location. Replace `effectiveFrom` with an explicit current/future Pakistan business date in `YYYY-MM-DD` form; do not copy the head-office code without confirming the site's location. Use the latest company version, including changes made by other administrators:

```http
POST /api/v1/hr/company-settings/site-jurisdictions
Content-Type: application/json
Idempotency-Key: company-site-location-001

{
  "siteId": "<existing active site UUID>",
  "effectiveFrom": "<confirmed current/future YYYY-MM-DD>",
  "jurisdictionCode": "<confirmed site code from GET jurisdictions>",
  "expectedCompanyVersion": "<profile.version from latest GET>",
  "reason": "Confirmed dated site jurisdiction",
  "supersedesVersionId": null
}
```

The response's `companyVersion` is the concurrency version for the next change. A later effective date needs no predecessor ID; a correction at the same current/future date must use that date's active `version.id` as `supersedesVersionId`. Keep the exact body and key when retrying an uncertain request. After a stale-version response, reload and review the current data before issuing a new request/key.

Read the retained timeline and paged audit history:

```http
GET /api/v1/hr/company-settings/sites/<existing site UUID>/jurisdictions
GET /api/v1/hr/company-settings/history?page=1&pageSize=25
```

Configured responses still report legal review as required. These requests do not approve deductions, statutory rates or payroll posting.

## Verification

Executed on 12 October 2026 in Release using the existing local package cache:

| Standalone check | Result | Evidence boundary |
|---|---|---|
| `VerifyCompanySettings.cs` | 30 passed | Real services with synthetic authorization/repository adapters: SuperAdmin/MFA/permission denial, current-session checks, exact replay including after midnight, stale versions, existing-master validation, configurable values, dated future changes, explicit corrections, retrospective/locked-period rejection and unavailable/conflicting source context |
| `VerifyCompanyPersistence.cs` | 23 passed | Actual EF metadata, persistence guards, actual `CosmixDbContext.SaveChanges` rejection of deleted singleton/mutated history, and production query translation; zero database connection attempts |

These checks do not establish real authenticated SQL transaction rollback, competing database writes or deployed-client acceptance. Root integration owns full solution/HTTP checks and migration preparation. No test project was created, and this feature work applied no migration or database write and made no Git or network mutation.
