param([switch]$BuildsReleased, [switch]$SkipBuild, [string[]]$Checks = @('Runtime', 'Publication', 'Persistence', 'RegisteredJson'))
$ErrorActionPreference = 'Stop'
if (!$BuildsReleased) { throw 'C01 builds are held. Run only after explicit parent recovery/build release, with -BuildsReleased.' }
if ([IO.DriveInfo]::new('C').AvailableFreeSpace -lt 350MB) { throw 'C01 build hold: C reserve below 350 MB.' }
$c01Repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$c01Identity = [Guid]::NewGuid().ToString('N')
$c01Root = Join-Path 'D:/CodexTemp' ('cosmix-c01-' + $c01Identity)
$null = New-Item -ItemType Directory -Path $c01Root
$c01Environment = @{}
foreach ($c01Variable in @('TEMP', 'TMP', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'NUGET_HTTP_CACHE_PATH', 'NUGET_SCRATCH')) {
    $c01Environment[$c01Variable] = [Environment]::GetEnvironmentVariable($c01Variable, 'Process')
}
try {
    $env:TEMP = $c01Root; $env:TMP = $c01Root; $env:DOTNET_CLI_HOME = $c01Root
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $c01Root 'nuget-http'
    $env:NUGET_SCRATCH = Join-Path $c01Root 'NuGetScratch'
    Write-Host "C01 owned artifacts and run identity: $c01Root"
    if (!$SkipBuild) {
        & dotnet build (Join-Path $c01Repo 'Cosmix.Backend.sln') --configuration Release --artifacts-path (Join-Path $c01Root 'solution') -p:RestoreLockedMode=true -p:NuGetAudit=false 2>&1 |
            Tee-Object -FilePath (Join-Path $c01Root 'Release.log')
        if ($LASTEXITCODE -ne 0) { throw 'C01 Release build failed.' }
    }
    foreach ($c01Check in $Checks) {
        $c01Source = Join-Path $PSScriptRoot ('C01Verify' + $c01Check + '.cs')
        $c01Runfile = Join-Path $c01Root ('C01' + $c01Check + '-' + $c01Identity + '.cs')
        $c01Text = [IO.File]::ReadAllText($c01Source)
        $c01Text = [regex]::Replace($c01Text, '(?m)^#:project (.+)\r?$', {
            param($c01Match)
            '#:project ' + [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $c01Match.Groups[1].Value.Trim())).Replace('\', '/')
        })
        [IO.File]::WriteAllText($c01Runfile, $c01Text)
        & dotnet run --file $c01Runfile --configuration Release --artifacts-path (Join-Path $c01Root $c01Check) -p:NuGetAudit=false 2>&1 |
            Tee-Object -FilePath (Join-Path $c01Root ($c01Check + '.log'))
        if ($LASTEXITCODE -ne 0) { throw "C01 $c01Check failed." }
    }
    Write-Host 'C01 source checks passed. No SQL execution, host, provider, employee fixtures or deployment acceptance.'
}
finally {
    foreach ($c01Variable in $c01Environment.Keys) { [Environment]::SetEnvironmentVariable($c01Variable, $c01Environment[$c01Variable], 'Process') }
}
