param([switch]$BuildSlotReleased, [string[]]$Checks = @('Rules', 'Service', 'Sources', 'Persistence', 'RegisteredJson'), [string]$PackageCache)
$ErrorActionPreference = 'Stop'
if (!$BuildSlotReleased) { throw 'C04 missing-evidence builds are held until the parent releases a serial slot.' }
if ([IO.DriveInfo]::new('C').AvailableFreeSpace -lt 350MB) { throw 'C04 missing-evidence C: reserve is below 350 MB.' }
$c04MissingIdentity = [Guid]::NewGuid().ToString('N')
$c04MissingRoot = Join-Path 'D:/CodexTemp' ('c04-missing-' + $c04MissingIdentity)
$null = New-Item -ItemType Directory -Path $c04MissingRoot
if ($PackageCache) {
    $c04MissingPackages = [IO.Path]::GetFullPath($PackageCache)
    if ([IO.Path]::GetPathRoot($c04MissingPackages) -ne 'D:\') { throw 'C04 missing package cache must be on D:.' }
} else { $c04MissingPackages = Join-Path $c04MissingRoot 'packages' }
$c04MissingEnvironment = @{}
foreach ($c04MissingName in @('TEMP', 'TMP', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'NUGET_HTTP_CACHE_PATH', 'NUGET_SCRATCH', 'NUGET_PACKAGES')) {
    $c04MissingEnvironment[$c04MissingName] = [Environment]::GetEnvironmentVariable($c04MissingName, 'Process')
}
try {
    $env:TEMP = $c04MissingRoot; $env:TMP = $c04MissingRoot; $env:DOTNET_CLI_HOME = $c04MissingRoot
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $c04MissingRoot 'nuget-http'; $env:NUGET_SCRATCH = Join-Path $c04MissingRoot 'NuGetScratch'
    $env:NUGET_PACKAGES = $c04MissingPackages
    Write-Host "C04 missing owned artifacts/run identity: $c04MissingRoot"
    foreach ($c04MissingCheck in $Checks) {
        $c04MissingText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot ('C04MissingVerify' + $c04MissingCheck + '.cs')))
        $c04MissingText = [regex]::Replace($c04MissingText, '(?m)^#:project (.+)\r?$', {
            param($c04MissingMatch)
            '#:project ' + [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $c04MissingMatch.Groups[1].Value.Trim())).Replace('\', '/')
        })
        $c04MissingRunfile = Join-Path $c04MissingRoot ('C04Missing' + $c04MissingCheck + '-' + $c04MissingIdentity + '.cs')
        $c04MissingStream = [IO.File]::Open($c04MissingRunfile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $c04MissingStream.Write([Text.Encoding]::UTF8.GetBytes($c04MissingText)) } finally { $c04MissingStream.Dispose() }
        & dotnet run --file $c04MissingRunfile --configuration Release --artifacts-path (Join-Path $c04MissingRoot $c04MissingCheck) -p:NuGetAudit=false 2>&1 |
            Tee-Object -FilePath (Join-Path $c04MissingRoot ($c04MissingCheck + '.log'))
        if ($LASTEXITCODE -ne 0) { throw "C04 missing $c04MissingCheck failed." }
    }
    Write-Host 'C04 missing standalone checks passed. No SQL, host, provider or deployment acceptance.'
} finally {
    foreach ($c04MissingName in $c04MissingEnvironment.Keys) { [Environment]::SetEnvironmentVariable($c04MissingName, $c04MissingEnvironment[$c04MissingName], 'Process') }
}
