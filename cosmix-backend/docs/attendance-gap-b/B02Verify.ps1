param([switch]$RequireCatalog)
$ErrorActionPreference = 'Stop'
$b02Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$b02Artifacts = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-b02-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $b02Artifacts
Write-Host "B02 artifacts: $b02Artifacts"
& dotnet build (Join-Path $b02Root 'Cosmix.Backend.sln') --configuration Release `
    --artifacts-path (Join-Path $b02Artifacts 'solution') -p:RestoreLockedMode=true `
    2>&1 | Tee-Object -FilePath (Join-Path $b02Artifacts 'release-build.log')
if ($LASTEXITCODE -ne 0) { throw 'B02 strict Release build failed.' }
foreach ($b02Check in @('Rules', 'Evaluator', 'PolicyReader', 'Management', 'Persistence')) {
    $b02Extra = @()
    if ($RequireCatalog -and $b02Check -eq 'Management') { $b02Extra = @('--', '--require-catalog') }
    & dotnet run --file (Join-Path $PSScriptRoot ('B02Verify' + $b02Check + '.cs')) --configuration Release `
        --artifacts-path (Join-Path $b02Artifacts $b02Check) @b02Extra `
        2>&1 | Tee-Object -FilePath (Join-Path $b02Artifacts ($b02Check + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "B02 $b02Check check failed." }
}
Write-Host 'B02 source checks finished. Read management HOLD status; no SQL execution, provider or hosted acceptance claimed.'
