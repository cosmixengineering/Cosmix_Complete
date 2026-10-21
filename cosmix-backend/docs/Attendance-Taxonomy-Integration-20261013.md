# Part A dated taxonomy integration into Attendance

The final Part B handoff released the existing Attendance consumers. This continuation implements the already requested broader-category source integration. It does not publish company mappings, policy values, permissions, or entitlements.

## Implemented behavior

- Employee, Office and Site roster resolvers use the approved mapping on the business date and across the occupied schedule interval. They bind the same dated employee assignment, category and location before and after IO. An overnight category/publication change is unavailable.
- EffectiveRosterResponse carries an optional immutable taxonomy source: exact category code, publication ID, fingerprint, work location, schedule basis and outside-OT inheritance. Only the exact confirmed legacy OFFICE/SITE baseline omits this field, preserving its historical JSON and hashes.
- The Office source reader matches the retained roster pin against fresh dated authority and rechecks complete membership after policy/boundary IO. Broader Office mappings retain their real Branch, roster, geofence and holiday sources.
- OT sources and rules accept an approved Site mapping only when outside-OT inheritance is explicitly enabled. Separate global/employee eligibility, approved duration, closed valid duty, compensation and independent decision controls still apply. Every Office mapping is ineligible. Existing confirmed duration/formula behavior is retained; a non-nine-hour duty needs an approved supported duration rule.
- Single and named-bulk OT submission, decisions and retries reject changed dated sources. Published-to-null legacy replay cannot bypass the check. Post-private-IO revalidation prevents staging a changed source.
- Daily calculation validates the roster pin against actual dated employment and fresh taxonomy at the beginning and end, including actual closed-session occupied dates. An approved manual correction must retain the same historical taxonomy pin. Part B schema-5 fixed-Admin, MFA, alternative-policy and outer-audit checks remain intact.
- Payroll producer and protected-history validation require a matching typed taxonomy source for both broader Site and Office categories. Existing detailed Office location/provenance checks and legal/payment gates remain.

## Fresh executed verification

| Verification | Result | Boundary |
| --- | --- | --- |
| Roster taxonomy | 30 passed | Real consumers, synthetic dated sources and IO races |
| Existing Site scheduling / Office provenance | 171 / 16 passed | Legacy regressions |
| OT workflow / existing rules / actual Application DI | 109 / 26 / 8 passed | Protected synthetic evidence, retries, IO races, real descriptors |
| Daily calculation | 193 passed | Existing schema-5 cases plus broader categories and stale correction proof |
| Payroll producer / service | 59 / 80 passed | Real consumers and protected synthetic financial history |
| Current model, actual deployment DI and default authority | 45 passed | SQL connections/private IO forbidden; all seven new consumer dependencies resolve the actual scoped taxonomy reader |
| Full four-project Release | 0 warnings, 0 errors | D: artifact outputs; 57.79 seconds |

RED runs reproduced the actual broader Office calculation/payroll rejection, acceptance of a bare broader Site payroll source, missing published roster membership binding, a published-to-null OT replay bypass, a policy-IO membership race, an empty interval, and stale manual-correction taxonomy. The corresponding current-source checks passed after fixes. Independent final source review reported no high-impact finding.

An older `docs/shift-publication/VerifyPublication.cs` fixture still has a recorded CS7036 compile failure because it omits Part B-added constructor arguments. It is not included in passed counts. The current full solution and the listed actual consumer suites pass.

## Source ownership and persistence

Six Application roster/Office files, six Application OT files, the existing Infrastructure OT employment Branch projection, and three Calculation/Payroll files changed: 16 product files. Exact hashes are retained in `audit/hr-srs-2026-10-13/part-a-continuation/attendance-taxonomy-source-hashes.json`; focused owner reports and standalone checks are adjacent. No permanent test project was added.

The applied A23/B24 migrations and current model remain unchanged. Fresh current-model verification still sees 24 migrations and no mapped rehire types; the three B24 migration/designer/snapshot hashes match the frozen handoff. The selected testing database remains at the previously verified 24 migrations, five accounts and fourteen employees; no additional SQL, account, role, provider or host operation was performed by this source integration.

C: allocation failure interrupted one write and truncated the calculation producer. The original was recovered from complete recorded source/patches and matched its exact frozen SHA-256 `C15E864004C6DCFC56FE91CEC609D2CB980ED8D245ACE2204E9B95AB12DC3848` before narrow changes were reapplied. Its recovery copy is retained on D:. Completed task temporary artifacts were moved to D: with all data preserved. Fresh builds/tests and source comparison verify the restored Part B behavior.

## Remaining operational work

Actual approved category publication and referenced duty/benefit policies remain management decisions. Existing-employee category conversion still needs its own approved dated workflow. Rehire remains proposed and disabled, with all 17 episode-aware consumer/persistence boundaries pending confirmation; permanent blacklist restrictions are preserved.

The current five-account testing target still lacks a supported bounded authenticated HTTP profile. SQL/HTTP concurrency, replay, revocation, restart and real carrier/face/device acceptance remain separate gates. Source counts and Release success are not operational sign-off. See the six-page HR continuation status for the complete line-wise handover.
