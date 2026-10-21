# Taxonomy continuation verification

Final local source checkpoint, 2026-10-13. This implementer changed only new taxonomy Application/Repository/Controller/filter files, `docs/Staff-Taxonomy.md`, and this audit folder. Existing HR integration, core DI/catalog and Attendance consumers belong to the root/B owners. No EF model/configuration/snapshot/migration or database/provider/account/Git mutation was performed by this implementer.

## Executed evidence

- Earlier full `Cosmix.Backend.sln` Release build, isolated TEMP artifacts: **passed, 0 warnings and 0 errors** after rollout-history/cancellation/predecessor fixes, before the independent role/temporal/raw-HTTP review corrections. The root integration owner owns the final full solution build.
- Fresh standalone actual feature/domain/controller checks after all independent review corrections: **92 passed** (43 codec/resolver/authority/metadata checks, 41 service/operation/private-evidence/replay checks, 8 actual controller/filter/MVC-body checks). They use synthetic authentication, persistence and private-file collaborators and real ASP.NET resource contexts. They do not exercise a SQL transaction, a listening API server or genuine deployed storage/provider.
- The retained `run-taxonomy-checks.ps1` was executed successfully. It builds the actual Application dependency and links the actual API controller/filter in a standalone Release project with warnings as errors and isolated TEMP artifacts.
- Initial runtime assertion reproduced the missing taxonomy resolver before implementation. Separate fresh regressions reproduced the missing stale-proposal cancellation operation and later-publication acceptance after removing its predecessor; both now pass.
- Independent-review regressions also reproduced RED for the actual domain `Role.CreateSystem(Admin).Code` contract, live-session expiry during private audit I/O, and raw duplicate category flags before MVC binding. Fresh final checks pass after the fixes.

The final script-executed TEMP directory was `C:\Users\HUMA~1.KHA\AppData\Local\Temp\cosmix-taxonomy-checks-5350e7eb89e946cbacb5d73b8ba5cbc5`. The earlier manually constructed harness was `C:\Users\Huma.Khan\AppData\Local\Temp\cosmix-taxonomy-checks-6d2918ff71ce4132ad0a7831ada18b5e`. Source-only harnesses are retained here as `taxonomy.Program.cs`, `taxonomy.ServiceChecks.cs` and `taxonomy.HttpChecks.cs`; `run-taxonomy-checks.ps1` creates a new standalone project and isolated artifacts under TEMP for a future run. No permanent test project is added to the backend solution or this audit folder.

## Covered boundaries

- Required explicit booleans/applicability, unknown/duplicate JSON members, category uniqueness/30-character bound, canonical ordering/roundtrip, required exact policy references, unsupported schedule/location combinations, confirmed OFFICE OT denial and SITE initial-site behavior.
- No-publication legacy baseline, unknown broader category, active mapping, future/expired/inactive publication, expired successor, same-date ambiguity, unknown schema, invalid evidence, stable earlier fingerprint after a future publication and missing-predecessor rejection.
- Draft/activation/scheduled-duty location rules and exact missing-field paths; unavailable/expired authority and retained authority stamp.
- Protected request without publication; exact request/decision replay; different-payload rejection; pending/stale/closed request guards; independent fixed reviewer; current permission/session revocation; mobile administration denial; exact private proposal/decision verification and tampered-content unavailability.
- Requester-only cancellation after authority unavailability, cancellation rowversion/replay/rights checks, preserved private reason, no publication, and a fresh proposal after cancellation.
- Incorrect private audit metadata is rejected. The fake persistence collaborator does **not** model SQL rollback; this check asserts rejection only and must not be represented as preserved-database-row acceptance.
- Actual canonical `ADMIN` review and historical proof verification; fixed reviewer assignment/session expiry during private audit I/O; substituted session/role-assignment denial; effective date, authority window, candidate revision and period-lock rechecks after I/O.
- Actual controller-attached raw request/decision/cancel filters before MVC-style binding: duplicate nested flags/decision/cancel reasons, required booleans, case mismatches, unknown members, valid body rewind and bounded chunked body rejection.

## Operational boundary and handoff

Real taxonomy values and authority are not configured. There are no approved category/rate/eligibility fixtures seeded into product settings, no granted role permissions and no actual employee/provider/private-file activity. Selected-target SQL contention, transactional rollback, append-only row preservation, receipts/audit/decryption/restart and authenticated browser/device acceptance remain separate work.

The resolver offers the confirmed OFFICE/SITE baseline before any publication, including pending-only reservations, and for historical dates before a valid active first publication's effective start. That historical exception preserves the exact original fingerprint and never enables broader codes. Once a version starts, inactive/expired/invalid/unknown/missing mappings cannot restore baseline. No backdated version or assignment is fabricated. Broader daily/job/mobile location shapes and category conversion require their explicit separate implementation/authority. Underlying claims/reference authority and eligibility must be verified by D/E; this framework is not their business implementation.

Consumer API is `IStaffTaxonomyResolver.ResolveAsync(code, on, ct)` / `ResolveForRangeAsync(code, from, toExclusive, ct)`. Helpers are `StaffTaxonomyResolver.RequireMapping`, `ValidateLocation` and `MissingLocationFields` with Draft/Activation/ScheduledDuty. HR submission has no initial-site field; apply `RequireSiteBeforeActivation` at independent approval/materialization with its separately supplied `initialSiteId`. Part B owns schedule/boundary/capture/calculation/OT evidence integration. Existing GlobalSite OT settings do not automatically authorize broader categories, and OFFICE remains denied.
