param([switch]$BuildsReleased, [Parameter(Mandatory)][string]$BinaryDirectory, [switch]$ApplicationOnly)
$ErrorActionPreference = 'Stop'
if (!$BuildsReleased) { throw 'C01 check compilation remains held until the parent releases the serial slot.' }
if ([IO.DriveInfo]::new('C').AvailableFreeSpace -lt 350MB) { throw 'C01 source proof hold: C reserve below 350 MB.' }
$c01Repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$c01Binaries = [IO.Path]::GetFullPath($BinaryDirectory)
if ([IO.Path]::GetPathRoot($c01Binaries) -ne 'D:\' -or !(Test-Path -LiteralPath $c01Binaries -PathType Container)) {
    throw 'Supply the parent fresh binary directory on D:.'
}
$c01Identity = [Guid]::NewGuid().ToString('N')
$c01Root = Join-Path 'D:/CodexTemp' ('cosmix-c01-proof-' + $c01Identity)
$null = New-Item -ItemType Directory -Path $c01Root
$c01Runfile = Join-Path $c01Root ('C01SourceProof-' + $c01Identity + '.cs')
$c01Input = [IO.File]::OpenRead((Join-Path $PSScriptRoot 'C01VerifySourceProof.cs'))
try {
    $c01Output = [IO.File]::Open($c01Runfile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $c01Input.CopyTo($c01Output) } finally { $c01Output.Dispose() }
} finally { $c01Input.Dispose() }
$c01Environment = @{}
foreach ($c01Variable in @('TEMP', 'TMP', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'NUGET_HTTP_CACHE_PATH', 'NUGET_SCRATCH')) {
    $c01Environment[$c01Variable] = [Environment]::GetEnvironmentVariable($c01Variable, 'Process')
}
try {
    $env:TEMP = $c01Root; $env:TMP = $c01Root; $env:DOTNET_CLI_HOME = $c01Root
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $c01Root 'nuget-http'; $env:NUGET_SCRATCH = Join-Path $c01Root 'NuGetScratch'
    $c01Arguments = @($c01Repo, $c01Binaries)
    if ($ApplicationOnly) { $c01Arguments += '--application-only' }
    Write-Host "C01 proof artifacts and independent run identity: $c01Root"
    & dotnet run --file $c01Runfile --configuration Release --artifacts-path (Join-Path $c01Root 'proof') -p:NuGetAudit=false -- @c01Arguments 2>&1 |
        Tee-Object -FilePath (Join-Path $c01Root 'SourceProof.log')
    if ($LASTEXITCODE -ne 0) { throw 'C01 actual binary/source proof failed.' }
} finally {
    foreach ($c01Variable in $c01Environment.Keys) { [Environment]::SetEnvironmentVariable($c01Variable, $c01Environment[$c01Variable], 'Process') }
}
