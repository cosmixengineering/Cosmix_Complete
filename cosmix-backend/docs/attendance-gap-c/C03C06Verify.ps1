param([switch]$BuildSlotGranted, [switch]$RequireCatalog, [switch]$SkipBuild, [string[]]$Checks = @('Rules','Workflow','Repository','Composition'))
$ErrorActionPreference = 'Stop'
if (!$BuildSlotGranted) { throw 'Parent must grant the serial C03/C06 build slot before running.' }
if ((Get-PSDrive C).Free -lt 350MB) { throw 'C free space below mandatory 350 MB reserve.' }
$c03Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$c03Artifacts = Join-Path 'D:\CosmixC03C06_20261013\runs' ([Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($c03Artifacts)
$c03Temp = Join-Path $c03Artifacts 'temp'
$null = [IO.Directory]::CreateDirectory($c03Temp)
$env:TEMP = $c03Temp
$env:TMP = $c03Temp
$env:TMPDIR = $c03Temp
$env:DOTNET_CLI_HOME = Join-Path $c03Artifacts 'cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $c03Artifacts 'nuget-http'
$env:NUGET_SCRATCH = Join-Path $c03Artifacts 'nuget-scratch'
$env:NUGET_PACKAGES = 'D:\CosmixC03C06_20261013\nuget-packages'
$env:MSBUILDDISABLENODEREUSE = '1'
Write-Output "C03/C06 artifacts: $c03Artifacts"
if (!$SkipBuild) {
    & dotnet build (Join-Path $c03Root 'Cosmix.Backend.sln') -c Release --artifacts-path (Join-Path $c03Artifacts 'solution') -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false `
        2>&1 | Tee-Object -FilePath (Join-Path $c03Artifacts 'release.log')
    if ($LASTEXITCODE -ne 0) { throw 'C03/C06 Release build failed.' }
}
foreach ($check in $Checks) {
    if ((Get-PSDrive C).Free -lt 350MB) { throw 'C reserve fell below 350 MB; further commands held.' }
    $extra = @()
    if ($RequireCatalog -and $check -eq 'Workflow') { $extra = @('--','--require-catalog') }
    $source = Join-Path $PSScriptRoot ('C03C06Verify' + $check + '.cs')
    $runFile = Join-Path $c03Artifacts ('C03C06' + $check + '-' + [Guid]::NewGuid().ToString('N') + '.cs')
    $sourceText = [IO.File]::ReadAllText($source)
    $sourceText = [regex]::Replace($sourceText, '(?m)^#:project (.+)\r?$', {
        param($match)
        '#:project ' + [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $match.Groups[1].Value.Trim())).Replace('\','/')
    })
    $runStream = [IO.File]::Open($runFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $runBytes = [Text.Encoding]::UTF8.GetBytes($sourceText); $runStream.Write($runBytes,0,$runBytes.Length) } finally { $runStream.Dispose() }
    & dotnet run --file $runFile -c Release --artifacts-path (Join-Path $c03Artifacts $check) -p:NuGetAudit=false @extra `
        2>&1 | Tee-Object -FilePath (Join-Path $c03Artifacts ($check + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "C03/C06 $check verification failed." }
}
Write-Output 'C03/C06 source checks finished. No SQL, host, credentials, provider/client acceptance or payroll integration was exercised.'
