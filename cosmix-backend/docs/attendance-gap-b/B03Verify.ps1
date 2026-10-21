$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifacts = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-b03-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $artifacts
Write-Host "B03 verification artifacts: $artifacts"
& dotnet build (Join-Path $root 'Cosmix.Backend.sln') --configuration Release --artifacts-path (Join-Path $artifacts 'release') -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Strict Release build failed.' }
$checks = @(
    'docs/attendance-corrections/VerifyCorrectionService.cs',
    'docs/attendance-corrections/VerifyCarryForwardService.cs',
    'docs/attendance-gap-b/B03VerifyOfficeEvidence.cs',
    'docs/attendance-gap-b/B03VerifyAlternativePolicies.cs',
    'docs/attendance-gap-b/B03VerifyRegisteredContracts.cs',
    'docs/attendance-corrections/VerifyCorrectionQueries.cs',
    'docs/attendance-corrections/VerifyCorrectionEligibility.cs',
    'docs/attendance-corrections/VerifyCarryForwardRules.cs',
    'docs/attendance-corrections/VerifyCarryForwardQueries.cs',
    'docs/attendance-corrections/VerifyAuditReads.cs',
    'docs/attendance-corrections/VerifyExceptions.cs',
    'docs/attendance-corrections/VerifyExceptionQueries.cs'
)
foreach ($file in $checks) {
    $name = [IO.Path]::GetFileNameWithoutExtension($file)
    & dotnet run --file (Join-Path $root $file) --configuration Release --artifacts-path (Join-Path $artifacts $name)
    if ($LASTEXITCODE -ne 0) { throw "$name failed." }
}
Write-Host 'B03 strict offline checks finished. No SQL, real providers, API host, credentials or business-data writes executed.'
