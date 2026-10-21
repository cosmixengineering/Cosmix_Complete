# Staff taxonomy: Attendance roster and Office source continuation

13 October 2026. This is executed source verification, not activated company policy or authenticated database/provider acceptance.

## Implemented source

- `RosterStaffTaxonomy.cs` adds an immutable retained category code, actual publication ID/fingerprint, WorkLocation, ScheduleBasis and outside-OT classification. It validates ordered duty intervals and only omits the source for the exact confirmed historical `OFFICE`/`SITE` baseline. Classification does not grant a benefit, access or overtime entitlement.
- `EffectiveRosterResponse.Taxonomy` is nullable and omitted from JSON when null, preserving historical baseline schedules and their serialization shape.
- `EmployeeRosterResolver` routes a dated approved Office/Site mapping and rejects mismatched category navigation or assignment location. Reference metadata alone cannot schedule an arbitrary category.
- `OfficeRosterResolver` and `SiteRosterResolver` retain published taxonomy identity, recheck actual membership after schedule/calendar IO, and require the same effective taxonomy source across every occupied date of an overnight duty. A publication or membership change fails closed.
- The Site resolver obtains the category from the existing Office roster employment reader, which already loads StaffCategory. Actual DI supplies both this reader and the taxonomy resolver; the older two-argument synthetic Site fixtures retain their existing behavior.
- `OfficeAttendanceSourceReader` accepts broader approved Office mappings, compares their exact retained roster proof to the fresh effective publication, and rechecks exact employee/assignment/branch/category after boundary and policy IO. A changed assignment cannot return even an unconfigured-boundary result as though it were the original retained source.

Exact six-file source hashes are retained in `roster-taxonomy-source-hashes.json`. No Infrastructure repository, Domain entity, model, migration, SQL, host, provider, account, policy value or permission grant was changed by this slice.

## Executed verification

1. Initial standalone RED: 7 passed / 11 failed against the original roster and Office reader. Failures demonstrated broader routing, absent publication proof, unavailable published authority still falling back to Office, Site overnight mapping changes, and broader Office source membership rejection.
2. Initial GREEN after implementing the actual production classes: 18 passed / 0 failed. The Application-only Release build emitted no warnings/errors.
3. Expanded source checks reproduced two further failures against the then-built assembly: assignment replacement during the final taxonomy IO, and an empty legacy duty interval. Both corresponding production fixes were added before final verification.
4. Final current-source managed-DLL check: **30 passed / 0 failed**, exit code 0. The parent coordinated a fresh Application Release build before this run; the check did not build or mutate the application itself. Audit and executed source SHA256 match: `6B847BAFF7003A6E5196CEA35F5C0B83548DAFA7397F9FA3E23C695739A1A6D6`.
5. Fresh unchanged regressions against the same current Application assembly: `docs/group-rosters/VerifySiteScheduling.cs` **171 passed / 0 scenario failures**; `docs/attendance-gap-b/B01VerifyOfficeSource.cs` **16 passed**.

Successful source assertions in this slice: 217 (30 + 171 + 16). Counts do not establish complete employee workflows.

Executed final commands used separate managed wrappers on D: and existing Release Application/Domain assemblies:

```powershell
dotnet run --project D:/CosmixPartAContinuation20261013/roster-taxonomy/Checks.csproj --configuration Release --no-restore
dotnet run --project D:/CosmixPartAContinuation20261013/roster-regressions/VerifySiteScheduling/Checks.csproj --configuration Release --no-restore
dotnet run --project D:/CosmixPartAContinuation20261013/roster-regressions/B01VerifyOfficeSource/Checks.csproj --configuration Release --no-restore
```

The retained standalone audit harness is `VerifyRosterTaxonomy.cs`; `run-roster-taxonomy-checks.ps1` creates a separate output wrapper after an actual Release build. It does not recreate a test project in the backend solution.

## Additional attempted regression and operational boundaries

The older `docs/shift-publication/VerifyPublication.cs` fixture could not compile: CS7036 at wrapper line 345 because its `ShiftVersionPublication` constructor invocation lacks the B-added `IOfficeRosterRepository` and `OfficeRosterImpactValidator` arguments. This is an existing fixture mismatch, not counted as passed and not edited outside this slice.

Approved broader company mappings remain unconfigured until their protected independent publication workflow is completed. No default policy, duty rule, OT eligibility, salary result, management rehire approval, HTTP acceptance or external provider success was invented. Root owns capture/calculation/OT/payroll consumers, actual composition/model verification and the final integrated delivery document.

During verification C: became full and one failed harness write truncated the owned audit file. The original successful 18-check TEMP copy was preserved on D:, the expanded final harness was restored, and its executed source hash was checked. No product source was lost by this slice and no files were deleted. Read-only recovery assistance also confirmed the frozen calculation producer checksum from its portable PDB and extracted exact historical source/patch material to D:; the parent recovered and verified that separate producer before the final build.
