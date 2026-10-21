$ErrorActionPreference = 'Stop'
$b07Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$b07Artifacts = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-b07-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $b07Artifacts
Write-Host "B07 verification artifacts: $b07Artifacts"

& dotnet build (Join-Path $b07Root 'Cosmix.Backend.sln') --configuration Release `
    --artifacts-path (Join-Path $b07Artifacts 'solution') -p:RestoreLockedMode=true `
    2>&1 | Tee-Object -FilePath (Join-Path $b07Artifacts 'release-build.log')
if ($LASTEXITCODE -ne 0) { throw "B07 Release build failed: $b07Artifacts/release-build.log" }

$b07Checks = @(
    @{ Name = 'service'; File = 'docs/attendance-completion/VerifySiteReports.cs' },
    @{ Name = 'queries'; File = 'docs/attendance-completion/VerifySiteReportQueries.cs' },
    @{ Name = 'contract'; File = 'docs/attendance-gap-b/B07VerifyQueueContract.cs' }
)
foreach ($b07Check in $b07Checks) {
    & dotnet run --file (Join-Path $b07Root $b07Check.File) --configuration Release `
        --artifacts-path (Join-Path $b07Artifacts $b07Check.Name) `
        2>&1 | Tee-Object -FilePath (Join-Path $b07Artifacts ($b07Check.Name + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "B07 $($b07Check.Name) check failed; inspect $b07Artifacts" }
}
Write-Host 'PASS: B07 build and standalone checks. No hosts, SQL execution or authenticated acceptance.'
