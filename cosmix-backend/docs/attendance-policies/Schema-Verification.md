# Attendance policy OpenAPI schema checks

The policy rules use exact snake_case converters and strict numeric JSON. The default
.NET exporter treated the custom enum converters as unconstrained and advertised
quoted numeric values despite runtime rejection. This was reproduced with the
existing Domain assembly and .NET 10 Web serializer options before the fix.

`AttendancePolicySchemaTransformer` now repairs the seventeen policy/applicability enum types
and strict numeric fields on the known rule/publication/leave/payroll-input forms. It documents
named options, nullable input and integer/decimal distinctions while preserving unrelated schemas.
The metadata does not approve, publish or activate a policy. Historical schema 1/2 members remain
readable; optional schema-3 choices must preserve explicit null as well as omitted input.

`VerifySchema.cs` exercises the actual exporter and transformer with Web JSON metadata,
compares documented names to runtime converter output and preserves an unrelated form
as a regression case. `VerifySchemaDocument.ps1` separately checks the real saved
OpenAPI document after the bounded anonymous host sweep. Neither creates a test project
or needs database, credentials, provider access or employee data.

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/attendance-final-checks'
$env:TMP = $env:TEMP
dotnet run --file docs/attendance-policies/VerifySchema.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Api" --property:NuGetAudit=false
pwsh -NoProfile -File docs/attendance-policies/VerifySchemaDocument.ps1
```

The actual saved document then reproduced a second defect: ASP.NET wrapped a nullable enum
component in `oneOf(null, reference)`, so null matched both branches. Enum components now contain
only named strings; the framework's wrapper owns nullability. Checks require exactly one branch
to match null and each allowed string, and no branch to match a number or unknown string.

At the earlier report/reminder checkpoint on 12 October 2026, `VerifySchema.cs` passed **114** exporter/transformer checks in
Release mode (exit 0). After a fresh successful solution build and 726-check registered HTTP
sweep, `VerifySchemaDocument.ps1` passed **80** checks against the actual generated document.
A prior feature HTTP attempt against an intermediate build was interrupted before a result,
and its owned listener was confirmed stopped; it is not counted.
These source/schema checks are not authenticated policy, publication, SQL or payroll acceptance.

The subsequent financial continuation expanded the exporter harness to schema 3 and reproduced
another actual-document issue: optional inline enum fields lost nullability even though required
nullable enum references retained their correct wrappers. A child enum transform overwrote the
parent form repair. The fix identifies optional versioned enum properties before assigning their
type/value list; required nullable references still have one exclusive null wrapper. A regression
reproduces that transform order. Runtime serialization and canonical policy bytes are unchanged.
The fresh pass completed **258** exporter/transformer checks, **965** anonymous HTTP/traffic checks
and **202** checks on the actual generated document. These results and remaining limits are
recorded in [current Attendance verification](../attendance-completion/Verification.md).
