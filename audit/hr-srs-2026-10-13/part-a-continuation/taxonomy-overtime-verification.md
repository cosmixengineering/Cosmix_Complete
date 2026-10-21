# Part A broader staff taxonomy: overtime consumers

Status: owned source and fresh managed consumer checks pass. Parent owns the final full-solution/integration verification and actual authenticated acceptance.

The source uses the shared `EffectiveRosterResponse.Taxonomy` proof. It retains the actual staff-category code, employment assignment, business date, published taxonomy version/fingerprint, location and scheduling basis. Published sources require the roster's exact membership identity and Karachi schedule context. The existing protected source fingerprint includes the complete roster proof; no second taxonomy DTO, EF property, schema change or migration is introduced.

Only a verified Site mapping with `OutsideOvertimeInheritance=true` may consume the existing global/employee overtime controls. Mapping alone supplies no eligibility, completed duty, approved duration, compensation, rate or money. Every Office-basis category stays ineligible, even if a broader mapping sets the inheritance flag. The existing confirmed nine-hour duration and salary formula are retained; other duty interpretations remain unavailable.

Source loading compares the retained roster proof with the real dated taxonomy reader over the complete occupied duty interval, including overtime beyond a roster's end. Submission and decision paths recheck taxonomy after private evidence IO. Single and named-bulk receipt paths revalidate sources before replay. Historical protected detail uses its immutable retained proof rather than changing an old award to match a later publication. Confirmed legacy Office/Site sources omit the nullable shared proof and retain their JSON/fingerprint shape.

Existing global and employee eligibility-switch history stays category-neutral. A fresh mapped Site/inheritance gate applies when reading and publishing employee eligibility; employee publication rechecks employment and taxonomy after private IO. No business policy, authority assignment, permission grant or actual category was seeded.

## Exact owned source files

- `src/Cosmix.Application/Attendance/Overtime/OvertimeTaxonomy.cs` (new)
- `src/Cosmix.Application/Attendance/Overtime/OvertimeSources.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeEligibilityService.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeRules.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeService.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeQueriesAndBulk.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/OvertimeRepository.cs` (only the dated employment Branch projection)

The shared roster proof/helper and broader calculation/payroll changes belong to the parent and roster owner. Existing Part B contract/session fields in OvertimeContracts and OvertimeRepository are preserved.

## Executed RED evidence

The retained partial Application binary had SHA256 `7D1C6D8CD3A6EAEFF1AA4EF4925EC8BC5A192D61C3F12718F2D5EEC6129F824C`.

1. `taxonomy.OvertimeChecks.cs` executed the original real OT service/operation/eligibility/formula suite plus broader taxonomy paths: 94 checks passed before the published roster with another membership identity was incorrectly accepted. Exit 1: `Expected overtime_taxonomy_source_conflict`. The current source adds the exact membership guard.
2. `taxonomy.OvertimeReplayRed.cs` executed a real published SITE submission and receipt, then returned a current legacy null proof. The retained binary incorrectly replayed the receipt. Exit 1: `Published SOURCE replay escaped through null current baseline proof`. The current source compares the production source fingerprint unconditionally when the taxonomy reader is present.

The initial old-API binary also rejected a broader category at `overtime_category_unavailable`; that preliminary check did not yet supply the shared approved proof and is not counted as the precise published-source RED evidence above.

The main standalone harness includes both precise regressions, legitimate broader pending/approval/financial history, missing global/disabled global/personal restrictions, every Office-basis denial, unavailable category, changing version/fingerprint, another category/membership, mapping transition during occupied overtime, post-private-IO publication/submission/decision races, named-bulk intake/decision/replays, and legacy null-proof serialization. Fixtures use real application services with synthetic repositories/private bytes, not a runtime test endpoint.

## Fresh GREEN evidence

The parent supplied the fresh Application Release build (zero warnings/errors). This consumer then executed the actual updated managed DLLs:

- `taxonomy.OvertimeChecks.cs`: **109 checks**, exit 0, no compiler warnings. The full original OT workflow suite and both precise regressions pass.
- `taxonomy.OvertimeReplayRed.cs`: the exact prior failing published-to-null receipt fixture now passes, exit 0. This repeats the case in the main suite; it is not an additional unique workflow.
- Unmodified `docs/attendance-completion/VerifyOvertimeRules.cs`: **26 checks**, exit 0, no compiler warnings.
- `taxonomy.OvertimeComposition.cs`: **8 actual AddApplication registration/constructor/alias/scope checks**, exit 0, no compiler warnings or repository IO. Production Source and Eligibility descriptors inject the same scoped real StaffTaxonomyResolver; the optional dependency does not silently select the null baseline adapter when the real reader is registered. The independent ConfirmedOvertimeDurationPolicy registration remains in place.

Verified Application DLL SHA256: `EF196064234886113F75B2D527B19BAFF46418FA2F5083155A2BB8A6443C32F2`. Domain DLL SHA256: `444012EB7D38E021C3BD819B66BD52EC5D70EF8D9C7FFE167BEA3A22F0BFEC65`.

## Verification boundary

The harness is retained in the audit directory and uses `D:\CodexTemp\Cosmix_Taxonomy_OT_20261013`, an owned TEMP console wrapper referencing only the existing managed Application/Domain DLLs plus the ASP.NET shared framework. Its prior C drive wrapper was preserved by an exact owned-directory move. It adds no backend test project and performs no SQL connection, host start, provider call, actual employee operation, authority grant, Git operation or financial acceptance.

Actual management publication and complete authenticated/provider acceptance remain separate gates. Rehire and permanent-ban handling are untouched.
